import { di, diKey, singleton } from "../di";
import { ILocalStore, ILocalStoreKey } from "../LocalStore";
import Result, { isError } from "../Result";
import { delay } from "../utils";

export type ApiEntityStatus = "value" | "noValue" | "notModified";

export interface ApiEntity {
  key: string;
  status: ApiEntityStatus;
  timestamp?: number;
  value?: any;
}

export interface Query {
  key: string;
  IfNoneMatch?: number;
}

export const IRemoteApiKey = diKey<IRemoteApi>();
export interface IRemoteApi {
  tryReadBatch(queries: Query[]): Promise<Result<ApiEntity[]>>;
  writeBatch(entities: ApiEntity[]): Promise<Result<void>>;
  removeBatch(keys: string[]): Promise<Result<void>>;
}

const prefix = "remote-";

@singleton(IRemoteApiKey)
export class RemoteApi implements IRemoteApi {
  constructor(private local: ILocalStore = di(ILocalStoreKey)) {}

  public static testDelay: number = 850;

  public async tryReadBatch(queries: Query[]): Promise<Result<ApiEntity[]>> {
    const remoteKeys = queries.map((query) => this.remoteKey(query.key));

    await delay(RemoteApi.testDelay); // Simulate network delay !!!!!!!!!!!!!

    const localEntities = this.local.tryReadBatch(remoteKeys);

    return this.skipNotModifiedEntities(queries, localEntities);
  }

  public async writeBatch(entities: ApiEntity[]): Promise<Result<void>> {
    const remoteEntities = entities.map((entity) => ({
      key: this.remoteKey(entity.key),
      status: "value",
      timestamp: entity.timestamp,
      value: entity,
    }));

    await delay(RemoteApi.testDelay); // Simulate network delay !!!!!!!!!!!!!

    this.local.writeBatch(remoteEntities);
  }

  public async removeBatch(keys: string[]): Promise<Result<void>> {
    const remoteKeys = keys.map((key) => this.remoteKey(key));

    await delay(RemoteApi.testDelay); // Simulate network delay !!!!!!!!!!!!!

    this.local.removeBatch(remoteKeys);
  }

  private skipNotModifiedEntities(
    queries: Query[],
    entities: Result<ApiEntity>[]
  ) {
    // If a query specifies IfNoneMatch, then matching existing entities are replaced by NotModifiedError
    return entities.map((entity, i): ApiEntity => {
      const key = queries[i].key;
      if (isError(entity)) {
        return { key: key, status: "noValue" };
      }

      if (
        queries[i].IfNoneMatch &&
        queries[i].IfNoneMatch === entity.timestamp
      ) {
        // The query specified a IfNoneMatch and entity has not been modified
        return { key: key, timestamp: entity.timestamp, status: "notModified" };
      }

      return {
        key: key,
        status: "value",
        timestamp: entity.timestamp,
        value: entity.value,
      };
    });
  }

  private remoteKey(localKey: string): string {
    return prefix + localKey;
  }
}
