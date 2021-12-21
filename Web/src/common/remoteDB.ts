import { di, diKey, singleton } from "./di";
import Result, { isError } from "./Result";
import { ILocalDB, ILocalDBKey } from "./LocalDB";
import { CustomError } from "./CustomError";
import { delay } from "./utils";

export interface RemoteEntity<T> {
  key: string;
  timestamp: number;
  version: number;

  value: T;
}

export interface Query {
  key: string;
  IfNoneMatch?: number;
}

export class NotModifiedError extends CustomError {}

const prefix = "remote-";

export const IRemoteDBKey = diKey<IRemoteDB>();
export interface IRemoteDB {
  writeBatch<T>(entities: RemoteEntity<T>[]): Promise<Result<void>>;
  tryReadBatch<T>(queries: Query[]): Promise<Result<RemoteEntity<T>>[]>;
  tryRead<T>(query: Query): Promise<Result<RemoteEntity<T>>>;
}

@singleton(IRemoteDBKey) // eslint-disable-next-line
class RemoteDB implements IRemoteDB {
  constructor(private api: ILocalDB = di(ILocalDBKey)) {}

  async tryRead<T>(query: Query): Promise<Result<RemoteEntity<T>>> {
    const responses = await this.tryReadBatch<T>([query]);
    return responses[0];
  }

  async writeBatch<T>(entities: RemoteEntity<T>[]): Promise<Result<void>> {
    const remoteEntities = entities.map((entity) => ({
      ...entity,
      key: this.remoteKey(entity.key),
      synced: 0,
    }));

    await delay(850); // Simulate network delay !!!!!!!!!!!!!

    this.api.writeBatch(remoteEntities);
  }

  async tryReadBatch<T>(queries: Query[]): Promise<Result<RemoteEntity<T>>[]> {
    const remoteKeys = queries.map((query) => this.remoteKey(query.key));

    await delay(850); // Simulate network delay !!!!!!!!!!!!!

    const remoteEntities = this.api.tryReadBatch<T>(remoteKeys);
    const entities = remoteEntities.map((entity) => {
      if (isError(entity)) {
        return entity;
      }
      return { ...entity, key: this.localKey(entity.key) };
    });

    // skipNotModifiedEntities will be handled by server
    return this.skipNotModifiedEntities(queries, entities);
  }

  skipNotModifiedEntities<T>(
    queries: Query[],
    entities: Result<RemoteEntity<T>>[]
  ) {
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
