import { ApiEntity, IApi, TokenInfo, User, Query } from "../Api";
import { di } from "../di";
import { ILocalStore, ILocalStoreKey } from "../LocalStore";
import Result, { isError } from "../Result";

const prefix = "remote-";

export class ApiMock implements IApi {
  constructor(private local: ILocalStore = di(ILocalStoreKey)) {}

  config(onOK: () => void, onError: (error: Error) => void): void {
    throw new Error("Method not implemented.");
  }
  login(user: User): Promise<Result<TokenInfo, Error>> {
    throw new Error("Method not implemented.");
  }
  createAccount(user: User): Promise<Result<void, Error>> {
    throw new Error("Method not implemented.");
  }
  check(): Promise<Result<void, Error>> {
    throw new Error("Method not implemented.");
  }

  public async tryReadBatch(queries: Query[]): Promise<Result<ApiEntity[]>> {
    const remoteKeys = queries.map((query) => this.remoteKey(query.key));

    const localEntities = this.local.tryReadBatch(remoteKeys);

    return this.skipNotModifiedEntities(queries, localEntities);
  }

  public async writeBatch(entities: ApiEntity[]): Promise<Result<void>> {
    const remoteEntities = entities.map((entity) => ({
      key: this.remoteKey(entity.key),
      status: "value",
      timestamp: entity.stamp,
      value: entity,
    }));

    this.local.writeBatch(remoteEntities);
  }

  public async removeBatch(keys: string[]): Promise<Result<void>> {
    const remoteKeys = keys.map((key) => this.remoteKey(key));

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
        return { key: key, status: "noValue", stamp: "" };
      }

      if (queries[i].IfNoneMatch && queries[i].IfNoneMatch === entity.stamp) {
        // The query specified a IfNoneMatch and entity has not been modified
        return { key: key, stamp: entity.stamp, status: "notModified" };
      }

      return {
        key: key,
        status: "value",
        stamp: entity.stamp,
        value: entity.value,
      };
    });
  }

  private remoteKey(localKey: string): string {
    return prefix + localKey;
  }
}
