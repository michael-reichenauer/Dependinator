import { atom, useAtom } from "jotai";

const syncModeAtom = atom(false);
let setSyncModeFunc: any = null;

export const setSyncMode = (flag: boolean) => setSyncModeFunc?.(flag);

export const useSyncMode = () => {
  const [syncMode, setSyncMode] = useAtom(syncModeAtom);
  if (!setSyncModeFunc) {
    setSyncModeFunc = setSyncMode;
  }
  return [syncMode, setSyncMode];
};
