import {
  ILocalData,
  ILocalDataKey,
  Entity as LocalEntity,
} from "../../common/LocalData";
import { IRemoteData, IRemoteDataKey } from "../../common/remoteData";
import Result, { expectValue, isError } from "../../common/Result";
import { di, diKey, singleton } from "./../../common/di";

export interface Entity<T> {
  key: string;
  value: T;
}

export const IStoreSyncKey = diKey<IStoreSync>();
export interface IStoreSync {
  initialize(): void;
  writeBatch<T>(entities: Entity<T>[]): void;
  removeBatch(keys: string[]): void;
  tryReadAsync<T>(key: string): Promise<Result<T>>;
  read<T>(key: string): T;
}

@singleton(IStoreSyncKey) // eslint-disable-next-line
class StoreSync implements IStoreSync {
  constructor(
    private localData: ILocalData = di(ILocalDataKey),
    private remoteData: IRemoteData = di(IRemoteDataKey)
  ) {}

  initialize(): void {}

  public read<T>(key: string): T {
    const entity = expectValue(this.localData.tryRead<T>(key));
    return entity.value;
  }

  public async tryReadAsync<T>(key: string): Promise<Result<T>> {
    const localEntity = this.localData.tryRead<T>(key);
    if (isError(localEntity)) {
      // Dto not cached locally, lets try get from remote location
      const remoteEntity = await this.remoteData.tryRead<T>({ key: key });
      if (isError(remoteEntity)) {
        // If network error, signal !!!!!!!!
        return new RangeError(`id ${key} not found,` + remoteEntity);
      }

      // Cache remote data locally as synced
      const entity = { ...remoteEntity, synced: remoteEntity.timestamp };
      this.localData.write(entity);
      return entity.value;
    }

    return localEntity.value;
  }

  public writeBatch<T>(entities: Entity<T>[]): void {
    const now = Date.now();
    const remoteEntities = entities.map((entity) => ({
      ...entity,
      timestamp: now,
    }));
    const localEntities = remoteEntities.map((entity) => ({
      ...entity,
      synced: 0,
    }));

    // Cache data locally
    this.localData.writeBatch(localEntities);

    // Sync data to server
    this.remoteData.writeBatch(remoteEntities).then((response) => {
      if (isError(response)) {
        // Signal sync error !!!!!!!
        console.warn("Sync error while writing");
        return;
      }

      // Stamp existing local items with synced time stamp and
      const keys = entities.map((entity) => entity.key);
      const existingEntity = this.localData
        .tryReadBatch<T>(keys)
        .filter((r) => !isError(r)) as LocalEntity<T>[];

      const syncedEntities = existingEntity.map((entity) => ({
        ...entity,
        synced: now,
      }));

      this.localData.writeBatch(syncedEntities);
    });
  }

  public removeBatch(keys: string[]): void {
    this.localData.removeBatch(keys);
  }

  public async triggerSyncCheck(): Promise<void> {
    //   const dto = await this.remoteData.tryRead<ApplicationDto>({
    //     id: applicationKey,
    //   });
    //   if (isError(dto)) {
    //     // Signal error check !!!!!!
    //     console.warn("Sync error while reading app");
    //     return;
    //   }
    //   // Merge local app and remote app
    //   //
  }
}
