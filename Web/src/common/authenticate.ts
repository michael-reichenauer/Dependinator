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

const minUserName = 2;
const minPassword = 4;

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

    return await this.api.check();
  }

  public async createUser(enteredUser: User): Promise<Result<void>> {
    const user = await this.hashAndExpandUser(enteredUser);
    if (isError(user)) {
      return user;
    }

    const wrappedDek = await this.dataCrypt.generateWrappedDataEncryptionKey(
      user
    );

    return await this.api.createAccount({ user: user, wDek: wrappedDek });
  }

  public async login(enteredUser: User): Promise<Result<void>> {
    const user = await this.hashAndExpandUser(enteredUser);
    if (isError(user)) {
      return user;
    }

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

  private async hashAndExpandUser(enteredUser: User): Promise<Result<User>> {
    let { username, password } = enteredUser;
    console.log(`org '${username}', '${password}'`);

    if (
      !username ||
      !password ||
      username.length < minUserName ||
      password.length < minPassword
    ) {
      return new AuthenticateError();
    }

    // Normalize username and password
    username = username.trim().toLowerCase();
    password = password.trim();

    console.log(`norm '${username}', '${password}'`);

    // Hash username to ensure original username is hidden from server
    username = await this.toSha256(username);

    // Expand/derive the password to ensure that password is hard to crack using brute force
    password = await this.dataCrypt.expandPassword({
      username: username,
      password: password,
    });

    console.log(`hash '${username}', '${password}'`);

    return { username: username, password: password };
  }

  private async toSha256(text: string): Promise<string> {
    const msgBuffer = new TextEncoder().encode(text);
    const hashBuffer = await crypto.subtle.digest("SHA-256", msgBuffer);
    const hashArray = Array.from(new Uint8Array(hashBuffer));

    // convert bytes to hex string
    const hashHex = hashArray
      .map((b) => b.toString(16).padStart(2, "0"))
      .join("");
    return hashHex;
  }
}
