import { di, diKey, singleton } from "../di";
import Result, { isError } from "../Result";
import { CustomError } from "../CustomError";
import { ApiEntity, IApi, IApiKey, Query } from "../Api";

export interface RemoteEntity {
  key: string;
  etag: string;
  localEtag: string;

  //stamp: string;

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
  constructor(private api: IApi = di(IApiKey)) {}

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
    const apiEntities = this.toUploadingApiEntities(entities);

    return await this.api.writeBatch(apiEntities);
  }

  public async removeBatch(keys: string[]): Promise<Result<void>> {
    return await this.api.removeBatch(keys);
  }

  private toDownloadedRemoteEntities(
    queries: Query[],
    apiEntities: ApiEntity[]
  ): Result<RemoteEntity>[] {
    return queries.map((query) => {
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
      return {
        key: entity.key,
        etag: entity.etag ?? "",
        localEtag: "",
        value: entity.value?.value,
        version: entity.value?.version ?? 0,
      };
    });
  }

  private toUploadingApiEntities(remoteEntities: RemoteEntity[]): ApiEntity[] {
    return remoteEntities.map((entity) => ({
      key: entity.key,
      etag: entity.etag,
      value: { value: entity.value, version: entity.version },
    }));
  }
}
