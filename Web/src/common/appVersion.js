import { useEffect, useRef } from 'react'
import axios from 'axios';
import { useActivity } from './activity'
// import { atom, useAtom } from 'jotai';

const checkRemoteInterval = 30 * 60 * 1000
const retryFailedRemoteInterval = 5 * 60 * 1000

// const appVersionAtom = atom({ sha: '', shortSha: '', buildTime: '' })

// export const useAppVersion = () => useAtom(appVersionAtom)

export const startTime = dateToLocalISO(new Date().toISOString())
export const localSha = process.env.REACT_APP_SHA === '%REACT_APP_SHA%' ? '000000' : process.env.REACT_APP_SHA
export const localShortSha = process.env.REACT_APP_SHA === '%REACT_APP_SHA%' ? '000000' : process.env.REACT_APP_SHA?.substring(0, 6)
export const localBuildTime = process.env.REACT_APP_BUILD_TIME === '%REACT_APP_BUILD_TIME%' ? startTime : process.env.REACT_APP_BUILD_TIME


export const useAppVersionMonitor = () => {
    const [isActive] = useActivity()
    const timerRef = useRef();
    const isRunning = useRef(false)

    useEffect(() => {
        clearTimeout(timerRef.current)
        const getRemoteVersion = async () => {
            if (!isActive || !isRunning.current) {
                isRunning.current = false
                clearTimeout(timerRef.current)
                console.log('Disable remote version check')
                return
            }
            // const handleError = () => {
            //     enqueueSnackbar('Failed to access server to get version',
            //         {
            //             variant: "error",
            //             onClick: () => closeSnackbar(),
            //             autoHideDuration: null
            //         })
            // }

            try {
                // console.log(`getting manifest ...`)
                console.log(`Checking remote, active=${isActive} ...`)
                const data = await axios.get('/manifest.json')
                const manifest = data.data
                // console.log(`Got remote manifest`, manifest)
                const remoteSha = manifest.sha === '%REACT_APP_SHA%' ? '000000' : manifest.sha
                const remoteBuildTime = manifest.buildTime === '%REACT_APP_BUILD_TIME%' ? '' : manifest.buildTime

                console.info(`Local version:  '${localSha.substring(0, 6)}' '${remoteBuildTime}'`)
                console.info(`Remote version: '${remoteSha.substring(0, 6)}' '${remoteBuildTime}'`)
                // setRemoteVersion({ sha: remoteSha, buildTime: remoteBuildTime })

                if (localSha !== remoteSha) {
                    console.info(`Local version: '${localSha.substring(0, 6)}' '${localBuildTime}'`)
                    console.info("Remote version differs, reloading ...")
                    //logger.flush().then(() => window.location.reload(true))
                }
                if (!isRunning.current) {
                    timerRef.current = setTimeout(getRemoteVersion, checkRemoteInterval)
                }

            }
            catch (err) {
                console.error("Failed get remote manifest:", err)
                // handleError()
                //networkError(err)
                if (!isRunning.current) {
                    timerRef.current = setTimeout(getRemoteVersion, retryFailedRemoteInterval)
                }
            }
            finally {
                // setIsLoading(false)
            }
        }
        isRunning.current = true
        getRemoteVersion()

        return () => {
            isRunning.current = false
            clearTimeout(timerRef.current)
        }
    }, [isActive, timerRef, isRunning])
}


// export const useAppVersion = () => {
//     const [remoteVersion] = useGlobal('remoteVersion')
//     return { localSha: localSha, localBuildTime: localBuildTime, remoteSha: remoteVersion.sha, remoteBuildTime: remoteVersion.buildTime }
// }


function dateToLocalISO(dateText) {
    const date = new Date(dateText)
    const off = date.getTimezoneOffset()
    const absoff = Math.abs(off)
    return (new Date(date.getTime() - off * 60 * 1000).toISOString().substr(0, 23) +
        (off > 0 ? '-' : '+') +
        (absoff / 60).toFixed(0).padStart(2, '0') + ':' +
        (absoff % 60).toString().padStart(2, '0'))
}

