﻿using System.Text.Json;
using Api.Controllers.Models;
using Api.Database.Models;
using Api.Mqtt;
using Api.Mqtt.Events;
using Api.Mqtt.MessageModels;
using Api.Services;
using Api.Services.ActionServices;
using Api.Services.Events;
using Api.Services.Models;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Api.EventHandlers
{
    /// <summary>
    ///     A background service which listens to events and performs callback functions.
    /// </summary>
    public class MqttEventHandler : EventHandlerBase
    {
        private readonly ILogger<MqttEventHandler> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public MqttEventHandler(ILogger<MqttEventHandler> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            // Reason for using factory: https://www.thecodebuzz.com/using-dbcontext-instance-in-ihostedservice/
            _scopeFactory = scopeFactory;

            Subscribe();
        }


        private IServiceProvider GetServiceProvider() { return _scopeFactory.CreateScope().ServiceProvider; }

        public override void Subscribe()
        {
            MqttService.MqttIsarStatusReceived += OnIsarStatus;
            MqttService.MqttIsarRobotInfoReceived += OnIsarRobotInfo;
            MqttService.MqttIsarMissionReceived += OnIsarMissionUpdate;
            MqttService.MqttIsarTaskReceived += OnIsarTaskUpdate;
            MqttService.MqttIsarStepReceived += OnIsarStepUpdate;
            MqttService.MqttIsarBatteryReceived += OnIsarBatteryUpdate;
            MqttService.MqttIsarPressureReceived += OnIsarPressureUpdate;
            MqttService.MqttIsarPoseReceived += OnIsarPoseUpdate;
            MqttService.MqttIsarCloudHealthReceived += OnIsarCloudHealthUpdate;
        }

        public override void Unsubscribe()
        {
            MqttService.MqttIsarStatusReceived -= OnIsarStatus;
            MqttService.MqttIsarRobotInfoReceived -= OnIsarRobotInfo;
            MqttService.MqttIsarMissionReceived -= OnIsarMissionUpdate;
            MqttService.MqttIsarTaskReceived -= OnIsarTaskUpdate;
            MqttService.MqttIsarStepReceived -= OnIsarStepUpdate;
            MqttService.MqttIsarBatteryReceived -= OnIsarBatteryUpdate;
            MqttService.MqttIsarPressureReceived -= OnIsarPressureUpdate;
            MqttService.MqttIsarPoseReceived -= OnIsarPoseUpdate;
            MqttService.MqttIsarCloudHealthReceived -= OnIsarCloudHealthUpdate;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) { await stoppingToken; }


        private async void OnIsarStatus(object? sender, MqttReceivedArgs mqttArgs)
        {
            var provider = GetServiceProvider();
            var robotService = provider.GetRequiredService<IRobotService>();
            var missionSchedulingService = provider.GetRequiredService<IMissionSchedulingService>();

            var isarStatus = (IsarStatusMessage)mqttArgs.Message;

            var robot = await robotService.ReadByIsarId(isarStatus.IsarId);

            if (robot == null)
            {
                _logger.LogInformation("Received message from unknown ISAR instance {Id} with robot name {Name}", isarStatus.IsarId, isarStatus.RobotName);
                return;
            }

            if (robot.Status == isarStatus.Status) { return; }

            robot.Status = isarStatus.Status;
            await robotService.Update(robot);
            _logger.LogInformation("Updated status for robot {Name} to {Status}", robot.Name, isarStatus.Status);

            if (isarStatus.Status == RobotStatus.Available) missionSchedulingService.TriggerRobotAvailable(new RobotAvailableEventArgs(robot.Id));
        }

        private async void OnIsarRobotInfo(object? sender, MqttReceivedArgs mqttArgs)
        {
            var provider = GetServiceProvider();
            var robotService = provider.GetRequiredService<IRobotService>();
            var installationService = provider.GetRequiredService<IInstallationService>();

            var isarRobotInfo = (IsarRobotInfoMessage)mqttArgs.Message;

            var installation = await installationService.ReadByName(isarRobotInfo.CurrentInstallation);

            if (installation is null)
            {
                _logger.LogError(
                    new InstallationNotFoundException($"No installation with code {isarRobotInfo.CurrentInstallation} found"),
                    "Could not create new robot due to missing installation"
                );
                return;
            }

            try
            {
                var robot = await robotService.ReadByIsarId(isarRobotInfo.IsarId);

                if (robot == null)
                {
                    _logger.LogInformation(
                        "Received message from new ISAR instance '{Id}' with robot name '{Name}'. Adding new robot to database",
                        isarRobotInfo.IsarId, isarRobotInfo.RobotName);

                    var robotQuery = new CreateRobotQuery
                    {
                        IsarId = isarRobotInfo.IsarId,
                        Name = isarRobotInfo.RobotName,
                        RobotType = isarRobotInfo.RobotType,
                        SerialNumber = isarRobotInfo.SerialNumber,
                        CurrentInstallationCode = installation.InstallationCode,
                        VideoStreams = isarRobotInfo.VideoStreamQueries,
                        Host = isarRobotInfo.Host,
                        Port = isarRobotInfo.Port,
                        RobotCapabilities = isarRobotInfo.Capabilities,
                        Status = RobotStatus.Available,
                    };

                    var newRobot = await robotService.CreateFromQuery(robotQuery);
                    _logger.LogInformation("Added robot '{RobotName}' with ISAR id '{IsarId}' to database", newRobot.Name, newRobot.IsarId);

                    return;
                }

                List<string> updatedFields = [];

                if (isarRobotInfo.VideoStreamQueries is not null) UpdateVideoStreamsIfChanged(isarRobotInfo.VideoStreamQueries, ref robot, ref updatedFields);
                if (isarRobotInfo.Host is not null) UpdateHostIfChanged(isarRobotInfo.Host, ref robot, ref updatedFields);

                UpdatePortIfChanged(isarRobotInfo.Port, ref robot, ref updatedFields);

                if (isarRobotInfo.CurrentInstallation is not null) UpdateCurrentInstallationIfChanged(installation, ref robot, ref updatedFields);
                if (isarRobotInfo.Capabilities is not null) UpdateRobotCapabilitiesIfChanged(isarRobotInfo.Capabilities, ref robot, ref updatedFields);
                if (updatedFields.IsNullOrEmpty()) return;

                robot = await robotService.Update(robot);
                _logger.LogInformation("Updated robot '{Id}' ('{RobotName}') in database: {Updates}", robot.Id, robot.Name, updatedFields);

            }
            catch (DbUpdateException e) { _logger.LogError(e, "Could not add robot to db"); }
            catch (Exception e) { _logger.LogError(e, "Could not update robot in db"); }
        }

        private static void UpdateVideoStreamsIfChanged(List<CreateVideoStreamQuery> videoStreamQueries, ref Robot robot, ref List<string> updatedFields)
        {
            var updatedStreams = videoStreamQueries
                .Select(
                    stream =>
                        new VideoStream
                        {
                            Name = stream.Name,
                            Url = stream.Url,
                            Type = stream.Type
                        }
                )
                .ToList();

            var existingVideoStreams = robot.VideoStreams;
            if (updatedStreams.Count == robot.VideoStreams.Count && updatedStreams.TrueForAll(stream => existingVideoStreams.Contains(stream))) return;
            var serializerOptions = new JsonSerializerOptions { WriteIndented = true };
            updatedFields.Add(
                $"\nVideoStreams ({JsonSerializer.Serialize(robot.VideoStreams, serializerOptions)} "
                + "\n-> "
                + $"\n{JsonSerializer.Serialize(updatedStreams, serializerOptions)})\n"
            );
            robot.VideoStreams = updatedStreams;
        }

        private static void UpdateHostIfChanged(string host, ref Robot robot, ref List<string> updatedFields)
        {
            if (host.Equals(robot.Host, StringComparison.Ordinal)) return;

            updatedFields.Add($"\nHost ({robot.Host} -> {host})\n");
            robot.Host = host;
        }

        private static void UpdatePortIfChanged(int port, ref Robot robot, ref List<string> updatedFields)
        {
            if (port.Equals(robot.Port)) return;

            updatedFields.Add($"\nPort ({robot.Port} -> {port})\n");
            robot.Port = port;
        }

        private static void UpdateCurrentInstallationIfChanged(Installation newCurrentInstallation, ref Robot robot, ref List<string> updatedFields)
        {
            if (newCurrentInstallation.InstallationCode.Equals(robot.CurrentInstallation?.InstallationCode, StringComparison.Ordinal)) return;

            updatedFields.Add($"\nCurrentInstallation ({robot.CurrentInstallation} -> {newCurrentInstallation})\n");
            robot.CurrentInstallation = newCurrentInstallation;
        }

        public static void UpdateRobotCapabilitiesIfChanged(IList<RobotCapabilitiesEnum> newRobotCapabilities, ref Robot robot, ref List<string> updatedFields)
        {
            if (robot.RobotCapabilities != null && Enumerable.SequenceEqual(newRobotCapabilities, robot.RobotCapabilities)) return;

            updatedFields.Add($"\nRobotCapabilities ({robot.RobotCapabilities} -> {newRobotCapabilities})\n");
            robot.RobotCapabilities = newRobotCapabilities;
        }

        private async void OnIsarMissionUpdate(object? sender, MqttReceivedArgs mqttArgs)
        {
            var provider = GetServiceProvider();
            var missionRunService = provider.GetRequiredService<IMissionRunService>();
            var robotService = provider.GetRequiredService<IRobotService>();
            var taskDurationService = provider.GetRequiredService<ITaskDurationService>();
            var lastMissionRunService = provider.GetRequiredService<ILastMissionRunService>();
            var missionSchedulingService = provider.GetRequiredService<IMissionSchedulingService>();
            var signalRService = provider.GetRequiredService<ISignalRService>();

            var isarMission = (IsarMissionMessage)mqttArgs.Message;

            MissionStatus status;
            try { status = MissionRun.MissionStatusFromString(isarMission.Status); }
            catch (ArgumentException e)
            {
                _logger.LogError(e, "Failed to parse mission status from MQTT message. Mission with ISARMissionId '{IsarMissionId}' was not updated", isarMission.MissionId);
                return;
            }

            MissionRun flotillaMissionRun;
            try { flotillaMissionRun = await missionRunService.UpdateMissionRunStatusByIsarMissionId(isarMission.MissionId, status); }
            catch (MissionRunNotFoundException) { return; }

            _logger.LogInformation(
                "Mission '{Id}' (ISARMissionID='{IsarMissionId}') status updated to '{Status}' for robot '{RobotName}' with ISAR id '{IsarId}'",
                flotillaMissionRun.Id, isarMission.MissionId, isarMission.Status, isarMission.RobotName, isarMission.IsarId
            );

            if (!flotillaMissionRun.IsCompleted) return;

            var robot = await robotService.ReadByIsarId(isarMission.IsarId);
            if (robot is null)
            {
                _logger.LogError("Could not find robot '{RobotName}' with ISAR id '{IsarId}'", isarMission.RobotName, isarMission.IsarId);
                return;
            }

            if (flotillaMissionRun.IsLocalizationMission())
            {
                if (flotillaMissionRun.Status != MissionStatus.Successful)
                {
                    await robotService.UpdateCurrentArea(robot.Id, null);

                    signalRService.ReportGeneralFailToSignalR(robot, "Failed Localization Mission", $"Failed localization mission for robot {robot.Name}.");
                    _logger.LogError("Localization mission for robot '{RobotName}' failed.", isarMission.RobotName);
                }
                else { await robotService.UpdateCurrentArea(robot.Id, flotillaMissionRun.Area); }
            }

            try { await robotService.UpdateCurrentMissionId(robot.Id, null); }
            catch (RobotNotFoundException)
            {
                _logger.LogError("Robot {robotName} not found when updating current mission id to null", robot.Name);
                return;
            }

            _logger.LogInformation("Robot '{Id}' ('{Name}') - completed mission run {MissionRunId}", robot.IsarId, robot.Name, flotillaMissionRun.Id);
            missionSchedulingService.TriggerMissionCompleted(new MissionCompletedEventArgs(robot.Id));

            if (flotillaMissionRun.MissionId == null)
            {
                _logger.LogInformation("Mission run {missionRunId} does not have a mission definition assosiated with it", flotillaMissionRun.Id);
                return;
            }

            try { await lastMissionRunService.SetLastMissionRun(flotillaMissionRun.Id, flotillaMissionRun.MissionId); }
            catch (MissionNotFoundException)
            {
                _logger.LogError("Mission not found when setting last mission run for mission definition {missionId}", flotillaMissionRun.MissionId);
                return;
            }

            await taskDurationService.UpdateAverageDurationPerTask(robot.Model.Type);
        }

        private async void OnIsarTaskUpdate(object? sender, MqttReceivedArgs mqttArgs)
        {
            var provider = GetServiceProvider();
            var missionRunService = provider.GetRequiredService<IMissionRunService>();
            var missionTaskService = provider.GetRequiredService<IMissionTaskService>();
            var signalRService = provider.GetRequiredService<ISignalRService>();
            var task = (IsarTaskMessage)mqttArgs.Message;

            IsarTaskStatus status;
            try { status = IsarTask.StatusFromString(task.Status); }
            catch (ArgumentException e)
            {
                _logger.LogError(e, "Failed to parse mission status from MQTT message. Mission '{Id}' was not updated", task.MissionId);
                return;
            }

            try { await missionTaskService.UpdateMissionTaskStatus(task.TaskId, status); }
            catch (MissionTaskNotFoundException) { return; }

            var missionRun = await missionRunService.ReadByIsarMissionId(task.MissionId);
            if (missionRun is null) _logger.LogWarning("Mission run with ID {Id} was not found", task.MissionId);

            _ = signalRService.SendMessageAsync("Mission run updated", missionRun?.Area?.Installation, missionRun != null ? new MissionRunResponse(missionRun) : null);

            _logger.LogInformation(
                "Task '{Id}' updated to '{Status}' for robot '{RobotName}' with ISAR id '{IsarId}'", task.TaskId, task.Status, task.RobotName, task.IsarId);
        }

        private async void OnIsarStepUpdate(object? sender, MqttReceivedArgs mqttArgs)
        {
            var provider = GetServiceProvider();
            var missionRunService = provider.GetRequiredService<IMissionRunService>();
            var inspectionService = provider.GetRequiredService<IInspectionService>();
            var signalRService = provider.GetRequiredService<ISignalRService>();

            var step = (IsarStepMessage)mqttArgs.Message;

            // Flotilla does not care about DriveTo, Localization, MoveArm or ReturnToHome steps
            var stepType = IsarStep.StepTypeFromString(step.StepType);
            if (stepType is IsarStepType.DriveToPose or IsarStepType.Localize or IsarStepType.MoveArm or IsarStepType.ReturnToHome) return;

            IsarStepStatus status;
            try { status = IsarStep.StatusFromString(step.Status); }
            catch (ArgumentException e)
            {
                _logger.LogError(e, "Failed to parse mission status from MQTT message. Mission '{Id}' was not updated", step.MissionId);
                return;
            }

            try { await inspectionService.UpdateInspectionStatus(step.StepId, status); }
            catch (InspectionNotFoundException) { return; }

            var missionRun = await missionRunService.ReadByIsarMissionId(step.MissionId);
            if (missionRun is null) _logger.LogWarning("Mission run with ID {Id} was not found", step.MissionId);

            _ = signalRService.SendMessageAsync("Mission run updated", missionRun?.Area?.Installation, missionRun != null ? new MissionRunResponse(missionRun) : null);

            _logger.LogInformation(
                "Inspection '{Id}' updated to '{Status}' for robot '{RobotName}' with ISAR id '{IsarId}'", step.StepId, step.Status, step.RobotName, step.IsarId);
        }

        private async void OnIsarBatteryUpdate(object? sender, MqttReceivedArgs mqttArgs)
        {
            var provider = GetServiceProvider();
            var batteryTimeseriesService = provider.GetRequiredService<IBatteryTimeseriesService>();

            var batteryStatus = (IsarBatteryMessage)mqttArgs.Message;
            await batteryTimeseriesService.AddBatteryEntry(batteryStatus.BatteryLevel, batteryStatus.IsarId);
        }

        private async void OnIsarPressureUpdate(object? sender, MqttReceivedArgs mqttArgs)
        {
            var provider = GetServiceProvider();
            var pressureTimeseriesService = provider.GetRequiredService<IPressureTimeseriesService>();

            var pressureStatus = (IsarPressureMessage)mqttArgs.Message;
            await pressureTimeseriesService.AddPressureEntry(pressureStatus.PressureLevel, pressureStatus.IsarId);
        }

        private async void OnIsarPoseUpdate(object? sender, MqttReceivedArgs mqttArgs)
        {
            var provider = GetServiceProvider();
            var poseTimeseriesService = provider.GetRequiredService<IPoseTimeseriesService>();

            var poseStatus = (IsarPoseMessage)mqttArgs.Message;
            var pose = new Pose(poseStatus.Pose);

            await poseTimeseriesService.AddPoseEntry(pose, poseStatus.IsarId);
        }

        private async void OnIsarCloudHealthUpdate(object? sender, MqttReceivedArgs mqttArgs)
        {
            var provider = GetServiceProvider();
            var signalRService = provider.GetRequiredService<ISignalRService>();
            var robotService = provider.GetRequiredService<IRobotService>();
            var teamsMessageService = provider.GetRequiredService<ITeamsMessageService>();

            var cloudHealthStatus = (IsarCloudHealthMessage)mqttArgs.Message;

            var robot = await robotService.ReadByIsarId(cloudHealthStatus.IsarId);
            if (robot == null)
            {
                _logger.LogInformation("Received message from unknown ISAR instance {Id} with robot name {Name}", cloudHealthStatus.IsarId, cloudHealthStatus.RobotName);
                return;
            }

            string messageTitle = "Failed Telemetry";
            string message = $"Failed telemetry request for robot {cloudHealthStatus.RobotName}.";
            signalRService.ReportGeneralFailToSignalR(robot, messageTitle, message);

            teamsMessageService.TriggerTeamsMessageReceived(new TeamsMessageEventArgs(message));

        }
    }
}
