import { IApi, IApiKey } from "./Api";
//import { store } from "../application/diagram/Store";
import { User } from "./Api";
import { di, diKey, singleton } from "./di";
import { IKeyVault, IKeyVaultKey } from "./keyVault";
import Result, { isError } from "./Result";

export const IAuthenticateKey = diKey<IAuthenticate>();
export interface IAuthenticate {
  createUser(user: User): Promise<Result<void>>;
  login(user: User): Promise<Result<void>>;
}

@singleton(IAuthenticateKey)
export class Authenticate implements IAuthenticate {
  constructor(
    private api: IApi = di(IApiKey),
    private keyVault: IKeyVault = di(IKeyVaultKey)
  ) {}

  async createUser(user: User): Promise<Result<void>> {
    // Reduce risk of clear text password logging
    user.password = await this.passwordHash(user.password);

    await this.api.createAccount(user);
  }

  async login(user: User): Promise<Result<void>> {
    user.password = await this.passwordHash(user.password);

    const tokenInfo = await this.api.login(user);
    if (isError(tokenInfo)) {
      return tokenInfo;
    }

    this.keyVault.setToken(tokenInfo.token);
  }

  async passwordHash(text: string) {
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
