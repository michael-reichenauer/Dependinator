import axios, { AxiosResponse } from "axios";
import timing from "../../common/timing";
import { atom, useAtom } from "jotai";
import {
  setErrorMessage,
  setSuccessMessage,
} from "../../common/MessageSnackbar";
import { keyVault } from "../../common/keyVault";

const connectionAtom = atom(false);
let setConnectionFunc: (flag: boolean) => void = (_: boolean) => {};
let isConnectionOK: boolean | null = null;
let isFirstCheck = true;

const setConnection: (flag: boolean) => void = (flag: boolean) =>
  setConnectionFunc(flag);

type Data = any;

export interface User {
  username: string;
  password: string;
}

export const useConnection = () => {
  const [connection, setConnection] = useAtom(connectionAtom);
  setConnectionFunc = setConnection;
  return [connection];
};

export default class Api {
  apiKey = "0624bc00-fcf7-4f31-8f3e-3bdc3eba7ade"; // Must be same as in server side api
  onInvalidToken: () => void;
  getToken = () => keyVault.getToken();
  setToken = (token: string) => keyVault.setToken(token);

  constructor(onInvalidToken: () => void) {
    this.onInvalidToken = onInvalidToken;
  }

  // async getCurrentUser() {
  //     console.log('host', window.location.hostname)
  //     if (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1') {
  //         await this.get('/api/Check')
  //         return {
  //             clientPrincipal: {
  //                 "identityProvider": "Local",
  //                 "userId": 'local',
  //                 "userDetails": 'local',
  //                 "userRoles": ["anonymous", "authenticated"]
  //             }
  //         }
  //     }

  //     return await this.get('/.auth/me')
  // }

  async getManifest() {
    return this.get("/manifest.json");
  }

  async check() {
    return this.get("/api/Check");
  }

  // async clearAllData() {
  //     return this.post('/api/ClearAllData')
  // }

  async createUser(user: User) {
    return this.post("/api/CreateUser", user);
  }

  async connectUser(user: User) {
    return this.post("/api/ConnectUser", user);
  }

  async connect() {
    return this.get("/api/Connect");
  }

  async getAllDiagramsData() {
    return this.get(`/api/GetAllDiagramsData`);
  }

  async newDiagram(diagram: Data) {
    return this.post("/api/NewDiagram", diagram);
  }

  async setCanvas(canvas: Data) {
    return this.post("/api/SetCanvas", canvas);
  }

  async getDiagram(diagramId: string) {
    return this.get(`/api/GetDiagram?diagramId=${diagramId}`);
  }

  async deleteDiagram(diagramId: string) {
    return this.post(`/api/DeleteDiagram`, { diagramId: diagramId });
  }

  async updateDiagram(diagram: Data) {
    return this.post(`/api/UpdateDiagram`, diagram);
  }

  async uploadDiagrams(diagrams: Data) {
    return this.post(`/api/UploadDiagrams`, diagrams);
  }

  async downloadAllDiagrams() {
    return this.get(`/api/DownloadAllDiagrams`);
  }

  // api helper functions ---------------------------------
  async get(uri: string) {
    this.handleRequest("get", uri);
    const t = timing();
    try {
      const rsp = (
        await axios.get(uri, {
          headers: { "x-api-key": this.apiKey, xtoken: this.getToken() },
        })
      ).data;
      this.handleOK("get", uri, null, rsp);
      return rsp;
    } catch (error) {
      this.handleError("get", error, uri);
      throw error;
    } finally {
      t.log("get", uri);
    }
  }

  async post(uri: string, data: Data) {
    this.handleRequest("post", uri, data);
    const t = timing();
    try {
      const rsp = (
        await axios.post(uri, data, {
          headers: { "x-api-key": this.apiKey, xtoken: this.getToken() },
        })
      ).data;
      this.handleOK("post", uri, data, rsp);
      return rsp;
    } catch (error) {
      this.handleError("post", error, uri, data);
      throw error;
    } finally {
      t.log("post", uri);
    }
  }

  handleRequest(method: string, uri: string, postData?: Data) {
    console.log("Request:", method, uri, postData);
  }

  handleOK(
    method: string,
    uri: string,
    postData: any,
    rsp: AxiosResponse<any>
  ) {
    if (isConnectionOK !== true) {
      console.log("Connection OK");
      setConnection(true);
      if (!isFirstCheck) {
        setSuccessMessage("Cloud connection OK");
      }
    }
    isFirstCheck = false;
    isConnectionOK = true;
    console.log("OK:", method, uri, postData, rsp);
  }

  handleError(method: any, error: any, uri: string, postData?: Data) {
    //console.log('Failed:', method, uri, postData, error)
    if (error.response) {
      // Request made and server responded
      if (
        error.response.status === 500 &&
        error.response.data?.includes("(ECONNREFUSED)")
      ) {
        this.handleNetworkError();
        return;
      }
      if (
        error.response.status === 400 &&
        error.response.data?.includes("The table specified does not exist")
      ) {
        this.handleInvalidToken();
        return;
      }
      if (
        error.response.status === 400 &&
        error.response.data?.includes("Invalid token")
      ) {
        this.handleInvalidToken();
        return;
      }
      if (
        error.response.status === 400 &&
        error.response.data?.includes("Invalid api request")
      ) {
        this.handleInvalidToken();
        return;
      }
      console.log(
        `Failed ${method} ${uri} ${postData}, status: ${error.response.status}: '${error.response.data}'`
      );
    } else if (error.request) {
      // The request was made but no response was received
      this.handleNetworkError();
    } else {
      // Something happened in setting up the request that triggered an Error
      console.error("Error", method, uri, postData, error, error.message);
    }
  }

  handleInvalidToken() {
    console.error("Invalid Token");
    this?.onInvalidToken?.();
  }

  handleNetworkError() {
    console.log("Network error");
    if (isConnectionOK !== false) {
      console.log("Connection error");
      setConnection(false);
      setErrorMessage("Cloud connection failed");
    }
    isConnectionOK = false;
  }
}
