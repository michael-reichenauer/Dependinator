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

export type OnConflict = (
  local: LocalEntity,
  remote: RemoteEntity
) => LocalEntity;
export interface SyncRequest {
  key: string;
  onConflict: OnConflict;
}

export const IStoreDBKey = diKey<IStoreDB>();
export interface IStoreDB {
  enableSync(isSyncEnabled: boolean, onConflict: OnConflict): void;
  monitorRemoteEntities(keys: string[]): void;
  readLocal<T>(key: string, defaultValue: T): T;
  tryReadLocalThenRemoteAsync<T>(key: string): Promise<Result<T>>;
  writeBatch(entities: Entity[]): void;
  removeBatch(keys: string[]): void;
}

@singleton(IStoreDBKey)
export class StoreDB implements IStoreDB {
  private syncPromise = Promise.resolve();
  private isSyncEnabled: boolean = false;
  private onConflict: OnConflict = (): any => {};
  private monitorKeys: string[] = [];

  constructor(
    private localDB: ILocalDB = di(ILocalDBKey),
    private remoteDB: IRemoteDB = di(IRemoteDBKey)
  ) {}

  monitorRemoteEntities(keys: string[]): void {
    this.monitorKeys = [...keys];
  }

  enableSync(isSyncEnabled: boolean, onConflict: OnConflict): void {
    this.isSyncEnabled = isSyncEnabled;
    this.onConflict = onConflict;

    if (isSyncEnabled) {
      this.triggerSync();
    }
  }

  public readLocal<T>(key: string, defaultValue: T): T {
    const localValue = this.localDB.tryReadValue<T>(key);
    if (isError(localValue)) {
      // Entity not cached locally, lets cache default value for future access
      this.cacheLocalValueOnly(key, defaultValue);
      return defaultValue;
    }

    return localValue;
  }

  public async tryReadLocalThenRemoteAsync<T>(key: string): Promise<Result<T>> {
    const localValue = this.localDB.tryReadValue<T>(key);
    if (isError(localValue)) {
      // Entity not cached locally, lets try get from remote location
      if (!this.isSyncEnabled) {
        return localValue;
      }
      const remoteEntity = await this.remoteDB.tryRead({ key: key });
      if (isError(remoteEntity)) {
        // Network error, signal !!!!!!!!
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
    const now = Date.now();

    const currentEntities = this.localDB.tryReadBatch(keys);

    // Updating current local entities with new data and setting timestamp and increase version
    const updatedLocalEntities = entities.map((newEntity, index) => {
      const localEntity = currentEntities[index];
      if (isError(localEntity)) {
        // First version of local entity (not yet cached)
        return {
          key: newEntity.key,
          timestamp: now,
          version: 1,
          synced: 0,
          isRemoved: false,
          value: newEntity.value,
        };
      }

      // Updating cached entity
      return {
        key: newEntity.key,
        timestamp: now,
        version: localEntity.version + 1,
        synced: localEntity.synced,
        isRemoved: false,
        value: newEntity.value,
      };
    });

    // Cache data locally
    this.localDB.writeBatch(updatedLocalEntities);

    this.triggerSync();
  }

  public removeBatch(keys: string[]): void {
    this.localDB.removeBatch(keys, false);
    this.triggerSync();
  }

  public triggerSync(): void {
    if (!this.isSyncEnabled) {
      return;
    }
    // Trigger sync, but ensure syncs are run in sequence awaiting previous sync
    this.syncPromise = this.syncPromise.then(async () => {
      await this.syncLocalAndRemote();
    });
  }

  public async syncLocalAndRemote(): Promise<void> {
    console.log("syncLocalAndRemote");

    const syncKeys: string[] = [...this.monitorKeys];

    const unSyncedKeys = this.localDB.getUnsyncedKeys();
    syncKeys.push(...unSyncedKeys);

    let preLocalEntities = this.localDB.tryReadBatch(syncKeys);

    const queries = preLocalEntities
      .filter((entity) => !isError(entity))
      .map((entity, index) => {
        if (!isError(entity) && entity.synced) {
          // Local entity exists and is synced, skip retrieving remote if not changed
          return { key: syncKeys[index], IfNoneMatch: entity.synced };
        }

        // Get entity regardless of matched timestamp
        return { key: syncKeys[index] };
      });

    if (!queries.length) {
      console.log("Nothing to sync");
      return;
    }

    const currentRemoteEntities = await this.remoteDB.tryReadBatch(queries);
    const currentLocalEntities = this.localDB.tryReadBatch(syncKeys);

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
        if (this.monitorKeys.includes(currentLocalEntity.key)) {
          // Monitored entities update local even if local has not changed
          remoteToLocalEntities.push(remoteEntity);
          // Signal updated local entity by remote entity !!!!
        }

        return;
      }

      // Both local and remote entity has been changed by some other client, lets merge the entities
      mergedEntities.push(this.onConflict(currentLocalEntity, remoteEntity));
    });

    // Convert remote entity to LocalEntity with synced= 'remote timestamp'
    const localEntitiesToUpdate: LocalEntity[] = remoteToLocalEntities.map(
      (remoteEntity) => ({
        key: remoteEntity.key,
        timestamp: remoteEntity.timestamp,
        version: remoteEntity.version,
        synced: remoteEntity.timestamp,
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

  private async uploadEntities(entities: RemoteEntity[]): Promise<void> {
    if (!entities.length) {
      // No entities to upload
      return;
    }

    const response = await this.remoteDB.writeBatch(entities);
    if (isError(response)) {
      // Signal sync error !!!!!!!
      console.warn("Sync error while writing entities");
      return;
    }

    // Set  existing local items with synced timestamp
    const keys = entities.map((entity) => entity.key);

    // Remember sync timestamps for uploaded entities
    const syncedItems = new Map<string, number>();
    entities.forEach((entity) => syncedItems.set(entity.key, entity.timestamp));

    // Update local entities with synced timestamps
    const localEntities = this.localDB
      .tryReadBatch(keys)
      .filter((r) => !isError(r)) as LocalEntity[];
    const syncedLocalEntities = localEntities.map((entity) => ({
      ...entity,
      synced: syncedItems.get(entity.key) ?? 0,
    }));

    this.localDB.writeBatch(syncedLocalEntities);
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
      value: remoteEntity.value,
    };
    this.localDB.write(entity);
  }
}
