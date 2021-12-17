import {
  ILocalData,
  ILocalDataKey,
  Entity as LocalEntity,
} from "../../common/LocalData";
import {
  IRemoteData,
  IRemoteDataKey,
  Entity as RemoteEntity,
} from "../../common/remoteData";
import Result, { expectValue, isError } from "../../common/Result";
import { di, diKey, singleton } from "./../../common/di";

export interface Entity<T> {
  key: string;
  value: T;
}

export interface SyncRequest<T> {
  key: string;
  onConflict: (local: LocalEntity<T>, remote: RemoteEntity<T>) => T;
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

  public async triggerSyncCheck<T = any>(
    requests: SyncRequest<T>[]
  ): Promise<void> {
    const keys = requests.map((request) => request.key);
    const localEntities = this.localData.tryReadBatch<T>(keys);

    const queries = localEntities.map((entity, index) => {
      if (!isError(entity) && entity.synced) {
        // Local entity exists and is synced, skip remote if not changed
        return { key: keys[index], IfNoneMatch: entity.synced };
      }

      // Get entity regardless of matched timestamp
      return { key: keys[index] };
    });

    const remoteEntities = await this.remoteData.tryReadBatch<T>(queries);

    const remoteToLocalEntities: RemoteEntity<T>[] = [];
    const localToRemoteEntities: LocalEntity<T>[] = [];
    const mergedEntities: Entity<T>[] = [];

    remoteEntities.forEach((remoteEntity, index) => {
      const localEntity = localEntities[index];
      if (isError(localEntity) && isError(remoteEntity)) {
        // Both local and remote items are missing, (both have been removed and nothing to sync)
        return;
      }
      if (isError(remoteEntity)) {
        // Remote entity is missing, lets upload local to remote
        if (!isError(localEntity)) {
          localToRemoteEntities.push(localEntity);
        }
        return;
      }
      if (isError(localEntity)) {
        // Local entity is missing, lets store remote to local
        remoteToLocalEntities.push(remoteEntity);
        return;
      }

      // Both local and remote entity exist, lets check time stamps
      if (localEntity.timestamp === remoteEntity.timestamp) {
        // Both local and remote entity have same timestamp, already same (nothing to sync)
        return;
      }

      if (localEntity.synced === remoteEntity.timestamp) {
        // Local entity has changed and remote entity same as uploaded previously by this client, lets sync new local up to remote
        localToRemoteEntities.push(localEntity);
        return;
      }

      if (localEntity.synced === localEntity.timestamp) {
        // Local entity has not changed, while remote has been changed by some other client, lets store new remote
        remoteToLocalEntities.push(remoteEntity);
        return;
      }

      // Both local and remote entity has been changed by some other client, lets merge the entities
      const mergedEntity = requests[index].onConflict(
        localEntity,
        remoteEntity
      );
      mergedEntities.push({ key: localEntity.key, value: mergedEntity });
    });

    //const localSynced = remoteToLocalEntities.map

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
