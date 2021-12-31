import { atom, useAtom } from "jotai";
import { di, diKey, singleton } from "./../common/di";
import { useRef } from "react";
import { SetAtom } from "jotai/core/types";
import { delay } from "../common/utils";
import { IAuthenticate, IAuthenticateKey } from "../common/authenticate";
import { showLoginDlg } from "./Login";
import { User } from "./diagram/Api";
import Result from "../common/Result";

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
  private currentState: SyncState = SyncState.Disabled;
  private previousState: SyncState = SyncState.Disabled;
  constructor(private authenticate: IAuthenticate = di(IAuthenticateKey)) {}

  async createAccount(user: User): Promise<Result<void>> {
    await delay(3000);
  }

  async login(user: User): Promise<Result<void>> {
    await delay(3000);

    this.setState(SyncState.Enabled);
  }

  closed(): void {
    this.restoreState();
  }

  async enableSync(): Promise<void> {
    this.setState(SyncState.Progress);
    showLoginDlg(this);
  }

  disableSync(): void {
    this.setState(SyncState.Disabled);
  }

  async retrySync(): Promise<void> {
    this.setState(SyncState.Progress);
    await delay(3000);

    this.setSyncMode?.(SyncState.Error);
  }

  setSetSyncMode(setSyncMode: SetAtom<SyncState>): void {
    this.setSyncMode = setSyncMode;
  }

  setState(state: SyncState): void {
    this.previousState = this.currentState;
    this.currentState = state;
    this.setSyncMode?.(this.currentState);
  }
  restoreState(): void {
    this.currentState = this.previousState;
    this.previousState = SyncState.Disabled;
    this.setSyncMode?.(this.currentState);
  }
}
