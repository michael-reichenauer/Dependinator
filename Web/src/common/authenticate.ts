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
    const wrappedDek = await this.dataCrypt.generateWrappedDataEncryptionKey(
      user
    );

    // Expand/derive the password
    user.password = await this.dataCrypt.expandPassword(user);
    return await this.api.createAccount({ user: user, wDek: wrappedDek });
  }

  async login(user: User): Promise<Result<void>> {
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
    this.keyVaultConfigure.setToken(loginRsp.token, false);
  }

  public resetLogin(): void {
    this.keyVaultConfigure.setDek(null);
    this.keyVaultConfigure.setToken(null, false);
  }
}
