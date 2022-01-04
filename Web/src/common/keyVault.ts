import { diKey, singleton } from "./di";

export const IKeyVaultKey = diKey<IKeyVault>();
export interface IKeyVault {
  getToken(): string | null;
  setToken(token: string | null): void;
}

@singleton(IKeyVaultKey)
export class KeyVault {
  kek: any = null;
  token: string | null = null;

  public getToken(): string | null {
    return this.token;
  }

  public setToken(token: string | null): void {
    this.token = token;
  }

  getKek(): any {
    return this.kek;
  }

  storeKek(kek: any): void {
    this.kek = kek;
  }
}
