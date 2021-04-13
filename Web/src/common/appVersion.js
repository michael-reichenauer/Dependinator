import { useEffect, useRef } from 'react'
import Api from '../application/diagram/Api'
import { useActivity } from './activity'


const checkRemoteInterval = 30 * 60 * 1000
const retryFailedRemoteInterval = 5 * 60 * 1000


export const startTime = dateToLocalISO(new Date().toISOString())
export const localSha = process.env.REACT_APP_SHA === '%REACT_APP_SHA%' ? '000000' : process.env.REACT_APP_SHA
export const localShortSha = process.env.REACT_APP_SHA === '%REACT_APP_SHA%' ? '000000' : process.env.REACT_APP_SHA?.substring(0, 6)
export const localBuildTime = process.env.REACT_APP_BUILD_TIME === '%REACT_APP_BUILD_TIME%' ? startTime : dateToLocalISO(process.env.REACT_APP_BUILD_TIME)

console.info(`Local version:  '${localSha.substring(0, 6)}' '${localBuildTime}'`)

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
                return
            }

            try {
                // console.log(`Checking remote, active=${isActive} ...`)
                const api = new Api()
                const manifest = await api.getManifest('/manifest.json')

                const remoteSha = manifest.sha === '%REACT_APP_SHA%' ? localSha : manifest.sha
                const remoteBuildTime = manifest.buildTime === '%REACT_APP_BUILD_TIME%' ? localBuildTime : dateToLocalISO(manifest.buildTime)

                console.info(`Remote version: '${remoteSha.substring(0, 6)}' '${remoteBuildTime}'`)

                if (localSha !== remoteSha) {
                    console.info(`Local version:  '${localSha.substring(0, 6)}' '${localBuildTime}'`)
                    console.info("Remote version differs, reloading ...")
                    window.location.reload()
                }
                if (!isRunning.current) {
                    timerRef.current = setTimeout(getRemoteVersion, checkRemoteInterval)
                }
            }
            catch (err) {
                console.error("Failed get remote manifest:", err)
                if (!isRunning.current) {
                    timerRef.current = setTimeout(getRemoteVersion, retryFailedRemoteInterval)
                }
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


function dateToLocalISO(dateText) {
    const date = new Date(dateText)
    const off = date.getTimezoneOffset()
    const absoff = Math.abs(off)
    return (new Date(date.getTime() - off * 60 * 1000).toISOString().substr(0, 23) +
        (off > 0 ? '-' : '+') +
        (absoff / 60).toFixed(0).padStart(2, '0') + ':' +
        (absoff % 60).toString().padStart(2, '0'))
}

