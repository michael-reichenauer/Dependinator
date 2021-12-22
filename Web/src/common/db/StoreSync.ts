import { ILocalDB, ILocalDBKey, LocalEntity } from "./LocalDB";
import {
  IRemoteDB,
  IRemoteDBKey,
  NotModifiedError,
  RemoteEntity,
} from "./remoteDB";
import Result, { isError } from "../Result";
import { di, diKey, singleton } from "../di";

export interface Entity {
  key: string;
  value: any;
}

export interface SyncRequest {
  key: string;
  onConflict: (local: LocalEntity, remote: RemoteEntity) => LocalEntity;
}

export const IStoreSyncKey = diKey<IStoreSync>();
export interface IStoreSync {
  initialize(): void;
  readLocal<T>(key: string, defaultValue: T): T;
  tryReadLocalThenRemoteAsync<T>(key: string): Promise<Result<T>>;
  writeBatch(entities: Entity[]): void;
  removeBatch(keys: string[]): void;
  triggerSync(requests: SyncRequest[], syncNonLocal: boolean): void;
}

@singleton(IStoreSyncKey)
export class StoreSync implements IStoreSync {
  private syncPromise = Promise.resolve();

  constructor(
    private localDB: ILocalDB = di(ILocalDBKey),
    private remoteDB: IRemoteDB = di(IRemoteDBKey)
  ) {}

  initialize(): void {}

  public readLocal<T>(key: string, defaultValue: T): T {
    const localValue = this.localDB.tryReadValue<T>(key);
    if (isError(localValue)) {
      // Entity not cached locally, lets cache default value
      this.cacheLocalValueOnly(key, defaultValue);
      return defaultValue;
    }

    return localValue;
  }

  public async tryReadLocalThenRemoteAsync<T>(key: string): Promise<Result<T>> {
    const localValue = this.localDB.tryReadValue<T>(key);
    if (isError(localValue)) {
      // Entity not cached locally, lets try get from remote location
      const remoteEntity = await this.remoteDB.tryRead({ key: key });
      if (isError(remoteEntity)) {
        // If network error, signal !!!!!!!!
        return new RangeError(`id ${key} not found,` + remoteEntity);
      }

      // Cache remote data locally as synced
      this.cacheRemoteEntity(remoteEntity);
      return remoteEntity.value;
    }

    return localValue;
  }

  public writeBatch(entities: Entity[]): void {
    const keys = entities.map((entity) => entity.key);
    const localEntities = this.localDB.tryReadBatch(keys);

    const updatedLocalEntities = entities.map((entity, index) => {
      const localEntity = localEntities[index];
      if (isError(localEntity)) {
        // First version of local entity
        return {
          key: entity.key,
          timestamp: Date.now(),
          version: 1,
          synced: 0,
          isRemoved: false,
          value: entity.value,
        };
      }

      // Updating cached entity
      return {
        key: entity.key,
        timestamp: Date.now(),
        version: localEntity.version + 1,
        synced: localEntity.synced,
        isRemoved: false,
        value: entity.value,
      };
    });

    // Cache data locally
    console.log("Write local", updatedLocalEntities);
    this.localDB.writeBatch(updatedLocalEntities);
  }

  public removeBatch(keys: string[]): void {
    this.localDB.removeBatch(keys, false);
    const requests = keys.map((key) => ({
      key: key,
      onConflict: (): any => {},
    }));
    this.triggerSync(requests);
  }

  public triggerSync(requests: SyncRequest[]): void {
    // Trigger sync, but ensure syncs are run in sequence awaiting previous sync
    this.syncPromise = this.syncPromise.then(async () => {
      await this.syncLocalAndRemote(requests);
    });
  }

  public async syncLocalAndRemote(requests: SyncRequest[]): Promise<void> {
    console.log("syncLocalAndRemote", requests);

    const keys = requests.map((request) => request.key);
    let preLocalEntities = this.localDB.tryReadBatch(keys);

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

    const currentRemoteEntities = await this.remoteDB.tryReadBatch(queries);
    const currentLocalEntities = this.localDB.tryReadBatch(keys);

    const remoteToLocalEntities: RemoteEntity[] = [];
    const localToRemoteEntities: LocalEntity[] = [];
    const mergedEntities: LocalEntity[] = [];

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
    const localEntitiesToUpdate: LocalEntity[] = remoteToLocalEntities.map(
      (remoteEntity) => ({
        key: remoteEntity.key,
        timestamp: remoteEntity.timestamp,
        version: remoteEntity.version,
        synced: remoteEntity.timestamp,
        isRemoved: false,
        value: remoteEntity.value,
      })
    );

    // Convert local entity to remote entity to be uploaded
    const remoteEntitiesToUpload: RemoteEntity[] = localToRemoteEntities.map(
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
        isRemoved: false,
        value: mergedEntity.value,
      });
      remoteEntitiesToUpload.push({
        key: mergedEntity.key,
        timestamp: now,
        version: mergedEntity.version + 1,
        value: mergedEntity.value,
      });
    });

    // Cache local entities
    this.localDB.writeBatch(localEntitiesToUpdate);

    await this.uploadEntities(remoteEntitiesToUpload);

    await this.syncRemovedEntities();
  }

  private async uploadEntities(
    remoteEntitiesToUpload: RemoteEntity[]
  ): Promise<void> {
    if (!remoteEntitiesToUpload.length) {
      // Nothing to upload
      return;
    }

    const uploadResponse = await this.remoteDB.writeBatch(
      remoteEntitiesToUpload
    );

    if (isError(uploadResponse)) {
      // Signal sync error !!!!!!!
      console.warn("Sync error while writing entities");
      return;
    }

    // Stamp existing local items with synced time stamp
    const uploadKeys = remoteEntitiesToUpload.map((entity) => entity.key);
    const syncedItems: { [key: string]: number } = {};
    remoteEntitiesToUpload.forEach(
      (item) => (syncedItems[item.key] = item.timestamp)
    );

    const postLocalEntities = this.localDB
      .tryReadBatch(uploadKeys)
      .filter((r) => !isError(r)) as LocalEntity[];

    const syncedEntities = postLocalEntities.map((entity) => ({
      ...entity,
      synced: syncedItems[entity.key],
    }));
    this.localDB.writeBatch(syncedEntities);
  }

  private async syncRemovedEntities() {
    const removedKeys = this.localDB.getRemovedKeys();
    const response = await this.remoteDB.removeBatch(removedKeys);
    if (isError(response)) {
      console.warn("Sync error while removing entities");
      return;
    }

    this.localDB.confirmRemoved(removedKeys);
  }

  private cacheLocalValueOnly<T>(key: string, value: T) {
    const entity: LocalEntity = {
      key: key,
      timestamp: Date.now(),
      version: 1,
      synced: 0,
      isRemoved: false,
      value: value,
    };
    this.localDB.write(entity);
  }

  private cacheRemoteEntity(remoteEntity: RemoteEntity) {
    const entity = {
      key: remoteEntity.key,
      timestamp: remoteEntity.timestamp,
      version: remoteEntity.version,
      synced: remoteEntity.timestamp,
      isRemoved: false,
      value: remoteEntity.value,
    };
    this.localDB.write(entity);
  }
}
