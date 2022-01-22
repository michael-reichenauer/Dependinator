import axios from "axios";
import timing from "./timing";

import { IKeyVault, IKeyVaultKey } from "./keyVault";
import Result, { isError } from "./Result";
import { di, diKey, singleton } from "./di";
import { CustomError } from "./CustomError";

export interface User {
  username: string;
  password: string;
}

export interface TokenInfo {
  token: string;
}

export type ApiEntityStatus = "value" | "noValue" | "notModified";

export interface ApiEntity {
  key: string;
  stamp: string;
  status?: ApiEntityStatus;
  value?: any;
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
  login(user: User): Promise<Result<TokenInfo>>;
  createAccount(user: User): Promise<Result<void>>;
  check(): Promise<Result<void>>;
  tryReadBatch(queries: Query[]): Promise<Result<ApiEntity[]>>;
  writeBatch(entities: ApiEntity[]): Promise<Result<void>>;
  removeBatch(keys: string[]): Promise<Result<void>>;
}

@singleton(IApiKey)
export class Api implements IApi {
  private apiKey = "0624bc00-fcf7-4f31-8f3e-3bdc3eba7ade"; // Must be same as in server side api

  private requestCount = 0;
  private onOK: () => void = () => {};
  private onError: (error: Error) => void = () => {};

  constructor(private keyVault: IKeyVault = di(IKeyVaultKey)) {}

  config(onOK: () => void, onError: (error: Error) => void): void {
    this.onOK = onOK;
    this.onError = onError;
  }

  public async login(user: User): Promise<Result<TokenInfo>> {
    const rsp = await this.post("/api/ConnectUser", user);
    if (isError(rsp)) {
      return rsp;
    }
    return rsp as TokenInfo;
  }

  public async createAccount(user: User): Promise<Result<void>> {
    throw new Error("Method not implemented.");
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

  public async writeBatch(entities: ApiEntity[]): Promise<Result<void>> {
    return await this.post("/api/writeBatch", entities);
  }

  public async removeBatch(keys: string[]): Promise<Result<void>> {
    return await this.post("/api/removeBatch", keys);
  }

  private getToken() {
    return this.keyVault.getToken();
  }

  // api helper functions ---------------------------------
  private async get(uri: string): Promise<Result<any>> {
    this.requestCount++;
    console.log(`Request #${this.requestCount}: GET ${uri} ...`);
    const t = timing();
    try {
      const rspData = (
        await axios.get(uri, {
          headers: { "x-api-key": this.apiKey, xtoken: this.getToken() },
        })
      ).data;

      console.groupCollapsed(
        `Request #${this.requestCount}: GET ${uri}: OK:`,
        t()
      );
      console.log("Response", rspData);
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
    console.log(`Request #${this.requestCount}: POST ${uri} ...`);
    const t = timing();
    try {
      const rspData = (
        await axios.post(uri, requestData, {
          headers: { "x-api-key": this.apiKey, xtoken: this.getToken() },
        })
      ).data;
      console.groupCollapsed(
        `Request #${this.requestCount}: POST ${uri}: OK:`,
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
          rsp.data?.includes("Invalid user")
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

// class ApiOld {
//   apiKey = "0624bc00-fcf7-4f31-8f3e-3bdc3eba7ade"; // Must be same as in server side api
//   onInvalidToken: () => void;
//   getToken = () => keyVault.getToken();
//   setToken = (token: string) => keyVault.setToken(token);

//   constructor(onInvalidToken: () => void) {
//     this.onInvalidToken = onInvalidToken;
//   }

//   // async getCurrentUser() {
//   //     console.log('host', window.location.hostname)
//   //     if (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1') {
//   //         await this.get('/api/Check')
//   //         return {
//   //             clientPrincipal: {
//   //                 "identityProvider": "Local",
//   //                 "userId": 'local',
//   //                 "userDetails": 'local',
//   //                 "userRoles": ["anonymous", "authenticated"]
//   //             }
//   //         }
//   //     }

//   //     return await this.get('/.auth/me')
//   // }

//   async getManifest() {
//     return this.get("/manifest.json");
//   }

//   async check() {
//     return this.get("/api/Check");
//   }

//   // async clearAllData() {
//   //     return this.post('/api/ClearAllData')
//   // }

//   async createUser(user: User) {
//     return this.post("/api/CreateUser", user);
//   }

//   async connectUser(user: User) {
//     return this.post("/api/ConnectUser", user);
//   }

//   async connect() {
//     return this.get("/api/Connect");
//   }

//   async getAllDiagramsData() {
//     return this.get(`/api/GetAllDiagramsData`);
//   }

//   async newDiagram(diagram: Data) {
//     return this.post("/api/NewDiagram", diagram);
//   }

//   async setCanvas(canvas: Data) {
//     return this.post("/api/SetCanvas", canvas);
//   }

//   async getDiagram(diagramId: string) {
//     return this.get(`/api/GetDiagram?diagramId=${diagramId}`);
//   }

//   async deleteDiagram(diagramId: string) {
//     return this.post(`/api/DeleteDiagram`, { diagramId: diagramId });
//   }

//   async updateDiagram(diagram: Data) {
//     return this.post(`/api/UpdateDiagram`, diagram);
//   }

//   async uploadDiagrams(diagrams: Data) {
//     return this.post(`/api/UploadDiagrams`, diagrams);
//   }

//   async downloadAllDiagrams() {
//     return this.get(`/api/DownloadAllDiagrams`);
//   }

//   // api helper functions ---------------------------------
//   async get(uri: string): Promise<any> {
//     this.handleRequest("get", uri);
//     const t = timing();
//     try {
//       const rsp = (
//         await axios.get(uri, {
//           headers: { "x-api-key": this.apiKey, xtoken: this.getToken() },
//         })
//       ).data;
//       this.handleOK("get", uri, null, rsp);
//       return rsp;
//     } catch (error) {
//       this.handleError("get", error, uri);
//       throw error;
//     } finally {
//       t.log("get", uri);
//     }
//   }

//   async post(uri: string, data: Data): Promise<any> {
//     this.handleRequest("post", uri, data);
//     const t = timing();
//     try {
//       const rsp = (
//         await axios.post(uri, data, {
//           headers: { "x-api-key": this.apiKey, xtoken: this.getToken() },
//         })
//       ).data;
//       this.handleOK("post", uri, data, rsp);
//       return rsp;
//     } catch (error) {
//       this.handleError("post", error, uri, data);
//       throw error;
//     } finally {
//       t.log("post", uri);
//     }
//   }

//   handleRequest(method: string, uri: string, postData?: Data) {
//     console.log("Request:", method, uri, postData);
//   }

//   handleOK(
//     method: string,
//     uri: string,
//     postData: any,
//     rsp: AxiosResponse<any>
//   ) {
//     if (isConnectionOK !== true) {
//       console.log("Connection OK");
//       setConnection(true);
//       if (!isFirstCheck) {
//         setSuccessMessage("Cloud connection OK");
//       }
//     }
//     isFirstCheck = false;
//     isConnectionOK = true;
//     console.log("OK:", method, uri, postData, rsp);
//   }

//   handleError(method: any, error: any, uri: string, postData?: Data) {
//     //console.log('Failed:', method, uri, postData, error)
//     if (error.response) {
//       // Request made and server responded
//       if (
//         error.response.status === 500 &&
//         error.response.data?.includes("(ECONNREFUSED)")
//       ) {
//         this.handleNetworkError();
//         return;
//       }
//       if (
//         error.response.status === 400 &&
//         error.response.data?.includes("The table specified does not exist")
//       ) {
//         this.handleInvalidToken();
//         return;
//       }
//       if (
//         error.response.status === 400 &&
//         error.response.data?.includes("Invalid token")
//       ) {
//         this.handleInvalidToken();
//         return;
//       }
//       if (
//         error.response.status === 400 &&
//         error.response.data?.includes("Invalid api request")
//       ) {
//         this.handleInvalidToken();
//         return;
//       }
//       console.log(
//         `Failed ${method} ${uri} ${postData}, status: ${error.response.status}: '${error.response.data}'`
//       );
//     } else if (error.request) {
//       // The request was made but no response was received
//       this.handleNetworkError();
//     } else {
//       // Something happened in setting up the request that triggered an Error
//       console.error("Error", method, uri, postData, error, error.message);
//     }
//   }

//   handleInvalidToken() {
//     console.error("Invalid Token");
//     this?.onInvalidToken?.();
//   }

//   handleNetworkError() {
//     console.log("Network error");
//     if (isConnectionOK !== false) {
//       console.log("Connection error");
//       setConnection(false);
//       setErrorMessage("Cloud connection failed");
//     }
//     isConnectionOK = false;
//   }
// }
