﻿using System.Text.Json;
using Api.Database.Models;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("robots")]
public class RobotController : ControllerBase
{
    private readonly ILogger<RobotController> _logger;
    private readonly RobotService _robotService;
    private readonly IsarService _isarService;

    public RobotController(
        ILogger<RobotController> logger,
        RobotService robotService,
        IsarService isarService
    )
    {
        _logger = logger;
        _robotService = robotService;
        _isarService = isarService;
    }

    /// <summary>
    /// List all robots on the asset.
    /// </summary>
    /// <remarks>
    /// <para> This query gets all robots (paginated) </para>
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IList<Robot>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IList<Robot>>> GetRobots()
    {
        var robots = await _robotService.ReadAll();
        return Ok(robots);
    }

    /// <summary>
    /// Gets the robot with the specified id
    /// </summary>
    /// <remarks>
    /// <para> This query gets the robot with the specified id </para>
    /// </remarks>
    [HttpGet]
    [Route("{id}")]
    [ProducesResponseType(typeof(Robot), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Robot>> GetRobotById([FromRoute] string id)
    {
        var robot = await _robotService.Read(id);
        if (robot == null)
            return NotFound($"Could not find robot with id {id}");
        return Ok(robot);
    }

    /// <summary>
    /// Create robot and add to database
    /// </summary>
    /// <remarks>
    /// <para> This query creates a robot and adds it to the database </para>
    /// </remarks>
    /// <returns> Robot </returns>
    [HttpPost]
    [ProducesResponseType(typeof(Robot), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Robot>> PostRobot([FromBody] Robot robot)
    {
        var newRobot = await _robotService.Create(robot);
        return CreatedAtAction(nameof(GetRobotById), new { id = newRobot.Id }, newRobot);
    }

    /// <summary>
    /// Updates a robot in the database
    /// </summary>
    /// <remarks>
    /// </remarks>
    /// <returns> Updated robot </returns>
    /// <response code="200"> The robot was succesfully updated </response>
    /// <response code="400"> The robot data is invalid </response>
    /// <response code="404"> There was no robot with the given ID in the database </response>
    [HttpPut]
    [Route("{id}")]
    [ProducesResponseType(typeof(Robot), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Robot>> UpdateRobot(
        [FromRoute] string id,
        [FromBody] Robot robot
    )
    {
        _logger.LogInformation("Updating robot with id: {id}", id);

        if (!ModelState.IsValid)
            return BadRequest("Invalid data.");

        if (id != robot.Id)
        {
            _logger.LogError("Id: {id} not corresponding to updated robot", id);
            return BadRequest("Inconsistent Id");
        }

        try
        {
            var updatedRobot = await _robotService.Update(robot);

            _logger.LogInformation($"Successful PUT of robot to database");

            return Ok(updatedRobot);
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"PUTing robot to database");
            throw;
        }
    }

    /// <summary>
    /// Start a mission for a given robot
    /// </summary>
    /// <remarks>
    /// <para> This query starts a mission for a given robot and creates a report </para>
    /// </remarks>
    [HttpPost]
    [Route("{robotId}/start/{missionId}")]
    [ProducesResponseType(typeof(Report), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Report>> StartMission(
        [FromRoute] string robotId,
        [FromRoute] string missionId
    )
    {
        var robot = await _robotService.Read(robotId);
        if (robot == null)
            return NotFound($"Could not find robot with robot id {robotId}");
        var report = await _isarService.StartMission(robot, missionId);
        return Ok(report);
    }

    /// <summary>
    /// Stop robot
    /// </summary>
    /// <remarks>
    /// <para> This query stops a robot based on id </para>
    /// </remarks>
    [HttpPost]
    [Route("{robotId}/stop/")]
    [ProducesResponseType(typeof(IsarStopMissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IsarStopMissionResponse>> StopMission([FromRoute] string robotId)
    {
        var response = await _isarService.StopMission();
        if (!response.IsSuccessStatusCode)
            _logger.LogError("Could not stop mission with id {robotId}", robotId);
        if (response.Content != null)
        {
            string? responseContent = await response.Content.ReadAsStringAsync();
            var isarResponse = JsonSerializer.Deserialize<IsarStopMissionResponse>(responseContent);
            return Ok(isarResponse);
        }
        return NotFound($"Could not stop mission on robot: {robotId}");
    }

    /// <summary>
    /// Get video streams for a given robot
    /// </summary>
    /// <remarks>
    /// <para> Retrieves the video streams available for the given robot </para>
    /// </remarks>
    [HttpGet]
    [Route("{robotId}/video_streams/")]
    [ProducesResponseType(typeof(IList<VideoStream>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IList<VideoStream>>> GetVideoStreams([FromRoute] string robotId)
    {
        var robot = await _robotService.Read(robotId);
        if (robot == null)
            return NotFound($"Could not find robot with id {robotId}");

        return Ok(robot.VideoStreams);
    }

    /// <summary>
    /// Add a video stream to a given robot
    /// </summary>
    /// <remarks>
    /// <para> Adds a provided video stream to the given robot </para>
    /// </remarks>
    /// <returns> The updated robot </returns>
    [HttpPost]
    [Route("{robotId}/video_streams/")]
    [ProducesResponseType(typeof(Robot), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Robot>> CreateVideoStream(
        [FromRoute] string robotId,
        [FromBody] VideoStream videoStream
    )
    {
        var robot = await _robotService.Read(robotId);
        if (robot == null)
            return NotFound($"Could not find robot with id {robotId}");

        if (robot.VideoStreams is null)
            robot.VideoStreams = new List<VideoStream>();

        // These will be autogenerated
        videoStream.Id = null;
        videoStream.RobotId = null;

        robot.VideoStreams.Add(videoStream);

        try
        {
            var updatedRobot = await _robotService.Update(robot);

            return CreatedAtAction(
                nameof(GetVideoStreams),
                new { robotId = updatedRobot.Id },
                updatedRobot
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding video stream to robot");
            throw;
        }
    }
}
