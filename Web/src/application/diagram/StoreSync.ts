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
  onConflict: (
    local: LocalEntity<T>,
    remote: RemoteEntity<T>
  ) => LocalEntity<T>;
}

export const IStoreSyncKey = diKey<IStoreSync>();
export interface IStoreSync {
  initialize(): void;
  readLocal<T>(key: string, defaultValue: T): T;
  tryReadLocalThenRemoteAsync<T>(key: string): Promise<Result<T>>;
  writeBatch<T>(entities: Entity<T>[]): void;
  removeBatch(keys: string[]): void;
  triggerSync<T = any>(requests: SyncRequest<T>[], syncNonLocal: boolean): void;
}

@singleton(IStoreSyncKey) // eslint-disable-next-line
class StoreSync implements IStoreSync {
  private syncPromise = Promise.resolve();
  private syncStartTime: number = 0;

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
    const keys = entities.map((entity) => entity.key);
    const localEntities = this.localData.tryReadBatch(keys);

    const updatedLocalEntities = entities.map((entity, index) => {
      const localEntity = localEntities[index];
      if (isError(localEntity)) {
        // First version of local entity
        return {
          key: entity.key,
          timestamp: Date.now(),
          version: 1,
          synced: 0,
          value: entity.value,
        };
      }

      // Updating cached entity
      return {
        key: entity.key,
        timestamp: Date.now(),
        version: localEntity.version + 1,
        synced: localEntity.synced,
        value: entity.value,
      };
    });

    // Cache data locally
    console.log("Write local", updatedLocalEntities);
    this.localData.writeBatch(updatedLocalEntities);
  }

  public removeBatch(keys: string[]): void {
    this.localData.removeBatch(keys);
  }

  public triggerSync<T = any>(requests: SyncRequest<T>[]): void {
    console.log("Trigger sync");
    this.syncPromise = this.syncPromise.then(async () => {
      console.log("Start sync", requests);
      this.syncStartTime = Date.now();
      await this.syncLocalAndRemote<any>(requests);
      console.log("Done sync", Date.now() - this.syncStartTime, requests);
    });
    console.log("Triggered sync");
  }

  public async syncLocalAndRemote<T = any>(
    requests: SyncRequest<T>[]
  ): Promise<void> {
    console.log("syncLocalAndRemote", requests);

    const keys = requests.map((request) => request.key);
    let preLocalEntities = this.localData.tryReadBatch<T>(keys);

    const queries = preLocalEntities
      .filter((entity) => !isError(entity))
      .map((entity, index) => {
        if (!isError(entity) && entity.synced) {
          // Local entity exists and is synced, skip retrieving remote if not changed
          return { key: keys[index], IfNoneMatch: entity.synced };
        }

        // Get entity regardless of matched timestamp
        return { key: keys[index] };
      });

    if (!queries.length) {
      console.log("Nothing to sync");
      return;
    }

    const currentRemoteEntities = await this.remoteData.tryReadBatch<T>(
      queries
    );
    const currentLocalEntities = this.localData.tryReadBatch<T>(keys);

    const remoteToLocalEntities: RemoteEntity<T>[] = [];
    const localToRemoteEntities: LocalEntity<T>[] = [];
    const mergedEntities: LocalEntity<T>[] = [];

    currentRemoteEntities.forEach((remoteEntity, index) => {
      const currentLocalEntity = currentLocalEntities[index];

      if (isError(currentLocalEntity)) {
        // Local entity is missing, skip sync
        return;
      }

      if (remoteEntity instanceof NotModifiedError) {
        // Remote entity was not changed since last sync, syncing if local has changed
        if (currentLocalEntity.synced !== currentLocalEntity.timestamp) {
          localToRemoteEntities.push(currentLocalEntity);
        }
        return;
      }
      if (isError(remoteEntity)) {
        // Remote entity is missing, lets upload local to remote
        localToRemoteEntities.push(currentLocalEntity);
        return;
      }

      // Both local and remote entity exist, lets check time stamps
      if (currentLocalEntity.timestamp === remoteEntity.timestamp) {
        // Both local and remote entity have same timestamp, already same (nothing to sync)
        return;
      }

      if (currentLocalEntity.synced === remoteEntity.timestamp) {
        // Local entity has changed and remote entity same as uploaded previously by this client, lets sync new local up to remote
        localToRemoteEntities.push(currentLocalEntity);
        return;
      }

      if (currentLocalEntity.synced === currentLocalEntity.timestamp) {
        // Local entity has not changed, while remote has been changed by some other client, lets store new remote
        remoteToLocalEntities.push(remoteEntity);
        // Signal updated local entity by remote entity
        return;
      }

      // Both local and remote entity has been changed by some other client, lets merge the entities
      mergedEntities.push(
        requests[index].onConflict(currentLocalEntity, remoteEntity)
      );
    });

    // Convert remote entity to LocalEntity with synced=<remote timestamp>
    const localEntitiesToUpdate: LocalEntity<T>[] = remoteToLocalEntities.map(
      (remoteEntity) => ({
        key: remoteEntity.key,
        timestamp: remoteEntity.timestamp,
        version: remoteEntity.version,
        synced: remoteEntity.timestamp,
        value: remoteEntity.value,
      })
    );

    // Convert local entity to remote entity to be uploaded
    const remoteEntitiesToUpload: RemoteEntity<T>[] = localToRemoteEntities.map(
      (localEntity) => ({
        key: localEntity.key,
        timestamp: localEntity.timestamp,
        version: localEntity.version,
        value: localEntity.value,
      })
    );

    // Add merged entity to both local and to be uploaded to remote
    const now = Date.now();
    mergedEntities.forEach((mergedEntity) => {
      localEntitiesToUpdate.push({
        key: mergedEntity.key,
        timestamp: now,
        version: mergedEntity.version + 1,
        synced: mergedEntity.synced,
        value: mergedEntity.value,
      });
      remoteEntitiesToUpload.push({
        key: mergedEntity.key,
        timestamp: now,
        version: mergedEntity.version + 1,
        value: mergedEntity.value,
      });
    });

    console.log("update local", localEntitiesToUpdate);
    // Cache local entities
    this.localData.writeBatch<any>(localEntitiesToUpdate);

    console.log("Upload remote", remoteEntitiesToUpload);

    if (!remoteEntitiesToUpload.length) {
      console.log("Nothing to upload !!");
      return;
    }

    const uploadResponse = await this.remoteData.writeBatch<any>(
      remoteEntitiesToUpload
    );

    if (isError(uploadResponse)) {
      // Signal sync error !!!!!!!
      console.warn("Sync error while writing");
      return;
    }

    // Stamp existing local items with synced time stamp
    const uploadKeys = remoteEntitiesToUpload.map((entity) => entity.key);
    console.log("upload keys", uploadKeys);
    const syncedItems: { [key: string]: number } = {};
    remoteEntitiesToUpload.forEach(
      (item) => (syncedItems[item.key] = item.timestamp)
    );

    const postLocalEntities = this.localData
      .tryReadBatch<T>(uploadKeys)
      .filter((r) => !isError(r)) as LocalEntity<T>[];

    const syncedEntities = postLocalEntities.map((entity) => ({
      ...entity,
      synced: syncedItems[entity.key],
    }));
    this.localData.writeBatch(syncedEntities);
  }

  private cacheLocalValueOnly<T>(key: string, value: T) {
    const entity = {
      key: key,
      timestamp: Date.now(),
      version: 1,
      synced: 0,
      value: value,
    };
    this.localData.write(entity);
  }

  private cacheRemoteEntity<T>(remoteEntity: RemoteEntity<T>) {
    const entity = {
      key: remoteEntity.key,
      timestamp: remoteEntity.timestamp,
      version: remoteEntity.version,
      synced: remoteEntity.timestamp,
      value: remoteEntity.value,
    };
    this.localData.write(entity);
  }
}
