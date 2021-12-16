import Result from "./Result";
import { diKey, singleton } from "./di";

export interface Entity<T> {
  key: string;
  timestamp: number;
  synced: number;

  value: T;
}

export const ILocalDataKey = diKey<ILocalData>();
export interface ILocalData {
  tryRead<T>(key: string): Result<Entity<T>>;
  tryReadBatch<T>(keys: string[]): Result<Entity<T>>[];
  write<T>(entity: Entity<T>): void;
  writeBatch<T>(entities: Entity<T>[]): void;
  remove(key: string): void;
  removeBatch(keys: string[]): void;
  keys(): string[];
  count(): number;
  clear(): void;
}

@singleton(ILocalDataKey)
export default class LocalData implements ILocalData {
  public tryRead<T>(key: string): Result<Entity<T>> {
    return this.tryReadBatch<T>([key])[0];
  }

  public tryReadBatch<T>(keys: string[]): Result<Entity<T>>[] {
    return keys.map((key: string) => {
      let entityText = localStorage.getItem(key);
      if (entityText == null) {
        return new RangeError(`No data for id: ${key}`);
      }
      // console.log(`Read key: ${key}, ${text.length} bytes`);
      const entity: any = JSON.parse(entityText);
      return entity as Entity<T>;
    });
  }

  public write<T>(entity: Entity<T>): void {
    this.writeBatch([entity]);
  }

  public writeBatch<T>(entities: Entity<T>[]): void {
    entities.forEach((entity) => {
      const key = entity.key;
      const entityText = JSON.stringify(entity);
      localStorage.setItem(key, entityText);
      // console.log(`Wrote key: ${key}, ${text.length} bytes`);
    });
  }

  public remove(key: string): void {
    this.removeBatch([key]);
  }

  public removeBatch(keys: string[]): void {
    keys.forEach((id: string) => {
      localStorage.removeItem(id);
    });
  }

  public keys(): string[] {
    const keys: string[] = [];
    for (var i = 0, len = localStorage.length; i < len; i++) {
      const key: string = localStorage.key(i) as string;
      keys.push(key);
    }
    return keys;
  }

  public count(): number {
    return localStorage.length;
  }

  public clear(): void {
    localStorage.clear();
  }
}
