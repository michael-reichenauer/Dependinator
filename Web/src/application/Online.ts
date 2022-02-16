import { atom, useAtom } from "jotai";
import { di, diKey, singleton } from "./../common/di";
import { SetAtom } from "jotai/core/types";
import { IAuthenticate, IAuthenticateKey } from "../common/authenticate";
import { ILoginProvider, showLoginDlg } from "./LoginDlg";
import {
  IApi,
  User,
  IApiKey,
  NoContactError,
  LocalApiServerError,
  LocalEmulatorError,
} from "../common/Api";
import Result, { isError } from "../common/Result";
import { AuthenticateError } from "./../common/Api";
import { setErrorMessage } from "../common/MessageSnackbar";
import { setSuccessMessage } from "./../common/MessageSnackbar";
import { IStore, IStoreKey } from "./diagram/Store";
import { activityEventName } from "../common/activity";
import { ILocalStore, ILocalStoreKey } from "./../common/LocalStore";
import { orDefault } from "./../common/Result";

export const IOnlineKey = diKey<IOnline>();
export interface IOnline {
  enableSync(): Promise<Result<void>>;
  disableSync(): void;
}

export enum SyncState {
  Disabled = "Disabled", // Sync is disabled and inactive
  Enabled = "Enabled", // Sync is enabled and active and ok
  Error = "Error", // Sync is enabled, but not ok
  Progress = "Progress", // Sync is in progress to try to be enabled and ok
}

// useSyncMode is used by ui read and be notified of current sync state
const syncModeAtom = atom(SyncState.Disabled);
let showSyncState: SetAtom<SyncState> = () => {};
export const useSyncMode = (): SyncState => {
  const [syncMode, setSyncModeFunc] = useAtom(syncModeAtom);
  // Ensure that the Online service can set SyncState, so set the setSyncMode function
  showSyncState = setSyncModeFunc;
  return syncMode;
};

// enum CurrentState {
//   Disabled = "Disabled", // Sync is disabled
//   Enabled = "Enabled", // Sync is enabled, active and ok
//   // Inactive = "Inactive", // Sync is enabled, but not active (no user activity for a while)
//   Error = "Error", // Sync is enabled, but some error

//   // Activating = "Activating", // In progress to try to be enabled from Inactive
//   Enabling = "Enabling", // In progress to try to be enabled from Disabled
//   Checking = "Checking", // In progress to try to check sync from Enabled
//   Reenabling = "Reenabling", // In progress to try to be enabled from Error
// }

@singleton(IOnlineKey)
export class Online implements IOnline, ILoginProvider {
  // private currentState: CurrentState = CurrentState.Disabled;
  private isEnabled = false;
  private isError = false;

  private firstActivate = true;

  constructor(
    private authenticate: IAuthenticate = di(IAuthenticateKey),
    private api: IApi = di(IApiKey),
    private store: IStore = di(IStoreKey),
    private localStore: ILocalStore = di(ILocalStoreKey)
  ) {
    document.addEventListener(activityEventName, (activity: any) =>
      this.onActivityEvent(activity)
    );
  }

  public async createAccount(user: User): Promise<Result<void>> {
    try {
      this.showProgress();
      const createRsp = await this.authenticate.createUser(user);
      if (isError(createRsp)) {
        setErrorMessage("Failed to create account");
        return createRsp;
      }
    } finally {
      this.hideProgress();
    }
  }

  public async login(user: User): Promise<Result<void>> {
    try {
      this.showProgress();
      const loginRsp = await this.authenticate.login(user);

      if (isError(loginRsp)) {
        setErrorMessage(this.toErrorMessage(loginRsp));
        return loginRsp;
      }

      return await this.enableSync();
    } finally {
      this.hideProgress();
    }
  }

  public async enableSync(): Promise<Result<void>> {
    try {
      this.showProgress();
      const checkRsp = await this.authenticate.check();

      if (checkRsp instanceof NoContactError) {
        this.isError = true;
        // No contact with server, cannot enable sync
        setErrorMessage(this.toErrorMessage(checkRsp));
        showSyncState(SyncState.Error);
        return checkRsp;
      }

      if (checkRsp instanceof AuthenticateError) {
        // Authentication is needed, showing the login dialog
        showLoginDlg(this);
        return checkRsp;
      }

      if (isError(checkRsp)) {
        // Som unexpected error (neither contact nor authenticate error)
        this.isError = true;
        setErrorMessage(this.toErrorMessage(checkRsp));
        showSyncState(SyncState.Error);
        return checkRsp;
      }

      this.store.configure({
        isSyncEnabled: true,
        onSyncChanged: (f: boolean) => this.onSyncChanged(f),
      });

      const syncResult = await this.store.triggerSync();
      if (isError(syncResult)) {
        // This should be very unlikely
        setErrorMessage(this.toErrorMessage(syncResult));
        this.authenticate.resetLogin();
        this.store.configure({ isSyncEnabled: false });
        this.isError = true;
        showSyncState(SyncState.Error);
        return syncResult;
      }

      // Device sync successfully enabled
      this.setTargetState(true);
      this.isEnabled = true;
      setSuccessMessage("Device sync is OK");
      showSyncState(SyncState.Enabled);
    } finally {
      this.hideProgress();
    }
  }

  public disableSync(): void {
    this.setTargetState(false);
    this.isEnabled = false;
    this.isError = false;
    this.store.configure({ isSyncEnabled: false });
    this.authenticate.resetLogin();
    showSyncState(SyncState.Disabled);
  }

  private onSyncChanged(ok: boolean) {
    console.log("Sync change", ok);
    if (!ok) {
      this.isError = true;
      showSyncState(SyncState.Error);
      setErrorMessage("Syncing failed");
    } else if (this.isEnabled) {
      this.isError = false;
      setSuccessMessage("Device sync is OK");
      showSyncState(SyncState.Enabled);
    } else {
      this.isError = false;
      showSyncState(SyncState.Disabled);
    }
  }

  private onActivityEvent(activity: CustomEvent) {
    const isActive = activity.detail;
    console.log(`onActivity: ${isActive}`);

    if (!isActive) {
      // User no longer active, inactivate sync if enabled
      if (this.isEnabled) {
        this.store.configure({ isSyncEnabled: false });
        this.showProgress();
      }
      return;
    }

    // Activated
    this.hideProgress();

    if (this.firstActivate) {
      // First activity signal, checking if sync should be enabled automatically
      this.firstActivate = false;
      if (this.getTargetState()) {
        setTimeout(() => this.enableSync(), 0);
        return;
      }
    }

    if (this.isEnabled) {
      this.store.configure({ isSyncEnabled: true });
      this.store.triggerSync();
    }
  }

  private getTargetState() {
    return orDefault(this.localStore.tryRead("syncState"), false);
  }
  private setTargetState(state: boolean) {
    this.localStore.write("syncState", state);
  }

  private showProgress(): void {
    showSyncState(SyncState.Progress);
  }

  private hideProgress(): void {
    if (!this.isEnabled) {
      showSyncState(SyncState.Disabled);
    } else if (this.isError) {
      showSyncState(SyncState.Error);
    } else {
      showSyncState(SyncState.Enabled);
    }
  }

  private toErrorMessage(error: Error): string {
    if (isError(error, LocalApiServerError)) {
      return "Local Azure functions api server is not started.";
    }
    if (isError(error, LocalEmulatorError)) {
      return "Local Azure storage emulator not started.";
    }
    if (isError(error, AuthenticateError)) {
      return "Invalid credentials. Please try again with different credentials or create a new account";
    }
    if (isError(error, NoContactError)) {
      return "No network contact with server. Please retry again in a while.";
    }

    return "Internal server error";
  }
}
