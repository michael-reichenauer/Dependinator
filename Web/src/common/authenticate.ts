import { AuthenticateError, IApi, IApiKey } from "./Api";
import { User } from "./Api";
import { di, diKey, singleton } from "./di";
import { IKeyVaultConfigure, IKeyVaultConfigureKey } from "./keyVault";
import Result, { isError } from "./Result";
import { IDataCrypt, IDataCryptKey } from "./DataCrypt";

export const IAuthenticateKey = diKey<IAuthenticate>();
export interface IAuthenticate {
  check(): Promise<Result<void>>;
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

  public async check(): Promise<Result<void>> {
    if (!this.keyVaultConfigure.getDek()) {
      console.log("No DEK");
      return new AuthenticateError();
    }

    await this.api.check();
  }

  public async createUser(user: User): Promise<Result<void>> {
    // Expand/derive the password
    user.password = await this.dataCrypt.expandPassword(user);

    const wrappedDek = await this.dataCrypt.generateWrappedDataEncryptionKey(
      user
    );

    return await this.api.createAccount({ user: user, wDek: wrappedDek });
  }

  public async login(user: User): Promise<Result<void>> {
    // Expand/derive the password
    const hash = await this.dataCrypt.expandPassword(user);
    user.password = hash;

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
  }

  public resetLogin(): void {
    this.keyVaultConfigure.setDek(null);

    // Try to logoff from server ass well (but don't await result)
    this.api.logoff();
  }
}
