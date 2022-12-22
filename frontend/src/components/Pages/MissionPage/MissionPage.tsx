import { Button, Icon, Typography } from '@equinor/eds-core-react'
import { arrow_back } from '@equinor/eds-icons'
import { useApi } from 'api/ApiCaller'
import { TaskTable } from 'components/Pages/MissionPage/TaskOverview/TaskTable'
import { MapPositionView } from 'components/Pages/MissionPage/MapPosition/MapPositionView'
import { VideoStreamWindow } from 'components/Pages/MissionPage/VideoStream/VideoStreamWindow'
import { Mission } from 'models/Mission'
import { VideoStream } from 'models/VideoStream'
import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import styled from 'styled-components'
import { MissionControlButtons } from 'components/Pages/FrontPage/MissionOverview/MissionControlButtons'
import { MissionHeader } from './MissionHeader/MissionHeader'


const TaskAndMapSection = styled.div`
    display: flex;
    flex-wrap: wrap;
    gap: 3rem;
    padding-top: 16px;
    padding-bottom: 16px;
`
const VideoStreamSection = styled.div`
    display: grid;
    gap: 1rem;
`

Icon.add({ arrow_back })

export function MissionPage() {
    const { missionId } = useParams()
    const apiCaller = useApi()
    const [videoStreams, setVideoStreams] = useState<VideoStream[]>([])
    const [selectedMission, setSelectedMission] = useState<Mission>()

    useEffect(() => {
        if (missionId) {
            apiCaller.getMissionById(missionId).then((mission) => {
                setSelectedMission(mission)
                updateVideoStreams(mission)
            })
        }
    }, [])

    useEffect(() => {
        const timeDelay = 1000
        const id = setInterval(() => {
            if (missionId) {
                apiCaller.getMissionById(missionId).then((mission) => {
                    setSelectedMission(mission)
                })
            }
        }, timeDelay)
        return () => clearInterval(id)
    }, [])

    const updateVideoStreams = (mission: Mission) => {
        apiCaller.getVideoStreamsByRobotId(mission.robot.id).then((streams) => {
            setVideoStreams(streams)
        })
    }

    var videoDisplay = videoStreams.map(function (videoStream, index) {
        return <VideoStreamWindow key={index} videoStream={videoStream} />
    })

    return (
        <>
            {
                selectedMission !== undefined &&
                <>
                    <Button variant="ghost" href="..">
                        <Icon name="arrow_back" size={32} />Back
                    </Button>
                    <MissionHeader mission={selectedMission}/>
                    <TaskAndMapSection>
                        <TaskTable mission={selectedMission} />
                        <MapPositionView mission={selectedMission} />
                    </TaskAndMapSection>
                    <VideoStreamSection>
                        {
                            videoStreams.length > 0 &&
                            <>
                                <Typography variant='h2'>Camera</Typography>
                                {videoDisplay}
                            </>
                        }
                    </VideoStreamSection>
                </>
            }

        </>
    )
}
