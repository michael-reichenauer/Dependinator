import { di, diKey, singleton } from "../di";
import Result, { isError } from "../Result";
import { CustomError } from "../CustomError";
import { delay } from "../utils";
import { ILocalStore, ILocalStoreKey } from "../LocalStore";

export interface RemoteEntity {
  key: string;
  timestamp: number;
  version: number;

  value: any;
}

export interface Query {
  key: string;
  IfNoneMatch?: number;
}

export class NotModifiedError extends CustomError {}
export class NetworkError extends CustomError {}

const prefix = "remote-";

export const IRemoteDBKey = diKey<IRemoteDB>();
export interface IRemoteDB {
  tryReadBatch(queries: Query[]): Promise<Result<Result<RemoteEntity>[]>>;
  writeBatch(entities: RemoteEntity[]): Promise<Result<void>>;
  removeBatch(keys: string[]): Promise<Result<void>>;
}

@singleton(IRemoteDBKey)
export class RemoteDB implements IRemoteDB {
  constructor(
    private api: ILocalStore = di(ILocalStoreKey),
    private testDelay = 850
  ) {}

  public async writeBatch(entities: RemoteEntity[]): Promise<Result<void>> {
    const remoteEntities = entities.map((entity) => ({
      key: this.remoteKey(entity.key),
      value: entity,
    }));

    await delay(this.testDelay); // Simulate network delay !!!!!!!!!!!!!

    this.api.writeBatch(remoteEntities);
  }

  public async tryReadBatch(
    queries: Query[]
  ): Promise<Result<Result<RemoteEntity>[]>> {
    const remoteKeys = queries.map((query) => this.remoteKey(query.key));

    await delay(this.testDelay); // Simulate network delay !!!!!!!!!!!!!

    const remoteEntities = this.api.tryReadBatch(remoteKeys);

    // skipNotModifiedEntities will be handled by server !!!
    return this.skipNotModifiedEntities(queries, remoteEntities);
  }

  public async removeBatch(keys: string[]): Promise<Result<void>> {
    const remoteKeys = keys.map((key) => this.remoteKey(key));

    await delay(this.testDelay); // Simulate network delay !!!!!!!!!!!!!

    this.api.removeBatch(remoteKeys);
  }

  skipNotModifiedEntities(queries: Query[], entities: Result<RemoteEntity>[]) {
    // If a query specifies IfNoneMatch, then matching existing entities are replaced by NotModifiedError
    return entities.map((entity, i) => {
      if (
        !isError(entity) &&
        queries[i].IfNoneMatch &&
        queries[i].IfNoneMatch === entity.timestamp
      ) {
        // The query specified a IfNoneMatch and entity has not been modified
        return new NotModifiedError();
      }
      return entity;
    });
  }

  remoteKey(localKey: string): string {
    return prefix + localKey;
  }

  localKey(remoteKey: string): string {
    return remoteKey.substring(prefix.length);
  }
}
