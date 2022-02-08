import { di, diKey, singleton } from "../di";
import Result, { isError } from "../Result";
import { CustomError } from "../CustomError";
import { ApiEntity, IApi, IApiKey, Query } from "../Api";
import { IDataCrypt, IDataCryptKey } from "../DataCrypt";
import { IKeyVault, IKeyVaultKey } from "../keyVault";

export interface RemoteEntity {
  key: string;
  etag: string;
  localEtag: string;

  value: any;
  version: number;
}

export interface RemoteEntityRsp {
  key: string;
  status?: string;
  etag?: string;
}

export class NotModifiedError extends CustomError {}

export const IRemoteDBKey = diKey<IRemoteDB>();
export interface IRemoteDB {
  tryReadBatch(queries: Query[]): Promise<Result<Result<RemoteEntity>[]>>;
  writeBatch(entities: RemoteEntity[]): Promise<Result<RemoteEntityRsp[]>>;
  removeBatch(keys: string[]): Promise<Result<void>>;
}

const noValueError = new RangeError("No value for key");
const notModifiedError = new NotModifiedError();

@singleton(IRemoteDBKey)
export class RemoteDB implements IRemoteDB {
  constructor(
    private api: IApi = di(IApiKey),
    private keyVault: IKeyVault = di(IKeyVaultKey),
    private dataCrypt: IDataCrypt = di(IDataCryptKey)
  ) {}

  public async tryReadBatch(
    queries: Query[]
  ): Promise<Result<Result<RemoteEntity>[]>> {
    const apiEntities = await this.api.tryReadBatch(queries);
    if (isError(apiEntities)) {
      return apiEntities;
    }

    return this.toDownloadedRemoteEntities(queries, apiEntities);
  }

  public async writeBatch(
    entities: RemoteEntity[]
  ): Promise<Result<RemoteEntityRsp[]>> {
    const apiEntities = await this.toUploadingApiEntities(entities);

    return await this.api.writeBatch(apiEntities);
  }

  public async removeBatch(keys: string[]): Promise<Result<void>> {
    return await this.api.removeBatch(keys);
  }

  private async toDownloadedRemoteEntities(
    queries: Query[],
    apiEntities: ApiEntity[]
  ): Promise<Result<RemoteEntity>[]> {
    // Get the KEK key to decrypt the unique DEK key for each package
    const kek = this.keyVault.getKek();

    return Promise.all(
      queries.map(async (query) => {
        const entity = apiEntities.find((e) => e.key === query.key);
        // console.log("api entity from server", entity);
        if (!entity) {
          // The entity was never returned from remote server
          return noValueError;
        }
        if (!entity.key || !entity.etag) {
          // The entity did not have expected properties
          return noValueError;
        }
        if (entity.status === "noValue") {
          return noValueError;
        }
        if (entity.status === "notModified") {
          return notModifiedError;
        }

        // Decrypt downloaded value
        const value = await this.decryptValue(entity.value, kek);
        return {
          key: entity.key,
          etag: entity.etag ?? "",
          localEtag: "",
          value: value.value,
          version: value.version ?? 0,
        };
      })
    );
  }

  private async toUploadingApiEntities(
    remoteEntities: RemoteEntity[]
  ): Promise<ApiEntity[]> {
    // Get the KEK key to encrypt the unique DEK key for each package
    const kek = this.keyVault.getKek();

    return Promise.all(
      remoteEntities.map(async (entity) => {
        // Encrypt value before uploading
        const value = { value: entity.value, version: entity.version };
        const encryptedValue = await this.encryptValue(value, kek);

        return {
          key: entity.key,
          etag: entity.etag,
          value: encryptedValue,
        };
      })
    );
  }

  private async encryptValue(value: any, kek: any): Promise<any> {
    const valueText = JSON.stringify(value);
    const encryptedValue = await this.dataCrypt.encryptText(valueText, kek);
    return encryptedValue;
  }

  private async decryptValue(encryptedValue: any, kek: any): Promise<any> {
    const valueText = await this.dataCrypt.decryptText(encryptedValue, kek);
    const value = JSON.parse(valueText);
    return value;
  }
}
