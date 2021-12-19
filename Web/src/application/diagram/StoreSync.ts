import {
  ILocalData,
  ILocalDataKey,
  Entity as LocalEntity,
} from "../../common/LocalData";
import {
  IRemoteData,
  IRemoteDataKey,
  NotModifiedError,
  Entity as RemoteEntity,
} from "../../common/remoteData";
import Result, { isError } from "../../common/Result";
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
  readLocal<T>(key: string, defaultValue: T): T;
  tryReadLocalThenRemoteAsync<T>(key: string): Promise<Result<T>>;
  writeBatch<T>(entities: Entity<T>[]): void;
  removeBatch(keys: string[]): void;
  triggerSync<T = any>(
    requests: SyncRequest<T>[],
    syncNonLocal: boolean
  ): Promise<void>;
}

@singleton(IStoreSyncKey) // eslint-disable-next-line
class StoreSync implements IStoreSync {
  constructor(
    private localData: ILocalData = di(ILocalDataKey),
    private remoteData: IRemoteData = di(IRemoteDataKey)
  ) {}

  initialize(): void {}

  public readLocal<T>(key: string, defaultValue: T): T {
    const localEntity = this.localData.tryRead<T>(key);
    if (isError(localEntity)) {
      // Entity not cached locally, lets cache default value
      this.cacheLocalValueOnly(key, defaultValue);
      return defaultValue;
    }

    return localEntity.value;
  }

  public async tryReadLocalThenRemoteAsync<T>(key: string): Promise<Result<T>> {
    const localEntity = this.localData.tryRead<T>(key);
    if (isError(localEntity)) {
      // Entity not cached locally, lets try get from remote location
      const remoteEntity = await this.remoteData.tryRead<T>({ key: key });
      if (isError(remoteEntity)) {
        // If network error, signal !!!!!!!!
        return new RangeError(`id ${key} not found,` + remoteEntity);
      }

      // Cache remote data locally as synced
      this.cacheRemoteEntity(remoteEntity);
      return remoteEntity.value;
    }

    return localEntity.value;
  }

  public writeBatch<T>(entities: Entity<T>[]): void {
    const localEntities = entities.map((entity) => ({
      key: entity.key,
      timestamp: Date.now(),
      synced: 0,
      value: entity.value,
    }));

    // Cache data locally
    this.localData.writeBatch(localEntities);
  }

  public removeBatch(keys: string[]): void {
    this.localData.removeBatch(keys);
  }

  public async triggerSync<T = any>(requests: SyncRequest<T>[]): Promise<void> {
    console.log("triggerTwoWaySync", requests);

    const keys = requests.map((request) => request.key);
    const currentLocalEntities = this.localData.tryReadBatch<T>(keys);

    const queries = currentLocalEntities
      .filter((entity) => !isError(entity))
      .map((entity, index) => {
        if (!isError(entity) && entity.synced) {
          // Local entity exists and is synced, skip remote if not changed
          return { key: keys[index], IfNoneMatch: entity.synced };
        }

        // Get entity regardless of matched timestamp
        return { key: keys[index] };
      });

    const currentRemoteEntities = await this.remoteData.tryReadBatch<T>(
      queries
    );

    const remoteToLocalEntities: RemoteEntity<T>[] = [];
    const localToRemoteEntities: LocalEntity<T>[] = [];
    const mergedEntities: Entity<T>[] = [];

    currentRemoteEntities.forEach((remoteEntity, index) => {
      const localEntity = currentLocalEntities[index];

      if (remoteEntity instanceof NotModifiedError) {
        // Remote entity was not changed since last sync, no need to sync)
        return;
      }
      if (isError(localEntity)) {
        // Local entity is missing, skip sync
        return;
      }
      if (isError(remoteEntity)) {
        // Remote entity is missing, lets upload local to remote
        localToRemoteEntities.push(localEntity);
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
        // Signal updated local entity by remote entity
        return;
      }

      // Both local and remote entity has been changed by some other client, lets merge the entities
      const mergedEntity = requests[index].onConflict(
        localEntity,
        remoteEntity
      );

      mergedEntities.push({ key: localEntity.key, value: mergedEntity });
    });

    // Convert remote entity to LocalEntity with synced=<remote timestamp>
    const localEntitiesToUpdate = remoteToLocalEntities.map((remoteEntity) => ({
      key: remoteEntity.key,
      timestamp: remoteEntity.timestamp,
      synced: remoteEntity.timestamp,
      value: remoteEntity.value,
    }));

    // Convert local entity to remote entity to be uploaded
    const remoteEntitiesToUpload = localToRemoteEntities.map((localEntity) => ({
      key: localEntity.key,
      timestamp: localEntity.timestamp,
      value: localEntity.value,
    }));

    // Add merged entity to both local and to be uploaded to remote
    const now = Date.now();
    mergedEntities.forEach((mergedEntity) => {
      localEntitiesToUpdate.push({
        key: mergedEntity.key,
        timestamp: now,
        synced: 0,
        value: mergedEntity.value,
      });
      remoteEntitiesToUpload.push({
        key: mergedEntity.key,
        timestamp: now,
        value: mergedEntity.value,
      });
    });

    // Cache local entities
    this.localData.writeBatch<any>(localEntitiesToUpdate);

    this.remoteData.writeBatch<any>(remoteEntitiesToUpload).then((response) => {
      if (isError(response)) {
        // Signal sync error !!!!!!!
        console.warn("Sync error while writing");
        return;
      }

      // Stamp existing local items with synced time stamp and
      const keys = remoteEntitiesToUpload.map((entity) => entity.key);
      const localEntities = this.localData
        .tryReadBatch<T>(keys)
        .filter((r) => !isError(r)) as LocalEntity<T>[];

      const syncedEntities = localEntities.map((entity) => ({
        ...entity,
        synced: now,
      }));
      this.localData.writeBatch(syncedEntities);
    });
  }

  private cacheLocalValueOnly<T>(key: string, value: T) {
    const entity = {
      key: key,
      timestamp: Date.now(),
      synced: 0,
      value: value,
    };
    this.localData.write(entity);
  }

  private cacheRemoteEntity<T>(remoteEntity: RemoteEntity<T>) {
    const entity = {
      key: remoteEntity.key,
      timestamp: remoteEntity.timestamp,
      synced: remoteEntity.timestamp,
      value: remoteEntity.value,
    };
    this.localData.write(entity);
  }
}
