import { diKey, singleton } from "./di";

export const IKeyVaultKey = diKey<IKeyVault>();
export interface IKeyVault {
  getDek(): any;
}

export const IKeyVaultConfigureKey = diKey<IKeyVaultConfigure>();
export interface IKeyVaultConfigure extends IKeyVault {
  setDek(dek: any): void;
}

@singleton(IKeyVaultKey, IKeyVaultConfigureKey)
export class KeyVault implements IKeyVault, IKeyVaultConfigure {
  private dek: any = null;

  getDek(): any {
    return this.dek;
  }

  setDek(dek: any): void {
    this.dek = dek;
  }
}
