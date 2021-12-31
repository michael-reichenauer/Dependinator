import { IApi, IApiKey } from "../application/diagram/Api";
//import { store } from "../application/diagram/Store";
import { User } from "./../application/diagram/Api";
import { di, diKey, singleton } from "./di";
import Result from "./Result";

export const IAuthenticateKey = diKey<IAuthenticate>();
export interface IAuthenticate {
  createUser(user: User): Promise<Result<void>>;
  login(user: User): Promise<Result<void>>;
}

@singleton(IAuthenticateKey)
export class Authenticate implements IAuthenticate {
  constructor(private api: IApi = di(IApiKey)) {}

  async createUser(user: User): Promise<Result<void>> {
    // Reduce risk of clear text password logging
    user.password = await this.passwordHash(user.password);

    await this.api.createAccount(user);
  }

  async login(user: User): Promise<Result<void>> {
    user.password = await this.passwordHash(user.password);
    return await this.api.login(user);
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
