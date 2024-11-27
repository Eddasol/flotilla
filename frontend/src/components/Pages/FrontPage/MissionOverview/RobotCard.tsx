import { Icon, Typography } from '@equinor/eds-core-react'
import { Robot, RobotStatus } from 'models/Robot'
import { tokens } from '@equinor/eds-tokens'
import { RobotStatusChip } from 'components/Displays/RobotDisplays/RobotStatusIcon'
import { BatteryStatusDisplay } from 'components/Displays/RobotDisplays/BatteryStatusDisplay'
import styled from 'styled-components'
import { RobotImage } from 'components/Displays/RobotDisplays/RobotImage'
import { useNavigate } from 'react-router-dom'
import { useLanguageContext } from 'components/Contexts/LanguageContext'
import { PressureStatusDisplay } from 'components/Displays/RobotDisplays/PressureStatusDisplay'
import { config } from 'config'
import { RobotType } from 'models/RobotModel'
import { StyledButton } from 'components/Styles/StyledComponents'
import { Icons } from 'utils/icons'

const StyledRobotPart = styled.div`
    display: flex;
    width: 446px;
    padding: 16px;
    align-items: center;
    gap: 16px;
    border-right: 1px solid ${tokens.colors.ui.background__medium.hex};
`

const HorizontalContent = styled.div`
    display: flex;
    flex-direction: row;
    align-content: start;
    align-items: start;
    justify-content: space-between;
    gap: 4px;
    padding-top: 2px;
`
const VerticalContent = styled.div`
    display: flex;
    flex-direction: column;
    justify-content: left;
    align-items: start;
    gap: 4px;
`

const LongTypography = styled(Typography)`
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    :hover {
        overflow: visible;
        white-space: normal;
        text-overflow: unset;
        word-break: break-word;
    }
`

const StyledGhostButton = styled(StyledButton)`
    padding: 0;
`

export const RobotCard = ({ robot }: { robot: Robot }) => {
    let navigate = useNavigate()
    const { TranslateText } = useLanguageContext()
    const goToRobot = () => {
        const path = `${config.FRONTEND_BASE_ROUTE}/robot/${robot.id}`
        navigate(path)
    }

    const getRobotModel = (type: RobotType) => {
        if (type === RobotType.TaurobInspector || type === RobotType.TaurobOperator) return 'Taurob'
        return type
    }

    return (
        <StyledRobotPart>
            <RobotImage robotType={robot.model.type} height="88px" />
            <VerticalContent>
                <LongTypography variant="h5">
                    {robot.name}
                    {' ('}
                    {getRobotModel(robot.model.type)}
                    {')'}
                </LongTypography>
                <HorizontalContent>
                    <VerticalContent>
                        <Typography variant="caption">{TranslateText('Status')}</Typography>
                        <RobotStatusChip
                            status={robot.status}
                            flotillaStatus={robot.flotillaStatus}
                            isarConnected={robot.isarConnected}
                        />
                    </VerticalContent>

                    {robot.status !== RobotStatus.Offline ? (
                        <>
                            <VerticalContent>
                                <Typography
                                    variant="meta"
                                    style={{ fontSize: 14, color: tokens.colors.text.static_icons__secondary.hex }}
                                >
                                    {TranslateText('Battery')}
                                </Typography>
                                <BatteryStatusDisplay
                                    batteryLevel={robot.batteryLevel}
                                    batteryWarningLimit={robot.model.batteryWarningThreshold}
                                />
                            </VerticalContent>

                            {robot.pressureLevel !== undefined && robot.pressureLevel !== null && (
                                <VerticalContent>
                                    <Typography
                                        variant="meta"
                                        style={{ fontSize: 14, color: tokens.colors.text.static_icons__secondary.hex }}
                                    >
                                        {TranslateText('Pressure')}
                                    </Typography>
                                    <PressureStatusDisplay
                                        pressure={robot.pressureLevel}
                                        upperPressureWarningThreshold={robot.model.upperPressureWarningThreshold}
                                        lowerPressureWarningThreshold={robot.model.lowerPressureWarningThreshold}
                                    />
                                </VerticalContent>
                            )}
                        </>
                    ) : (
                        <></>
                    )}
                </HorizontalContent>
                <StyledGhostButton variant="ghost" onClick={goToRobot}>
                    {TranslateText('Open robot information')}
                    <Icon name={Icons.RightCheveron} size={16} />
                </StyledGhostButton>
            </VerticalContent>
        </StyledRobotPart>
    )
}

export const RobotCardPlaceholder = () => {
    const { TranslateText } = useLanguageContext()
    return (
        <StyledRobotPart>
            <RobotImage robotType={RobotType.NoneType} height="88px" />
            <VerticalContent>
                <Typography variant="h5" color="disabled">
                    {TranslateText('No robot connected')}
                </Typography>
                <HorizontalContent>
                    <RobotStatusChip isarConnected={true} />
                </HorizontalContent>
            </VerticalContent>
        </StyledRobotPart>
    )
}