import Result, { isError, orDefault } from "../Result";
import { di, diKey, singleton } from "../di";
import { ILocalStore, ILocalStoreKey } from "../LocalStore";
import { CustomError } from "../CustomError";

export interface LocalEntity {
  key: string;
  timestamp: number;
  version: number;
  synced: number;

  value: any;
}

export class RemovedError extends CustomError {}

export const ILocalDBKey = diKey<ILocalDB>();
export interface ILocalDB {
  tryReadValue<T>(key: string): Result<T>;
  tryReadBatch(keys: string[]): Result<LocalEntity>[];
  getUnsyncedKeys(): string[];
  getAllEntities(): LocalEntity[];
  write(entity: LocalEntity): void;
  writeBatch(entities: LocalEntity[]): void;
  removeBatch(keys: string[], confirmed: boolean): void;
  confirmRemoved(keys: string[]): void;
  getRemovedKeys(): string[];
  clear(): void;
}

const removedKey = "removedEntities";

function isLocalEntity(obj: any): obj is LocalEntity {
  return (
    "key" in obj &&
    "timestamp" in obj &&
    "synced" in obj &&
    "version" in obj &&
    "value" in obj
  );
}

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
    return this.localStore.tryReadBatch(keys);
  }

  public write(entity: LocalEntity): void {
    this.writeBatch([entity]);
  }

  public getUnsyncedKeys(): string[] {
    const unSyncedKeys = this.getAllEntities()
      .filter((entity) => entity.timestamp !== entity.synced)
      .map((entity: LocalEntity) => entity.key);
    return unSyncedKeys;
  }

  public getAllEntities(): LocalEntity[] {
    const localValues = this.localStore.tryReadBatch(this.localStore.keys());
    const existingEntities = localValues.filter((value) =>
      isLocalEntity(value)
    ) as LocalEntity[];
    return existingEntities;
  }

  public writeBatch(entities: LocalEntity[]): void {
    const keyValues = entities.map((entity) => ({
      key: entity.key,
      value: entity,
    }));
    this.localStore.writeBatch(keyValues);

    // Ensure possible previously removed keys are no longer considered removed
    const keys = entities.map((entity) => entity.key);
    this.confirmRemoved(keys);
  }

  public removeBatch(keys: string[], confirmed: boolean): void {
    this.localStore.removeBatch(keys);

    if (confirmed) {
      // Remove is confirmed, no need to store keys
      return;
    }

    // Store removed keys until confirmRemoved is called (after syncing)
    const removedKeys = this.getRemovedKeys();
    const newRemovedKeys = keys.filter((key) => !removedKeys.includes(key));
    if (newRemovedKeys.length === 0) {
      return;
    }
    removedKeys.push(...newRemovedKeys);
    this.localStore.write(removedKey, removedKeys);
  }

  public getRemovedKeys(): string[] {
    return orDefault(this.localStore.tryRead<string[]>(removedKey), []);
  }

  public confirmRemoved(keys: string[]): void {
    let removedKeys = this.getRemovedKeys();
    removedKeys = removedKeys.filter((key) => !keys.includes(key));
    this.localStore.write(removedKey, removedKeys);
  }

  public clear(): void {
    this.localStore.clear();
  }
}
