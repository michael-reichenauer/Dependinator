import Result from "./Result";
import { diKey, singleton } from "./di";

export interface Entity {
  id: string;
}

export const ILocalDataKey = diKey<ILocalData>();
export interface ILocalData {
  tryRead<T>(key: string): Result<T>;
  tryReadBatch<T>(keys: string[]): Result<T>[];
  write<T extends Entity>(entity: T): void;
  writeBatch(entities: Entity[]): void;
  remove(key: string): void;
  removeBatch(keys: string[]): void;
  keys(): string[];
  count(): number;
  clear(): void;
}

@singleton(ILocalDataKey)
export default class LocalData implements ILocalData {
  public tryRead<T>(id: string): Result<T> {
    return this.tryReadBatch<T>([id])[0];
  }

  public tryReadBatch<T>(keys: string[]): Result<T>[] {
    return keys.map((key: string) => {
      let textValue = localStorage.getItem(key);
      if (textValue == null) {
        return new RangeError(`No data for id: ${key}`);
      }
      // console.log(`Read key: ${key}, ${text.length} bytes`);
      const entity: any = JSON.parse(textValue);
      return entity as T;
    });
  }

  public write<T extends Entity>(entity: T): void {
    this.writeBatch([entity]);
  }

  public writeBatch<T extends Entity>(entities: T[]): void {
    entities.forEach((entity) => {
      const key = entity.id;
      const textValue = JSON.stringify(entity);
      localStorage.setItem(key, textValue);
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
