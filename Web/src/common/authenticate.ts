import { IApi, IApiKey } from "./Api";
import { User } from "./Api";
import { di, diKey, singleton } from "./di";
import { IKeyVaultConfigure, IKeyVaultConfigureKey } from "./keyVault";
import Result, { isError } from "./Result";
import { IDataCrypt, IDataCryptKey } from "./DataCrypt";

export const IAuthenticateKey = diKey<IAuthenticate>();
export interface IAuthenticate {
  createUser(user: User): Promise<Result<void>>;
  login(user: User): Promise<Result<void>>;
  resetLogin(): void;
}

@singleton(IAuthenticateKey)
export class Authenticate implements IAuthenticate {
  constructor(
    private api: IApi = di(IApiKey),
    private keyVaultConfigure: IKeyVaultConfigure = di(IKeyVaultConfigureKey),
    private dataCrypt: IDataCrypt = di(IDataCryptKey)
  ) {}

  async createUser(user: User): Promise<Result<void>> {
    // Reduce risk of clear text password logging
    user.password = await this.passwordHash(user.password);

    return await this.api.createAccount(user);
  }

  async login(user: User): Promise<Result<void>> {
    user.password = await this.passwordHash(user.password);

    const tokenInfo = await this.api.login(user);
    if (isError(tokenInfo)) {
      return tokenInfo;
    }

    // derive a KEK key used when encrypting data
    const kek = await this.dataCrypt.deriveKeyEncryptionKey(user);

    this.keyVaultConfigure.setKek(kek);
    this.keyVaultConfigure.setToken(tokenInfo.token, false);
  }

  public resetLogin(): void {
    this.keyVaultConfigure.setKek(null);
    this.keyVaultConfigure.setToken(null, false);
  }

  private async passwordHash(text: string) {
    // encode as UTF-8
    const msgBuffer = new TextEncoder().encode(text);

    // hash the message
    const hashBuffer = await crypto.subtle.digest("SHA-256", msgBuffer);

    // convert ArrayBuffer to Array
    const hashArray = Array.from(new Uint8Array(hashBuffer));

    // convert bytes to hex string
    const hashHex = hashArray
      .map((b) => b.toString(16).padStart(2, "0"))
      .join("");
    return hashHex;
  }
}
