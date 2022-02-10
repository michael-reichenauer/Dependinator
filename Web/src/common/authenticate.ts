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

    const wrappedDek = await this.dataCrypt.generateWrappedDataEncryptionKey(
      user
    );

    return await this.api.createAccount({ user: user, wDek: wrappedDek });
  }

  async login(user: User): Promise<Result<void>> {
    user.password = await this.passwordHash(user.password);

    const loginRsp = await this.api.login(user);
    if (isError(loginRsp)) {
      return loginRsp;
    }

    // Extract the data encryption key DEK from the wrapped/encrypted wDek
    const dek = await this.dataCrypt.unwrapDataEncryptionKey(
      loginRsp.wDek,
      user
    );

    this.keyVaultConfigure.setDek(dek);
    this.keyVaultConfigure.setToken(loginRsp.token, false);
  }

  public resetLogin(): void {
    this.keyVaultConfigure.setDek(null);
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
