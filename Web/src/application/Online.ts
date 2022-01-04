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
  retrySync(): Promise<void>;
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

    if (isError(loginRsp)) {
      this.stopProgress();
      let msg = "Failed to login. Internal server error";
      if (loginRsp instanceof AuthenticateError) {
        msg =
          "Invalid credentials. Please try again with different credentials or create a new account";
      } else if (loginRsp instanceof NoContactError) {
        msg = "No network contact with server. Please retry in a while again.";
      }

      setErrorMessage(msg);
      return;
    }

    const checkRsp = await this.api.check();
    if (isError(checkRsp)) {
      this.stopProgress();
      setErrorMessage("Failed to login.  Credentials could not be verified");
      return;
    }

    this.setState(SyncState.Enabled);
    this.store.configure(true);
    setSuccessMessage("Device sync is enabled");
  }

  public closed(): void {
    this.stopProgress();
  }

  public async enableSync(): Promise<void> {
    this.startProgress();
    const checkRsp = await this.api.check();

    if (checkRsp instanceof NoContactError) {
      this.stopProgress();
      setErrorMessage(
        "No network contact with server, please retry in a while again."
      );
      return;
    }

    if (checkRsp instanceof AuthenticateError) {
      showLoginDlg(this);
      return;
    }

    console.log("rsp", checkRsp);
    if (isError(checkRsp)) {
      this.stopProgress();
      setErrorMessage("Internal server error.");
      return;
    }

    this.setState(SyncState.Enabled);
    setSuccessMessage("Device sync is enabled");
  }

  public disableSync(): void {
    this.setState(SyncState.Disabled);
    this.store.configure(false);
  }

  public async retrySync(): Promise<void> {
    this.startProgress();
    await delay(3000);

    this.store.configure(false);
    this.setSyncMode?.(SyncState.Error);
  }

  private onActivityEvent(activity: CustomEvent) {
    this.isActive = activity.detail;
    if (!this.isActive) {
      // No longer active, disable sync if enabled
      if (this.currentState !== SyncState.Disabled) {
        this.store.configure(false);
        this.startProgress();
      }

      return;
    }

    // Activated, lets enable sync if not disabled
    this.stopProgress();
    if (this.currentState !== SyncState.Disabled) {
      this.store.configure(true);
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
