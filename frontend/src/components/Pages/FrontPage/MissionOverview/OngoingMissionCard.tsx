import { Card, Typography } from '@equinor/eds-core-react'
import { config } from 'config'
import { tokens } from '@equinor/eds-tokens'
import { Mission } from 'models/Mission'
import styled from 'styled-components'
import { MissionProgressDisplay } from 'components/Displays/MissionDisplays/MissionProgressDisplay'
import { MissionStatusDisplayWithHeader } from 'components/Displays/MissionDisplays/MissionStatusDisplay'
import { useNavigate } from 'react-router-dom'
import { MissionControlButtons } from 'components/Displays/MissionButtons/MissionControlButtons'
import { BatteryStatusDisplay } from 'components/Displays/RobotDisplays/BatteryStatusDisplay'
import { MissionRobotDisplay } from 'components/Displays/MissionDisplays/MissionRobotDisplay'
import { useRobotContext } from 'components/Contexts/RobotContext'
import { TaskType } from 'models/Task'

interface MissionProps {
    mission: Mission
}

const StyledMissionCard = styled(Card)`
    width: calc(100vw - 30px);
    max-width: 400px;
    padding: 10px;
    justify-content: space-between;
`
const StyledTitle = styled(Card)`
    width: 70%;
    height: 50px;
    justify-content: center;
    padding-left: 12px;
    :hover {
        background-color: #deedee;
        cursor: pointer;
    }
    box-shadow: none;
`
const TopContent = styled.div`
    display: flex;
    justify-content: space-between;
`
const BottomContent = styled.div`
    display: flex;
    justify-content: space-between;
    white-space: nowrap;
    gap: 6px;
`

export const OngoingMissionCard = ({ mission }: MissionProps): JSX.Element => {
    const { enabledRobots } = useRobotContext()
    let navigate = useNavigate()
    const routeChange = () => {
        const path = `${config.FRONTEND_BASE_ROUTE}/mission/${mission.id}`
        navigate(path)
    }

    const robot = enabledRobots.find((robot) => mission.robot.id === robot.id)

    let missionTaskType = TaskType.Inspection
    if (mission.tasks.every((task) => task.type === TaskType.ReturnHome)) missionTaskType = TaskType.ReturnHome
    if (mission.tasks.every((task) => task.type === TaskType.Localization)) missionTaskType = TaskType.Localization

    return (
        <StyledMissionCard style={{ boxShadow: tokens.elevation.raised }}>
            <TopContent>
                <StyledTitle onClick={routeChange}>
                    <Typography variant="h5" style={{ color: tokens.colors.text.static_icons__default.hex }}>
                        {mission.name}
                    </Typography>
                </StyledTitle>
                <MissionControlButtons
                    missionName={mission.name}
                    missionTaskType={missionTaskType}
                    robotId={mission.robot.id}
                    missionStatus={mission.status}
                />
            </TopContent>
            <BottomContent>
                <MissionStatusDisplayWithHeader status={mission.status} />
                <MissionProgressDisplay mission={mission} />
                <MissionRobotDisplay mission={mission} />
                <BatteryStatusDisplay
                    batteryLevel={robot?.batteryLevel}
                    batteryWarningLimit={robot?.model.batteryWarningThreshold}
                    textAlignedBottom={true}
                />
            </BottomContent>
        </StyledMissionCard>
    )
}
