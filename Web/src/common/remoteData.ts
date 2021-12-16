import { di, diKey, singleton } from "./di";
import Result, { isError } from "./Result";
import { ILocalData, ILocalDataKey } from "./LocalData";
import { CustomError } from "./CustomError";

export interface Entity {
  id: string;
  timestamp?: number;
}

export interface Query {
  id: string;
  IfNoneMatch?: number;
}

export class NotModifiedError extends CustomError {}

const prefix = "remote-";

export const IRemoteDataKey = diKey<IRemoteData>();
export interface IRemoteData {
  writeBatch<T extends Entity>(entities: T[]): Promise<Result<void>>;
  tryReadBatch<T extends Entity>(queries: Query[]): Promise<Result<T>[]>;
  tryRead<T extends Entity>(query: Query): Promise<Result<T>>;
}

@singleton(IRemoteDataKey) // eslint-disable-next-line
class RemoteData implements IRemoteData {
  constructor(private api: ILocalData = di(ILocalDataKey)) {}

  async tryRead<T extends Entity>(query: Query): Promise<Result<T>> {
    const responses = await this.tryReadBatch<T>([query]);
    return responses[0];
  }

  async writeBatch<T extends Entity>(entities: T[]): Promise<Result<void>> {
    const remoteEntities = entities.map((entity) => ({
      ...entity,
      id: this.remoteId(entity.id),
    }));

    this.api.writeBatch(remoteEntities);
  }

  async tryReadBatch<T extends Entity>(queries: Query[]): Promise<Result<T>[]> {
    const remoteKeys = queries.map((query) => this.remoteId(query.id));
    const entities = this.api.tryReadBatch<T>(remoteKeys);

    // skipNotModifiedEntities will be handled by server
    return this.skipNotModifiedEntities(queries, entities);
  }

  skipNotModifiedEntities<T extends Entity>(
    queries: Query[],
    entities: Result<T>[]
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

  remoteId(id: string): string {
    return prefix + id;
  }
}
