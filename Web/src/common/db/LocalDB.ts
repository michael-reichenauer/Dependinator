import Result, { isError, orDefault } from "../Result";
import { di, diKey, singleton } from "../di";
import { ILocalStore, ILocalStoreKey } from "../LocalStore";

// LocalEntity is the entity stored in the local device store and corresponds
// to the RemoteEntity, which is stored in a remote cloud server and synced
export interface LocalEntity {
  key: string; // Entity key (same local as remote)
  etag: string; // local etag set when updating local entity
  syncedEtag: string; // local entity when last sync was done, sync is needed if not same as etag
  remoteEtag: string; // remote server etag when last sync was done.

  value: any;
  version: number;
}

// The local db interface, which is used by StoreDB to sync between local and remote
export const ILocalDBKey = diKey<ILocalDB>();
export interface ILocalDB {
  tryReadValue<T>(key: string): Result<T>;
  tryReadBatch(keys: string[]): Result<LocalEntity>[];
  write(entity: LocalEntity): void;
  writeBatch(entities: LocalEntity[]): void;
  preRemoveBatch(keys: string[]): void; // Called when local entity removed, call confirmRemoved after sync
  confirmRemoved(keys: string[]): void; // Called after sync of removed entities
  getUnsyncedKeys(): string[]; // Get all entity keys that need sync (etag!=syncedEtag)
  getAllEntities(): LocalEntity[];
  getRemovedKeys(): string[]; // Get all removed entity keys, which have not yet been confirmed
  clear(): void;
}

const removedKey = "db_.removedKeys"; // Key to store removed entities that are not yet synced upp
const localKeyPrefix = "db."; // key prefix for local db entities

@singleton(ILocalDBKey)
export class LocalDB implements ILocalDB {
  constructor(private localStore: ILocalStore = di(ILocalStoreKey)) {}

  public tryReadValue<T>(key: string): Result<T> {
    const entity = this.tryReadBatch([key])[0];
    if (isError(entity)) {
      return entity;
    }

    return entity.value;
  }

  public tryReadBatch(keys: string[]): Result<LocalEntity>[] {
    const localKeys = this.toLocalKeys(keys);
    return this.localStore.tryReadBatch(localKeys);
  }

  public write(entity: LocalEntity): void {
    this.writeBatch([entity]);
  }

  public writeBatch(entities: LocalEntity[]): void {
    const localEntities = entities.map((entity) => ({
      key: this.toLocalKey(entity.key),
      value: entity,
    }));
    this.localStore.writeBatch(localEntities);

    // Ensure possible previously removed keys are no longer considered removed
    const keys = entities.map((entity) => entity.key);
    this.confirmRemoved(keys);
  }

  // Called when removing local entities. Call confirmRemoved after sync
  public preRemoveBatch(keys: string[]): void {
    const localKeys = this.toLocalKeys(keys);
    this.localStore.removeBatch(localKeys);

    // Store removed keys until confirmRemoved is called (after syncing)
    const removedKeys = this.getRemovedKeys();
    const newRemovedKeys = keys.filter((key) => !removedKeys.includes(key));
    if (newRemovedKeys.length === 0) {
      return;
    }
    removedKeys.push(...newRemovedKeys);
    this.localStore.write(removedKey, removedKeys);
  }

  // Called after sync when remote server also has removed the keys
  public confirmRemoved(keys: string[]): void {
    let removedKeys = this.getRemovedKeys();
    removedKeys = removedKeys.filter((key) => !keys.includes(key));
    this.localStore.write(removedKey, removedKeys);
  }

  public getUnsyncedKeys(): string[] {
    const unSyncedKeys = this.getAllEntities()
      .filter((entity) => entity.etag !== entity.syncedEtag)
      .map((entity: LocalEntity) => entity.key);
    return unSyncedKeys;
  }

  public getAllEntities(): LocalEntity[] {
    const localKeys = this.localStore
      .keys()
      .filter((key) => key.startsWith(localKeyPrefix));
    return this.localStore.tryReadBatch(localKeys);
  }

  public getRemovedKeys(): string[] {
    return orDefault(this.localStore.tryRead<string[]>(removedKey), []);
  }

  public clear(): void {
    const localKeys = this.localStore
      .keys()
      .filter((key) => key.startsWith(localKeyPrefix));
    this.localStore.removeBatch(localKeys);
    this.localStore.removeBatch([removedKey]);
  }

  private toLocalKeys(keys: string[]): string[] {
    return keys.map((key) => this.toLocalKey(key));
  }

  private toLocalKey(key: string): string {
    return localKeyPrefix + key;
  }
}
