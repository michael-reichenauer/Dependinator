import { ILocalDB, ILocalDBKey, LocalEntity } from "./LocalDB";
import {
  IRemoteDB,
  IRemoteDBKey,
  NotModifiedError,
  Query,
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

export const IStoreDBKey = diKey<IStoreDB>();
export interface IStoreDB {
  configure(isSyncEnabled: boolean): void;
  initialize(
    onConflict: OnConflict,
    onRemoteChanged: () => void,
    onSyncChanged: (connected: boolean) => void
  ): void;
  monitorRemoteEntities(keys: string[]): void;
  readLocal<T>(key: string, defaultValue: T): T;
  tryReadLocalThenRemoteAsync<T>(key: string): Promise<Result<T>>;
  writeBatch(entities: Entity[]): void;
  removeBatch(keys: string[]): void;
}

const autoSyncInterval = 30000;

@singleton(IStoreDBKey)
export class StoreDB implements IStoreDB {
  private isSyncEnabled: boolean = false;
  private syncPromise = Promise.resolve();
  private isConnected: boolean = true;
  private syncedTimestamp: number = 0;
  private monitorKeys: string[] = [];
  private onConflict: OnConflict = (): any => {};
  private onRemoteChanged: () => void = () => {};
  private onSyncChanged: (connected: boolean) => void = () => {};

  constructor(
    private localDB: ILocalDB = di(ILocalDBKey),
    private remoteDB: IRemoteDB = di(IRemoteDBKey)
  ) {}

  initialize(
    onConflict: OnConflict,
    onRemoteChanged: () => void,
    onSyncChanged: (connected: boolean) => void
  ): void {
    this.onConflict = onConflict;
    this.onRemoteChanged = onRemoteChanged;
    this.onSyncChanged = onSyncChanged;
  }

  monitorRemoteEntities(keys: string[]): void {
    this.monitorKeys = [...keys];
  }

  configure(isSyncEnabled: boolean): void {
    this.isSyncEnabled = isSyncEnabled;
    console.log("Syncing enabled:", isSyncEnabled);

    if (isSyncEnabled) {
      this.triggerSync();
      setTimeout(() => this.autoSync(), autoSyncInterval);
    }
  }

  public readLocal<T>(key: string, defaultValue: T): T {
    const localValue = this.localDB.tryReadValue<T>(key);
    if (isError(localValue)) {
      // Entity not cached locally, lets cache default value for future access
      this.cacheLocalValue(key, defaultValue);
      return defaultValue;
    }

    return localValue;
  }

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

  private triggerSync(): void {
    if (!this.isSyncEnabled) {
      return;
    }

    // Trigger sync, but ensure syncs are run in sequence awaiting previous sync
    this.syncPromise = this.syncPromise.then(async () => {
      await this.syncLocalAndRemote();
      this.syncedTimestamp = Date.now();
    });
  }

  private autoSync() {
    if (!this.isSyncEnabled) {
      return;
    }

    const delay = Math.max(0, Date.now() - this.syncedTimestamp);
    if (delay < autoSyncInterval - 100) {
      // Not yet time to sync, schedule recheck after some time
      setTimeout(() => this.autoSync(), autoSyncInterval - delay);
      return;
    }

    // Time to auto sync and schedule next auto sync
    this.triggerSync();
    setTimeout(() => this.autoSync(), autoSyncInterval);
  }

  private async syncLocalAndRemote(): Promise<void> {
    console.log("Syncing ...");

    // Always syncing monitored entities and unsynced local entities
    let syncKeys: string[] = [...this.monitorKeys];
    syncKeys = this.addUnsyncedLocalKeys(syncKeys);

    // Generating query, which is key and timestamp to skip known synced remote entities
    const queries = this.makeRemoteQueries(syncKeys, localEntities);
    if (!queries.length) {
      console.log("Nothing to sync");
      return;
    }

    // Getting remote entities to compare with local entities later
    const remoteEntities = await this.remoteDB.tryReadBatch(queries);
    if (isError(remoteEntities)) {
      // Failed to connect to remote server
      this.setIsConnected(false);
      return;
    }

    this.setIsConnected(true);
    const currentLocalEntities = this.localDB.tryReadBatch(syncKeys);

    const remoteToLocalEntities: RemoteEntity[] = [];
    const localToRemoteEntities: LocalEntity[] = [];
    const mergedEntities: LocalEntity[] = [];

    remoteEntities.forEach((remoteEntity, index) => {
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

    console.log(
      `Synced to local: ${localEntitiesToUpdate.length}, to remote: ${remoteEntitiesToUpload.length}`
    );

    // Cache local entities
    this.localDB.writeBatch(localEntitiesToUpdate);
    if (localEntitiesToUpdate.length > 0) {
      console.log("Remote updated local entities !!!");
      setTimeout(() => this.onRemoteChanged(), 0);
    }

    await this.uploadEntities(remoteEntitiesToUpload);

    await this.syncRemovedEntities();
  }

  private makeRemoteQueries(syncKeys: string[]): Query[] {
    const localEntities = this.localDB.tryReadBatch(syncKeys);

    return localEntities
      .filter((entity) => !isError(entity))
      .map((entity, index) => {
        if (!isError(entity) && entity.synced) {
          // Local entity exists and is synced, skip retrieving remote if not changed
          return { key: syncKeys[index], IfNoneMatch: entity.synced };
        }

        // Get entity regardless of matched timestamp
        return { key: syncKeys[index] };
      });
  }

  private addUnsyncedLocalKeys(syncKeys: string[]): string[] {
    const unSyncedKeys = this.localDB
      .getUnsyncedKeys()
      .filter((key) => !syncKeys.includes(key));
    return syncKeys.concat(unSyncedKeys);
  }

  private setIsConnected(isConnected: boolean): void {
    if (isConnected !== this.isConnected) {
      this.isConnected = isConnected;
      this.onSyncChanged(isConnected);
    }
  }

  private async uploadEntities(entities: RemoteEntity[]): Promise<void> {
    if (!entities.length) {
      // No entities to upload
      return;
    }

    const response = await this.remoteDB.writeBatch(entities);
    if (isError(response)) {
      this.setIsConnected(false);
      return;
    }

    this.setIsConnected(true);
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
    if (removedKeys.length === 0) {
      return;
    }
    const response = await this.remoteDB.removeBatch(removedKeys);
    if (isError(response)) {
      this.setIsConnected(false);
      return;
    }
    this.setIsConnected(true);
    console.log(`Remove confirmed of ${removedKeys.length} entities`);
    this.localDB.confirmRemoved(removedKeys);
  }

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

  private async tryReadRemote<T>(key: string): Promise<Result<T>> {
    // Entity not cached locally, lets try get from remote location
    if (!this.isSyncEnabled) {
      return new RangeError(`Local key ${key} not found`);
    }

    const remoteEntities = await this.remoteDB.tryReadBatch([{ key: key }]);
    if (isError(remoteEntities)) {
      this.setIsConnected(false);
      return remoteEntities;
    }

    this.setIsConnected(true);
    const remoteEntity = remoteEntities[0];
    if (isError(remoteEntity)) {
      return remoteEntity;
    }

    // Cache remote data locally as synced
    this.cacheRemoteEntity(remoteEntity);
    return remoteEntity.value;
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
