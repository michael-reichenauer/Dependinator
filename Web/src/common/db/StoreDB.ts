import { ILocalDB, ILocalDBKey, LocalEntity } from "./LocalDB";
import {
  IRemoteDB,
  IRemoteDBKey,
  NotModifiedError,
  RemoteEntity,
} from "./RemoteDB";
import Result, { isError } from "../Result";
import { di, diKey, singleton } from "../di";
import { Query } from "../Api";

export interface Entity {
  key: string;
  value: any;
}

export interface Configuration {
  onConflict: (local: LocalEntity, remote: RemoteEntity) => LocalEntity;
  onRemoteChanged: () => void;
  onSyncChanged: (isOK: boolean) => void;
  isSyncEnabled: boolean;
}

// Key-value database, that syncs locally stored entities with a remote server
export const IStoreDBKey = diKey<IStoreDB>();
export interface IStoreDB {
  configure(options: Partial<Configuration>): void;
  monitorRemoteEntities(keys: string[]): void;
  readLocal<T>(key: string, defaultValue: T): T;
  tryReadLocalThenRemoteAsync<T>(key: string): Promise<Result<T>>;
  writeBatch(entities: Entity[]): void;
  removeBatch(keys: string[]): void;
  triggerSync(): Promise<Result<void>>;
}

const autoSyncInterval = 30000;

@singleton(IStoreDBKey)
export class StoreDB implements IStoreDB {
  private syncPromise = Promise.resolve<Result<void>>(undefined);
  private isSyncOK: boolean = true;
  private autoSyncTimeoutId: any = null;
  private monitorKeys: string[] = [];
  private configuration: Configuration = {
    isSyncEnabled: false,
    onConflict: (l, r) => l,
    onSyncChanged: () => {},
    onRemoteChanged: () => {},
  };

  constructor(
    private localDB: ILocalDB = di(ILocalDBKey),
    private remoteDB: IRemoteDB = di(IRemoteDBKey)
  ) {}

  // Called when remote entities should be monitored for changed by other clients
  public monitorRemoteEntities(keys: string[]): void {
    this.monitorKeys = [...keys];
  }

  public configure(configuration: Partial<Configuration>): void {
    if (
      configuration.isSyncEnabled !== undefined &&
      configuration.isSyncEnabled !== this.configuration.isSyncEnabled
    ) {
      console.log("Syncing - isSyncEnabled =", configuration.isSyncEnabled);
    }

    this.configuration = { ...this.configuration, ...configuration };
  }

  // Reads local value or returns default value, (caches default value to be used for next access)
  public readLocal<T>(key: string, defaultValue: T): T {
    const localValue = this.localDB.tryReadValue<T>(key);
    if (isError(localValue)) {
      // Entity not cached locally, lets cache default value for future access
      this.cacheLocalValue(key, defaultValue);
      return defaultValue;
    }

    return localValue;
  }

  // First tries to get local entity, but if not exists locally, then retrieving form remote server is tried
  public async tryReadLocalThenRemoteAsync<T>(key: string): Promise<Result<T>> {
    const localValue = this.localDB.tryReadValue<T>(key);
    if (isError(localValue)) {
      // Entity not cached locally, lets try get from remote location
      return this.tryReadRemote(key);
    }

    return localValue;
  }

  public writeBatch(entities: Entity[]): void {
    const keys = entities.map((entity) => entity.key);
    const now = Date.now();

    const localEntities = this.localDB.tryReadBatch(keys);

    // Updating current local entities with new data and setting timestamp and increase version
    const updatedEntities = entities.map((newEntity, index) => {
      const localEntity = localEntities[index];
      if (isError(localEntity)) {
        // First version of local entity (not yet cached)
        return {
          key: newEntity.key,
          timestamp: now,
          version: 1,
          synced: 0,
          value: newEntity.value,
        };
      }

      // Updating cached local entity with new value, timestamp and version
      return {
        key: newEntity.key,
        timestamp: now,
        version: localEntity.version + 1,
        synced: localEntity.synced,
        value: newEntity.value,
      };
    });

    // Update local entities
    this.localDB.writeBatch(updatedEntities);

    this.triggerSync();
  }

  public removeBatch(keys: string[]): void {
    this.localDB.removeBatch(keys, false);
    this.triggerSync();
  }

  // Called to trigger a sync, which are done in sequence (not parallel)
  public async triggerSync(): Promise<Result<void>> {
    if (!this.configuration.isSyncEnabled) {
      clearTimeout(this.autoSyncTimeoutId);
      return;
    }

    // Trigger sync, but ensure syncs are run in sequence awaiting previous sync
    this.syncPromise = this.syncPromise.then(async () => {
      clearTimeout(this.autoSyncTimeoutId);
      const syncResult = await this.syncLocalAndRemote();

      this.autoSyncTimeoutId = setTimeout(
        () => this.triggerSync(),
        autoSyncInterval
      );
      return syncResult;
    });

    return await this.syncPromise;
  }

  // // Scheduled to be called regularly to trigger sync with remote server
  // private autoSync() {
  //   if (!this.configuration.isSyncEnabled) {
  //     return;
  //   }

  //   const delay = Math.max(0, Date.now() - this.syncedTimestamp);
  //   if (delay < autoSyncInterval - 100) {
  //     // Not yet time to sync, schedule recheck after some time
  //     setTimeout(() => this.autoSync(), autoSyncInterval - delay);
  //     return;
  //   }

  //   // Time to auto sync and schedule next auto sync
  //   this.triggerSync();
  //   setTimeout(() => this.autoSync(), autoSyncInterval);
  // }

  // Syncs local and remote entities by retrieving changed remote entities and compare with
  // stored local entities.
  // If remote has changed but not local, then local is updated
  // If local has changed but not remote, then entity is uploaded to remote,
  // If both local and remote has changed, the conflict is resolved and both local and remote are updated
  // Removed entities are synced as well
  private async syncLocalAndRemote(): Promise<Result<void>> {
    if (!this.configuration.isSyncEnabled) {
      return;
    }

    console.log("Syncing ...");

    // Always syncing monitored entities and unsynced local entities
    let syncKeys: string[] = [...this.monitorKeys];
    syncKeys = this.addUnsyncedLocalKeys(syncKeys);

    // Generating query, which is key and timestamp to skip known unchanged remote entities
    const queries = this.makeRemoteQueries(syncKeys);
    if (!queries.length) {
      console.log("Nothing to sync");
      return;
    }

    // Getting remote entities to compare with local entities
    const remoteEntities = await this.remoteDB.tryReadBatch(queries);
    if (isError(remoteEntities)) {
      // Failed to connect to remote server
      this.setSyncStatus(false);
      return remoteEntities;
    }

    this.setSyncStatus(true);
    const localEntities = this.localDB.tryReadBatch(syncKeys);

    const remoteToLocal: RemoteEntity[] = [];
    const localToRemote: LocalEntity[] = [];
    const mergedEntities: LocalEntity[] = [];

    remoteEntities.forEach((remoteEntity, index) => {
      const localEntity = localEntities[index];

      if (isError(localEntity)) {
        // Local entity is missing, skip sync
        return;
      }

      if (remoteEntity instanceof NotModifiedError) {
        // Remote entity was not changed since last sync, lets upload local to remote if local has changed
        if (localEntity.synced !== localEntity.timestamp) {
          localToRemote.push(localEntity);
        }
        return;
      }

      if (isError(remoteEntity)) {
        // Remote entity is missing, lets upload local to remote
        localToRemote.push(localEntity);
        return;
      }

      if (localEntity.timestamp === remoteEntity.timestamp) {
        // Both local and remote entity have same timestamp, already same (nothing to sync)
        return;
      }

      if (localEntity.synced === remoteEntity.timestamp) {
        // Local entity has changed and remote entity same as uploaded previously by this client,
        // lets upload local to remote
        localToRemote.push(localEntity);
        return;
      }

      if (localEntity.synced === localEntity.timestamp) {
        // Local entity has not changed, while remote has been changed by some other client,
        // lets cache the updated remote entity if the entity is actively monitored
        if (this.monitorKeys.includes(localEntity.key)) {
          remoteToLocal.push(remoteEntity);
        }
        return;
      }

      // Local entity was chanced by this client and remote entity wad changed by some other client,
      // lets merge the entities by resolving the conflict
      const resolvedEntity = this.configuration.onConflict(
        localEntity,
        remoteEntity
      );
      mergedEntities.push(resolvedEntity);
    });

    // Convert remote entity to LocalEntity with synced= 'remote timestamp'
    const localToUpdate = this.convertRemoteToLocal(remoteToLocal);

    // Convert local entity to remote entity to be uploaded
    const remoteToUpload = this.convertLocalToRemote(localToRemote);

    // Add merged entity to both local and to be uploaded to remote
    this.addMergedEntities(mergedEntities, localToUpdate, remoteToUpload);

    console.log(
      `Synced to local: ${localToUpdate.length}, to remote: ${remoteToUpload.length}`
    );

    this.updateLocalEntities(localToUpdate);

    const uploadResult = await this.uploadEntities(remoteToUpload);
    if (isError(uploadResult)) {
      return uploadResult;
    }

    const removeResult = await this.syncRemovedEntities();
    if (isError(removeResult)) {
      return removeResult;
    }
  }

  // Entities merged by a conflict needs to update both local and remote
  private addMergedEntities(
    merged: LocalEntity[],
    toLocal: LocalEntity[],
    toRemote: RemoteEntity[]
  ) {
    const now = Date.now();
    merged.forEach((mergedEntity) => {
      toLocal.push({
        key: mergedEntity.key,
        timestamp: now,
        version: mergedEntity.version + 1,
        synced: mergedEntity.synced,
        value: mergedEntity.value,
      });
      toRemote.push({
        key: mergedEntity.key,
        timestamp: now,
        version: mergedEntity.version + 1,
        value: mergedEntity.value,
      });
    });
  }

  private updateLocalEntities(localToUpdate: LocalEntity[]): void {
    this.localDB.writeBatch(localToUpdate);

    if (localToUpdate.length > 0) {
      // Signal that local entities where changed during sync so main app can reload ui
      console.log("Remote entity updated local entities");
      setTimeout(() => this.configuration.onRemoteChanged(), 0);
    }
  }

  private convertRemoteToLocal(remoteEntities: RemoteEntity[]): LocalEntity[] {
    return remoteEntities.map((remoteEntity) => ({
      key: remoteEntity.key,
      timestamp: remoteEntity.timestamp,
      version: remoteEntity.version,
      synced: remoteEntity.timestamp,
      value: remoteEntity.value,
    }));
  }

  private convertLocalToRemote(localEntities: LocalEntity[]): RemoteEntity[] {
    return localEntities.map((localEntity) => ({
      key: localEntity.key,
      timestamp: localEntity.timestamp,
      version: localEntity.version,
      value: localEntity.value,
    }));
  }

  // Creates queries used when retrieving remote entities while syncing. But specifying
  // IfNoneMatch property to exclude already known unchanged remote entities (synced by this client)
  private makeRemoteQueries(syncKeys: string[]): Query[] {
    const localEntities = this.localDB.tryReadBatch(syncKeys);

    // creating queries based on key and synced timestamps to skip already known unchanged remote entities
    return localEntities
      .filter((entity) => !isError(entity))
      .map((entity, index) => {
        if (!isError(entity) && entity.synced) {
          // Local entity exists and is synced, skip retrieving remote if not changed since last sync
          return { key: syncKeys[index], IfNoneMatch: entity.synced };
        }

        // Get entity regardless of matched timestamp
        return { key: syncKeys[index] };
      });
  }

  // Add keys of unsynced entities
  private addUnsyncedLocalKeys(syncKeys: string[]): string[] {
    const unSyncedKeys = this.localDB
      .getUnsyncedKeys()
      .filter((key) => !syncKeys.includes(key));
    return syncKeys.concat(unSyncedKeys);
  }

  // Signal is connected changes (called when remote connections succeeds or fails)
  private setSyncStatus(isOK: boolean): void {
    if (isOK !== this.isSyncOK) {
      this.isSyncOK = isOK;
      this.configuration.onSyncChanged(isOK);
    }
  }

  // Upload remote entities to server. If ok, then local entities are marked as synced
  private async uploadEntities(
    entities: RemoteEntity[]
  ): Promise<Result<void>> {
    if (!entities.length) {
      // No entities to upload
      return;
    }

    const response = await this.remoteDB.writeBatch(entities);
    if (isError(response)) {
      this.setSyncStatus(false);
      return response;
    }

    this.setSyncStatus(true);

    // Remember sync timestamps for uploaded entities
    const syncedItems = new Map<string, number>();
    entities.forEach((entity) => syncedItems.set(entity.key, entity.timestamp));

    // Update local entities with synced timestamps
    const keys = entities.map((entity) => entity.key);
    const localEntities = this.localDB
      .tryReadBatch(keys)
      .filter((r) => !isError(r)) as LocalEntity[];
    const syncedLocalEntities = localEntities.map((entity) => ({
      ...entity,
      synced: syncedItems.get(entity.key) ?? 0,
    }));

    this.localDB.writeBatch(syncedLocalEntities);
  }

  // Syncing removed local entities to be removed on remote server as well, if ok, then
  // local removed values are marked as confirmed removed (i.e. synced)
  private async syncRemovedEntities(): Promise<Result<void>> {
    const removedKeys = this.localDB.getRemovedKeys();
    if (removedKeys.length === 0) {
      return;
    }

    const response = await this.remoteDB.removeBatch(removedKeys);
    if (isError(response)) {
      this.setSyncStatus(false);
      return response;
    }
    this.setSyncStatus(true);

    this.localDB.confirmRemoved(removedKeys);
    console.log(`Remove confirmed of ${removedKeys.length} entities`);
  }

  // Trying to read a remote value, if ok, the caching it locally as well
  private async tryReadRemote<T>(key: string): Promise<Result<T>> {
    // Entity not cached locally, lets try get from remote location
    if (!this.configuration.isSyncEnabled) {
      // Since sync is disabled, the trying to read fails with not found error
      return new RangeError(`Local key ${key} not found`);
    }

    const remoteEntities = await this.remoteDB.tryReadBatch([{ key: key }]);
    if (isError(remoteEntities)) {
      this.setSyncStatus(false);
      return remoteEntities;
    }

    this.setSyncStatus(true);
    const remoteEntity = remoteEntities[0];
    if (isError(remoteEntity)) {
      return remoteEntity;
    }

    // Cache remote data locally as synced
    this.cacheRemoteEntity(remoteEntity);
    return remoteEntity.value;
  }

  // Caching a local value when storing a local value for the first time
  private cacheLocalValue<T>(key: string, value: T) {
    const entity: LocalEntity = {
      key: key,
      timestamp: Date.now(),
      version: 1,
      synced: 0,
      value: value,
    };

    this.localDB.write(entity);
  }

  // Read a remote value and now caching it locally a well
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
