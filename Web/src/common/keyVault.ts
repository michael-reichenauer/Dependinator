import { di, diKey, singleton } from "./di";
import { ILocalStore, ILocalStoreKey } from "./LocalStore";
import { orDefault } from "./Result";

export const IKeyVaultKey = diKey<IKeyVault>();
export interface IKeyVault {
  getToken(): string | null;
  getKek(): any;
}

export const IKeyVaultConfigureKey = diKey<IKeyVaultConfigure>();
export interface IKeyVaultConfigure extends IKeyVault {
  setToken(token: string | null, persist: boolean): void;
  setKek(kek: any): void;
}

const tokenKey = "token";

@singleton(IKeyVaultKey, IKeyVaultConfigureKey)
export class KeyVault implements IKeyVault, IKeyVaultConfigure {
  private kek: any = null;
  private token: string | null = null;

  constructor(private localStore: ILocalStore = di(ILocalStoreKey)) {
    this.token = orDefault(this.localStore.tryRead(tokenKey), null);
  }

  public getToken(): string | null {
    return this.token;
  }

  public setToken(token: string | null, persist: boolean): void {
    this.token = token;

    if (persist && this.token) {
      this.localStore.write(tokenKey, this.token);
    } else {
      this.localStore.remove(tokenKey);
    }
  }

  getKek(): any {
    return this.kek;
  }

  setKek(kek: any): void {
    this.kek = kek;
  }
}
