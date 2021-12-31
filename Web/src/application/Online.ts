import { atom, useAtom } from "jotai";
import { di, diKey, singleton } from "./../common/di";
import { useRef } from "react";
import { SetAtom } from "jotai/core/types";
import { delay } from "../common/utils";

export enum SyncState {
  Disabled = "Disabled",
  Enabled = "Enabled",
  Error = "Error",
  Progress = "Trying",
}

const syncModeAtom = atom(SyncState.Disabled);
export const useSyncMode = (): SyncState => {
  const [syncMode, setSyncMode] = useAtom(syncModeAtom);
  const ref = useRef(di(IOnlineKey));
  ref.current.setSetSyncMode(setSyncMode);
  return syncMode;
};

export const IOnlineKey = diKey<IOnline>();
export interface IOnline {
  enableSync(): Promise<void>;
  retrySync(): Promise<void>;
  disableSync(): void;
  setSetSyncMode(setSyncMode: SetAtom<SyncState>): void;
}

@singleton(IOnlineKey)
export class Online implements IOnline {
  private setSyncMode: SetAtom<SyncState> | null = null;

  async enableSync(): Promise<void> {
    this.setSyncMode?.(SyncState.Progress);
    await delay(3000);

    this.setSyncMode?.(SyncState.Enabled);
  }

  disableSync(): void {
    this.setSyncMode?.(SyncState.Disabled);
  }

  async retrySync(): Promise<void> {
    this.setSyncMode?.(SyncState.Progress);
    await delay(3000);

    this.setSyncMode?.(SyncState.Error);
  }

  setSetSyncMode(setSyncMode: SetAtom<SyncState>): void {
    this.setSyncMode = setSyncMode;
  }
}
