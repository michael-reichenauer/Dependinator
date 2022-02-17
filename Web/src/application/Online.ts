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
import {
  clearErrorMessages,
  setErrorMessage,
  setInfoMessage,
} from "../common/MessageSnackbar";
import { setSuccessMessage } from "./../common/MessageSnackbar";
import { IStore, IStoreKey } from "./diagram/Store";
import { activityEventName } from "../common/activity";
import { ILocalStore, ILocalStoreKey } from "./../common/LocalStore";
import { orDefault } from "./../common/Result";

// Online is uses to control if device database sync should and can be enable or not
export const IOnlineKey = diKey<IOnline>();
export interface IOnline {
  enableSync(): Promise<Result<void>>;
  disableSync(): void;
}

// Current sync state to be shown e.g. in ui
export enum SyncState {
  Disabled = "Disabled", // Sync is disabled and inactive
  Enabled = "Enabled", // Sync is enabled and active and ok
  Error = "Error", // Sync is enabled, but not some error is preventing sync
  Progress = "Progress", // Progress to try to be enabled and ok, will result in either enabled or error
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

const persistentSyncKeyName = "syncState";
const deviseSyncOKMessage = "Device sync is OK";

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
    // Listen for user activate events to control if device sync should be activated or deactivated
    document.addEventListener(activityEventName, (activity: any) =>
      this.onActivityEvent(activity)
    );
    // Listen for StoreDB sync OK or error when syncing
    this.store.configure({
      onSyncChanged: (f: boolean, e?: Error) => this.onSyncChanged(f, e),
    });
  }

  // Called by LoginDlg when user wants to create an new user account
  public async createAccount(user: User): Promise<Result<void>> {
    try {
      this.showProgress();
      const createRsp = await this.authenticate.createUser(user);
      if (isError(createRsp)) {
        setErrorMessage("Failed to create account");
        return createRsp;
      }
      clearErrorMessages();
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

      // Login successful, enable device sync
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

      this.setDatabaseSync(true);

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
      this.setPersistentIsEnabled(true);
      this.isEnabled = true;
      this.isError = false;
      setSuccessMessage(deviseSyncOKMessage);
      showSyncState(SyncState.Enabled);
    } finally {
      this.hideProgress();
    }
  }

  public disableSync(): void {
    this.setPersistentIsEnabled(false);
    this.isEnabled = false;
    this.isError = false;
    this.setDatabaseSync(false);
    this.authenticate.resetLogin();
    showSyncState(SyncState.Disabled);
    clearErrorMessages();
    setInfoMessage("Device sync is disabled");
  }

  private setDatabaseSync(flag: boolean): void {
    this.store.configure({ isSyncEnabled: flag });
  }

  // Called by the StoreDB when ever sync changes to OK or to !OK with some error
  private onSyncChanged(ok: boolean, error?: Error) {
    if (!this.isEnabled) {
      // Syncing is not enabled, just reset state
      this.setDatabaseSync(false);
      this.isError = false;
      showSyncState(SyncState.Disabled);
      return;
    }

    if (!ok) {
      // StoreDB failed syncing, showing error
      this.isError = true;
      showSyncState(SyncState.Error);
      setErrorMessage(this.toErrorMessage(error));
      return;
    }

    // StoreDB now can sync OK, show Success message
    this.isError = false;
    setSuccessMessage(deviseSyncOKMessage);
    showSyncState(SyncState.Enabled);
  }

  // Called whenever user activity changes, e.g. not active or activated page
  private onActivityEvent(activity: CustomEvent) {
    const isActive = activity.detail;
    console.log(`onActivity: ${isActive}`);

    if (!isActive) {
      // User no longer active, inactivate database sync if enabled
      if (this.isEnabled) {
        this.setDatabaseSync(false);
        this.showProgress();
      }
      return;
    }

    // Activated
    this.hideProgress();

    if (this.firstActivate) {
      // First activity signal, checking if sync should be enabled automatically
      this.firstActivate = false;
      if (this.getPersistentIsEnabled()) {
        setTimeout(() => this.enableSync(), 0);
        return;
      }
    }

    if (this.isEnabled) {
      // Activate database sync again
      this.setDatabaseSync(true);
      this.store.triggerSync();
    }
  }

  private getPersistentIsEnabled() {
    return orDefault(this.localStore.tryRead(persistentSyncKeyName), false);
  }

  private setPersistentIsEnabled(state: boolean) {
    this.localStore.write(persistentSyncKeyName, state);
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

  private toErrorMessage(error?: Error): string {
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
