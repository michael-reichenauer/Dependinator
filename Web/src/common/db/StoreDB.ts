import { ILocalDB, ILocalDBKey, LocalEntity } from "./LocalDB";
import {
  IRemoteDB,
  IRemoteDBKey,
  isNetworkError,
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
  private syncPromise = Promise.resolve();
  private isSyncEnabled: boolean = false;
  private isConnected: boolean = true;
  private syncedTime: number = 0;
  private onConflict: OnConflict = (): any => {};
  private monitorKeys: string[] = [];
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
    console.log("Syncing:", isSyncEnabled);

    if (isSyncEnabled) {
      this.triggerSync();
      setTimeout(() => this.autoSync(), autoSyncInterval);
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
        this.setIsConnected(false);
        return new RangeError(`id ${key} not found,` + remoteEntity);
      }

      // Cache remote data locally as synced
      this.setIsConnected(true);
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
      this.syncedTime = Date.now();
    });
  }

  private autoSync() {
    if (!this.isSyncEnabled) {
      return;
    }

    const delay = Math.max(0, Date.now() - this.syncedTime);
    if (delay < autoSyncInterval) {
      setTimeout(() => this.autoSync(), autoSyncInterval - delay + 1000);
      return;
    }

    // Time to auto sync and schedule next auto sync
    this.triggerSync();
    setTimeout(() => this.autoSync(), autoSyncInterval);
  }

  private async syncLocalAndRemote(): Promise<void> {
    console.log("Syncing ...");

    const syncKeys: string[] = [...this.monitorKeys];

    const unSyncedKeys = this.localDB
      .getUnsyncedKeys()
      .filter((key) => !syncKeys.includes(key));
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
    if (isNetworkError(currentRemoteEntities)) {
      this.setIsConnected(false);
      return;
    }
    this.setIsConnected(true);
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
