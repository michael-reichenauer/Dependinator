import { atom, useAtom } from "jotai"

const syncModeAtom = atom(false)
let setSyncModeFunc = null

export const setSyncMode = flag => setSyncModeFunc?.(flag)

export const useSyncMode = () => {
    const [syncMode, setSyncMode] = useAtom(syncModeAtom)
    if (!setSyncModeFunc) {
        setSyncModeFunc = setSyncMode
    }
    return [syncMode, setSyncMode]
}

