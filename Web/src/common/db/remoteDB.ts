import { di, diKey, singleton } from "../di";
import Result, { isError } from "../Result";
import { CustomError } from "../CustomError";
import { ApiEntity, IRemoteApi, IRemoteApiKey, Query } from "./RemoteApi";

export interface RemoteEntity {
  key: string;
  timestamp: number;
  version: number;

  value: any;
}

export class NotModifiedError extends CustomError {}

export const IRemoteDBKey = diKey<IRemoteDB>();
export interface IRemoteDB {
  tryReadBatch(queries: Query[]): Promise<Result<Result<RemoteEntity>[]>>;
  writeBatch(entities: RemoteEntity[]): Promise<Result<void>>;
  removeBatch(keys: string[]): Promise<Result<void>>;
}

const noValueError = new RangeError("No value for key");
const notModifiedError = new NotModifiedError();

@singleton(IRemoteDBKey)
export class RemoteDB implements IRemoteDB {
  constructor(private api: IRemoteApi = di(IRemoteApiKey)) {}

  public async tryReadBatch(
    queries: Query[]
  ): Promise<Result<Result<RemoteEntity>[]>> {
    const apiEntities = await this.api.tryReadBatch(queries);
    if (isError(apiEntities)) {
      return apiEntities;
    }

    return this.toRemoteEntities(apiEntities);
  }

  public async writeBatch(entities: RemoteEntity[]): Promise<Result<void>> {
    const apiEntities = this.toApiEntities(entities);

    return await this.api.writeBatch(apiEntities);
  }

  public async removeBatch(keys: string[]): Promise<Result<void>> {
    return await this.api.removeBatch(keys);
  }

  private toRemoteEntities(apiEntities: ApiEntity[]): Result<RemoteEntity>[] {
    return apiEntities.map((entity) => {
      if (entity.status === "noValue") {
        return noValueError;
      }
      if (entity.status === "notModified") {
        return notModifiedError;
      }
      return {
        key: entity.key,
        timestamp: entity.timestamp ?? 0,
        version: entity.value?.version ?? 0,
        value: entity.value?.value,
      };
    });
  }

  private toApiEntities(remoteEntities: RemoteEntity[]): ApiEntity[] {
    return remoteEntities.map((entity) => ({
      key: entity.key,
      status: "value",
      timestamp: entity.timestamp,
      value: { value: entity.value, version: entity.version },
    }));
  }
}
