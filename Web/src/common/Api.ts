import axios from "axios";
import timing from "./timing";

import Result, { isError } from "./Result";
import { diKey, singleton } from "./di";
import { CustomError } from "./CustomError";

export interface User {
  username: string;
  password: string;
}

export interface CreateUserReq {
  user: User;
  wDek: string;
}

export interface LoginRsp {
  token: string;
  wDek: string;
}

export type ApiEntityStatus = "value" | "noValue" | "notModified" | "error";

export interface ApiEntity {
  key: string;
  etag?: string;
  // stamp: string;
  status?: ApiEntityStatus;
  value?: any;
}

export interface ApiEntityRsp {
  key: string;
  status?: string;
  etag?: string;
}

export interface Query {
  key: string;
  IfNoneMatch?: string;
}

export class NetworkError extends CustomError {}
export class AuthenticateError extends NetworkError {}
export class CredentialError extends AuthenticateError {}
export class TokenError extends AuthenticateError {}
export class NoContactError extends NetworkError {}
export class RequestError extends NetworkError {}

export const IApiKey = diKey<IApi>();
export interface IApi {
  config(onOK: () => void, onError: (error: Error) => void): void;
  login(user: User): Promise<Result<LoginRsp>>;
  logoff(): Promise<Result<void>>;
  createAccount(createUser: CreateUserReq): Promise<Result<void>>;
  check(): Promise<Result<void>>;
  tryReadBatch(queries: Query[]): Promise<Result<ApiEntity[]>>;
  writeBatch(entities: ApiEntity[]): Promise<Result<ApiEntityRsp[]>>;
  removeBatch(keys: string[]): Promise<Result<void>>;
}

@singleton(IApiKey)
export class Api implements IApi {
  private apiKey = "0624bc00-fcf7-4f31-8f3e-3bdc3eba7ade"; // Must be same as in server side api

  private requestCount = 0;
  private onOK: () => void = () => {};
  private onError: (error: Error) => void = () => {};

  config(onOK: () => void, onError: (error: Error) => void): void {
    this.onOK = onOK;
    this.onError = onError;
  }

  public async login(user: User): Promise<Result<LoginRsp>> {
    const rsp = await this.post("/api/Login", user);
    if (isError(rsp)) {
      return rsp;
    }
    return rsp as LoginRsp;
  }

  public async logoff(): Promise<Result<void>> {
    const rsp = await this.post("/api/Logoff", null);
    if (isError(rsp)) {
      return rsp;
    }
  }

  public async createAccount(createUser: CreateUserReq): Promise<Result<void>> {
    return await this.post("/api/CreateUser", createUser);
  }

  public async check(): Promise<Result<void>> {
    return await this.get("/api/Check");
  }

  public async tryReadBatch(queries: Query[]): Promise<Result<ApiEntity[]>> {
    const rsp = await this.post("/api/tryReadBatch", queries);
    if (isError(rsp)) {
      return rsp;
    }
    return rsp as ApiEntity[];
  }

  public async writeBatch(
    entities: ApiEntity[]
  ): Promise<Result<ApiEntityRsp[]>> {
    return await this.post("/api/writeBatch", entities);
  }

  public async removeBatch(keys: string[]): Promise<Result<void>> {
    return await this.post("/api/removeBatch", keys);
  }

  // api helper functions ---------------------------------
  private async get(uri: string): Promise<Result<any>> {
    this.requestCount++;
    // console.log(`Request #${this.requestCount}: GET ${uri} ...`);
    const t = timing();
    try {
      const rsp = await axios.get(uri, {
        headers: { "x-api-key": this.apiKey },
      });

      const rspData = rsp.data;
      const rspBytes = ("" + rsp.request?.responseText).length;
      console.groupCollapsed(
        `Request #${this.requestCount}: GET ${uri}: OK: (0->${rspBytes} bytes)`,
        t()
      );
      console.log("Response", rspData);
      console.log("#rsp", rsp);
      console.groupEnd();
      this.onOK();
      return rspData;
    } catch (e) {
      const error = this.toError(e);
      console.groupCollapsed(
        `%cRequest #${this.requestCount}: GET ${uri}: ERROR: ${error.name}: ${error.message}`,
        "color: #CD5C5C",
        t()
      );
      console.log("%cError:", "color: #CD5C5C", error);
      console.groupEnd();
      this.onError(error);
      return error;
    }
  }

  async post(uri: string, requestData: any): Promise<Result<any>> {
    this.requestCount++;
    // console.log(`Request #${this.requestCount}: POST ${uri} ...`);
    const t = timing();
    try {
      const rsp = await axios.post(uri, requestData, {
        headers: { "x-api-key": this.apiKey },
      });
      const rspData = rsp.data;
      const reqBytes = ("" + rsp.config.data).length;
      const rspBytes = ("" + rsp.request?.responseText).length;
      console.groupCollapsed(
        `Request #${this.requestCount}: POST ${uri}: OK: (${reqBytes}->${rspBytes} bytes)`,
        t()
      );
      console.log("Request:", requestData);
      console.log("Response:", rspData);
      console.groupEnd();
      this.onOK();
      return rspData;
    } catch (e) {
      const error = this.toError(e);
      console.groupCollapsed(
        `%cRequest #${this.requestCount}: POST ${uri}: ERROR: ${error.name}: ${error.message}`,
        "color: #CD5C5C",
        t()
      );
      console.log("Request:", requestData);
      console.log("%cError:", "color: #CD5C5C", error);
      console.groupEnd();
      this.onError(error);
      return error;
    }
  }

  toError(rspError: any) {
    if (rspError.response) {
      // Request made and server responded
      //console.log("Failed:", rspError.response);
      const rsp = rspError.response;
      const axiosError = new NetworkError(
        `Status: ${rsp.status} '${rsp.statusText}': ${rsp.data}`
      );

      if (rsp.status === 500 && rsp.data?.includes("(ECONNREFUSED)")) {
        return new NoContactError(
          "Local api server not started, Start local Azure functions server",
          axiosError
        );
      } else if (rsp.status === 400) {
        if (rsp.data?.includes("ECONNREFUSED 127.0.0.1:10002")) {
          return new RequestError(
            "Local storage emulator not started. Call 'AzureStorageEmulator.exe start'",
            axiosError
          );
        }
        if (
          rsp.data?.includes("The table specified does not exist") ||
          rsp.data?.includes("Invalid token") ||
          rsp.data?.includes("Invalid user") ||
          rsp.data?.includes("AuthenticateError")
        ) {
          return new AuthenticateError(axiosError);
        }
      }

      return new RequestError("Invalid or unsupported request", axiosError);
    } else if (rspError.request) {
      // The request was made but no response was received
      return new NoContactError(rspError);
    }

    // Something happened in setting up the request that triggered an Error
    return new NetworkError(
      "Failed to send request. Request setup error",
      rspError
    );
  }
}
