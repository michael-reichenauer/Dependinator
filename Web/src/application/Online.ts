import { atom, useAtom } from "jotai";
import { di, diKey, singleton } from "./../common/di";
import { useRef } from "react";
import { SetAtom } from "jotai/core/types";
import { delay } from "../common/utils";
import { IAuthenticate, IAuthenticateKey } from "../common/authenticate";
import { showLoginDlg } from "./Login";
import { IApi, User, IApiKey, NoContactError } from "../common/Api";
import Result, { isError } from "../common/Result";
import { AuthenticateError } from "./../common/Api";
import { setErrorMessage } from "../common/MessageSnackbar";
import { setSuccessMessage } from "./../common/MessageSnackbar";
import { IStore, IStoreKey } from "./diagram/Store";
import { activityEventName } from "../common/activity";

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
  disableSync(): void;

  setSetSyncMode(setSyncMode: SetAtom<SyncState>): void;
}

@singleton(IOnlineKey)
export class Online implements IOnline {
  private setSyncMode: SetAtom<SyncState> | null = null;
  private currentState: SyncState = SyncState.Disabled;
  private isActive: boolean = true;

  constructor(
    private authenticate: IAuthenticate = di(IAuthenticateKey),
    private api: IApi = di(IApiKey),
    private store: IStore = di(IStoreKey)
  ) {
    document.addEventListener(activityEventName, (activity: any) =>
      this.onActivityEvent(activity)
    );
  }

  public async createAccount(user: User): Promise<Result<void>> {
    await delay(3000);
  }

  public async login(user: User): Promise<Result<void>> {
    const loginRsp = await this.authenticate.login(user);
    this.stopProgress();

    if (isError(loginRsp)) {
      let msg = "Failed to login. Internal server error";
      if (isError(loginRsp, AuthenticateError)) {
        msg =
          "Invalid credentials. Please try again with different credentials or create a new account";
      } else if (isError(loginRsp, NoContactError)) {
        msg = "No network contact with server. Please retry in a while again.";
      }
      console.log("error msg", loginRsp, msg);
      setErrorMessage(msg);
      return;
    }

    this.enableSync();
  }

  public closed(): void {
    this.stopProgress();
  }

  public async enableSync(): Promise<void> {
    this.startProgress();
    const checkRsp = await this.api.check();

    if (isError(checkRsp, NoContactError)) {
      // No contact with server,
      this.stopProgress();
      setErrorMessage(
        "No network contact with server, please retry in a while again."
      );
      return;
    }

    if (isError(checkRsp, AuthenticateError)) {
      showLoginDlg(this);
      return;
    }

    if (isError(checkRsp)) {
      // Som unexpected error (neither contact nor authenticate error)
      this.stopProgress();
      setErrorMessage("Internal server error.");
      return;
    }

    this.store.configure({ isSyncEnabled: true });
    const syncResult = await this.store.triggerSync();
    if (isError(syncResult)) {
      this.stopProgress();
      setErrorMessage("Failed to enable sync. Internal server error.");
      this.store.configure({ isSyncEnabled: false });
      this.setState(SyncState.Disabled);
      return;
    }

    this.setState(SyncState.Enabled);
    setSuccessMessage("Device sync is OK");
  }

  public disableSync(): void {
    this.setState(SyncState.Disabled);
    this.store.configure({ isSyncEnabled: false });
  }

  private onActivityEvent(activity: CustomEvent) {
    this.isActive = activity.detail;
    if (!this.isActive) {
      // No longer active, disable sync if enabled
      if (this.currentState !== SyncState.Disabled) {
        this.store.configure({ isSyncEnabled: false });
        this.startProgress();
      }

      return;
    }

    // Activated, lets enable sync if not disabled
    this.stopProgress();
    if (this.currentState !== SyncState.Disabled) {
      this.store.configure({ isSyncEnabled: true });
      this.store.triggerSync();
    }
  }

  public setSetSyncMode(setSyncMode: SetAtom<SyncState>): void {
    this.setSyncMode = setSyncMode;
  }

  private setState(state: SyncState): void {
    this.currentState = state;
    this.setSyncMode?.(this.currentState);
  }

  private startProgress(): void {
    this.setSyncMode?.(SyncState.Progress);
  }

  private stopProgress(): void {
    this.setSyncMode?.(this.currentState);
  }
}
