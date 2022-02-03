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
import assert from "assert";

export interface Entity {
  key: string;
  value: any;
}

export type mergedType = "local" | "remote" | "merged";

export interface MergeEntity {
  key: string;
  mergedType: mergedType;
  value: any;
  version: number;
}

export interface MergedEntity extends MergeEntity {
  mergedType: mergedType;
  localEtag: string;
  remoteEtag: string;
  syncedEtag: string;
}

export interface Configuration {
  onConflict: (local: LocalEntity, remote: RemoteEntity) => MergeEntity;
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
    onConflict: (l, r) => ({ ...l, mergedType: "local" }),
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
    this.logConfig(configuration);

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
    const etag = this.generateEtag();

    const localEntities = this.localDB.tryReadBatch(keys);

    // Updating current local entities with new data and setting timestamp and increase version
    const updatedEntities = entities.map((newEntity, index): LocalEntity => {
      const localEntity = localEntities[index];
      if (isError(localEntity)) {
        // First initial version of local entity
        return {
          key: newEntity.key,
          etag: etag,
          syncedEtag: "",
          remoteEtag: "",
          value: newEntity.value,
          version: 1,
        };
      }

      // Updating cached local entity with new value, etag and version
      return {
        key: newEntity.key,
        etag: etag,
        syncedEtag: localEntity.syncedEtag,
        remoteEtag: localEntity.remoteEtag,
        value: newEntity.value,
        version: localEntity.version + 1,
      };
    });

    // Update local entities
    this.localDB.writeBatch(updatedEntities);

    this.triggerSync();
  }

  public removeBatch(keys: string[]): void {
    this.localDB.preRemoveBatch(keys);
    if (!this.configuration.isSyncEnabled) {
      // If not sync is enabled, the entities are just remove locally
      this.localDB.confirmRemoved(keys);
    }
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

    // console.log("Syncing ...");

    // Always syncing monitored entities and unsynced local entities
    const unSyncedKeys = this.localDB.getUnsyncedKeys();
    let syncKeys: string[] = [...this.monitorKeys];
    syncKeys = this.addKeys(syncKeys, unSyncedKeys);

    // Generating query, which is key and remote etag to skip known not modified remote entities
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
    const mergedEntities: MergedEntity[] = [];

    remoteEntities.forEach((remoteEntity, index) => {
      const localEntity = localEntities[index];

      // console.log("sync:", lx(localEntity), lx(remoteEntity));
      if (isError(localEntity)) {
        // Local entity is missing, skip sync
        return;
      }

      if (remoteEntity instanceof NotModifiedError) {
        // Remote entity was not changed since last sync,
        if (localEntity.etag !== localEntity.syncedEtag) {
          // local has changed since last upload
          localToRemote.push(localEntity);
        }
        return;
      }

      if (isError(remoteEntity)) {
        // Remote entity is missing, lets upload local to remote
        localToRemote.push(localEntity);
        return;
      }

      assert(localEntity.key === remoteEntity.key, "Not same entity");

      if (
        localEntity.etag === localEntity.syncedEtag &&
        localEntity.remoteEtag === remoteEntity.etag
      ) {
        // Local entity has not changed and local entity is same as remote entity (nothing to sync)
        return;
      }

      if (
        localEntity.etag === localEntity.syncedEtag &&
        localEntity.remoteEtag !== remoteEntity.etag
      ) {
        // Local entity has not changed, while remote has been changed by some other client,
        // lets update local entity if the entity is actively monitored
        if (this.monitorKeys.includes(localEntity.key)) {
          remoteToLocal.push(remoteEntity);
        }
        return;
      }

      if (
        localEntity.etag !== localEntity.syncedEtag &&
        localEntity.remoteEtag === remoteEntity.etag
      ) {
        // Local entity has changed and remote entity same as uploaded previously by this client,
        // lets upload local to remote
        localToRemote.push(localEntity);
        return;
      }

      // Local entity was chanced by this client and remote entity wad changed by some other client,
      // lets merge the entities by resolving the conflict
      const mergeEntity = this.configuration.onConflict(
        localEntity,
        remoteEntity
      );
      const mergedEntity: MergedEntity = {
        ...mergeEntity,
        localEtag: localEntity.etag,
        syncedEtag: localEntity.syncedEtag,
        remoteEtag: remoteEntity.etag,
      };
      mergedEntities.push(mergedEntity);
    });

    // Convert remote entity to LocalEntity with synced= 'remote timestamp'
    const localToUpdate = this.convertRemoteToLocal(remoteToLocal);

    // Convert local entity to remote entity to be uploaded
    const remoteToUpload = this.convertLocalToRemote(localToRemote);

    // Add merged entity to both local and to be uploaded to remote
    this.addMergedEntities(mergedEntities, localToUpdate, remoteToUpload);

    console.log(
      `Synced to local: ${localToUpdate.length}, to remote: ${remoteToUpload.length}, (merged: ${mergedEntities.length})`
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

  private convertRemoteToLocal(remoteEntities: RemoteEntity[]): LocalEntity[] {
    const etag = this.generateEtag();
    return remoteEntities.map(
      (remoteEntity): LocalEntity => ({
        key: remoteEntity.key,
        etag: etag,
        syncedEtag: etag,
        remoteEtag: remoteEntity.etag,
        value: remoteEntity.value,
        version: remoteEntity.version,
      })
    );
  }

  private convertLocalToRemote(localEntities: LocalEntity[]): RemoteEntity[] {
    return localEntities.map(
      (localEntity): RemoteEntity => ({
        key: localEntity.key,
        etag: localEntity.remoteEtag,
        localEtag: localEntity.etag,
        value: localEntity.value,
        version: localEntity.version,
      })
    );
  }

  // Entities merged by a conflict needs to update both local and remote
  private addMergedEntities(
    merged: MergedEntity[],
    toLocal: LocalEntity[],
    toRemote: RemoteEntity[]
  ) {
    const etag = this.generateEtag();
    merged.forEach((mergedEntity) => {
      toLocal.push({
        key: mergedEntity.key,
        etag: etag,
        syncedEtag: mergedEntity.syncedEtag,
        remoteEtag: mergedEntity.remoteEtag,
        value: mergedEntity.value,
        version: mergedEntity.version + 1,
      });
      toRemote.push({
        key: mergedEntity.key,
        etag: mergedEntity.remoteEtag,
        localEtag: etag,
        value: mergedEntity.value,
        version: mergedEntity.version + 1,
      });
    });
  }

  private updateLocalEntities(localToUpdate: LocalEntity[]): void {
    if (!localToUpdate.length) {
      // No entities to update
    }

    this.localDB.writeBatch(localToUpdate);

    if (localToUpdate.length > 0) {
      // Signal that local entities where changed during sync so main app can reload ui
      console.log("Remote entity updated local entities");
      setTimeout(() => this.configuration.onRemoteChanged(), 0);
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

    const responses = await this.remoteDB.writeBatch(entities);
    if (isError(responses)) {
      this.setSyncStatus(false);
      return responses;
    }

    this.setSyncStatus(true);

    // Remember etags for uploaded entities
    const uppLoadedEtags = new Map<string, string>();
    responses.forEach((rsp) => {
      if (rsp.etag) {
        uppLoadedEtags.set(rsp.key, rsp.etag);
      }
    });

    const syncingEtags = new Map<string, string>();
    entities.forEach((entity) => {
      if (entity.localEtag) {
        syncingEtags.set(entity.key, entity.localEtag);
      }
    });

    // Get local entities that correspond to the uploaded entities (skip just removed entities)
    const keys = entities.map((entity) => entity.key);
    const localEntities = this.localDB
      .tryReadBatch(keys)
      .filter((r) => !isError(r)) as LocalEntity[];

    // Update local entities with syncedEtags and remote server etags
    const syncedLocalEntities = localEntities.map(
      (entity): LocalEntity => ({
        ...entity,
        remoteEtag: uppLoadedEtags.get(entity.key) ?? "",
        syncedEtag: syncingEtags.get(entity.key) ?? "",
      })
    );

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

  // Creates queries used when retrieving remote entities while syncing. But specifying
  // IfNoneMatch property to exclude already known unchanged remote entities (synced by this client)
  private makeRemoteQueries(syncKeys: string[]): Query[] {
    const localEntities = this.localDB.tryReadBatch(syncKeys);

    // creating queries based on key and synced timestamps to skip already known unchanged remote entities
    return localEntities
      .filter((entity) => !isError(entity))
      .map((entity, index) => {
        if (isError(entity) || !entity.remoteEtag) {
          // Local entity does not exist of has not been synced, get entity regardless of matched timestamp
          return { key: syncKeys[index] };
        }

        // Local entity exists and is synced, skip retrieving remote if not changed since last sync
        return { key: syncKeys[index], IfNoneMatch: entity.remoteEtag };
      });
  }

  // Add keys of unsynced entities
  private addKeys(syncKeys: string[], keys: string[]): string[] {
    const addingKeys = keys.filter((key) => !syncKeys.includes(key));
    return syncKeys.concat(addingKeys);
  }

  // Signal is connected changes (called when remote connections succeeds or fails)
  private setSyncStatus(isOK: boolean): void {
    if (isOK !== this.isSyncOK) {
      this.isSyncOK = isOK;
      this.configuration.onSyncChanged(isOK);
    }
  }

  // Caching a local value when storing a local value for the first time
  private cacheLocalValue<T>(key: string, value: T) {
    const entity: LocalEntity = {
      key: key,
      etag: this.generateEtag(),
      syncedEtag: "",
      remoteEtag: "",
      value: value,
      version: 1,
    };

    this.localDB.write(entity);
  }

  // Read a remote value and now caching it locally a well
  private cacheRemoteEntity(remoteEntity: RemoteEntity) {
    const etag = this.generateEtag();
    const entity: LocalEntity = {
      key: remoteEntity.key,
      etag: etag,
      syncedEtag: etag,
      remoteEtag: remoteEntity.etag,
      value: remoteEntity.value,
      version: remoteEntity.version,
    };

    this.localDB.write(entity);
  }

  private generateEtag(): string {
    return `W/"datetime'${new Date().toISOString()}'"`.replace(/:/g, "%3A");
  }

  private logConfig(configuration: Partial<Configuration>) {
    if (
      configuration.isSyncEnabled !== undefined &&
      configuration.isSyncEnabled !== this.configuration.isSyncEnabled
    ) {
      console.log("Syncing - isSyncEnabled =", configuration.isSyncEnabled);
    }
  }
}
