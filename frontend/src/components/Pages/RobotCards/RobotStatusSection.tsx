import styled from 'styled-components'
import { RobotStatusCard, RobotStatusCardPlaceholder } from './RobotStatusCard'
import { useInstallationContext } from 'components/Contexts/InstallationContext'
import { useRobotContext } from 'components/Contexts/RobotContext'

const RobotCardSection = styled.div`
    display: flex;
    flex-wrap: wrap;
    gap: 2rem;
`
const RobotView = styled.div`
    display: grid;
    grid-column: 1/ -1;
    gap: 1rem;
`

export const RobotStatusSection = () => {
    const { installationCode } = useInstallationContext()
    const { enabledRobots } = useRobotContext()

    const relevantRobots = enabledRobots
        .filter(
            (robot) =>
                robot.currentInstallation.installationCode.toLocaleLowerCase() === installationCode.toLocaleLowerCase()
        )
        .sort((robot, robotToCompareWith) =>
            robot.status! !== robotToCompareWith.status!
                ? robot.status! > robotToCompareWith.status!
                    ? 1
                    : -1
                : robot.name! === robotToCompareWith.name!
                  ? 0
                  : robot.name! > robotToCompareWith.name!
                    ? 1
                    : -1
        )

    const robotDisplay = relevantRobots.map((robot) => <RobotStatusCard key={robot.id} robot={robot} />)

    return (
        <RobotView>
            <RobotCardSection>
                {relevantRobots.length > 0 && robotDisplay}
                {relevantRobots.length === 0 && <RobotStatusCardPlaceholder />}
            </RobotCardSection>
        </RobotView>
    )
}
