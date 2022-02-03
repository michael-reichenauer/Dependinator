import { ApiEntity, IApi, TokenInfo, User, Query, ApiEntityRsp } from "../Api";
import { di } from "../di";
import { ILocalStore, ILocalStoreKey } from "../LocalStore";
import Result, { isError } from "../Result";

const prefix = "ApiMock-";

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

  public async writeBatch(
    entities: ApiEntity[]
  ): Promise<Result<ApiEntityRsp[]>> {
    const etag = this.generateEtag();
    const remoteEntities = entities.map((entity) => {
      entity.etag = etag;
      return { key: this.remoteKey(entity.key), value: entity };
    });

    this.local.writeBatch(remoteEntities);

    return remoteEntities.map((entity) => ({
      key: entity.value.key,
      etag: entity.value.etag,
    }));
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
        return { key: key, status: "noValue" };
      }

      if (queries[i].IfNoneMatch && queries[i].IfNoneMatch === entity.etag) {
        // The query specified a IfNoneMatch and entity has not been modified
        return { key: key, etag: entity.etag, status: "notModified" };
      }

      return {
        key: key,
        etag: entity.etag,
        value: entity.value,
      };
    });
  }

  private remoteKey(localKey: string): string {
    return prefix + localKey;
  }

  private generateEtag(): string {
    return `W/"datetime'${new Date().toISOString()}'"`.replace(/:/g, "%3A");
  }
}
