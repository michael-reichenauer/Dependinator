import Result from "./Result";
import { diKey, singleton } from "./di";

export interface Item {
  id: string;
}

export const ILocalDataKey = diKey<ILocalData>();
export interface ILocalData {
  tryRead<T>(key: string): Result<T>;
  tryReadBatch<T>(ids: string[]): Result<T>[];
  write(data: Item): void;
  writeBatch(batch: Item[]): void;
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

  public tryReadBatch<T>(ids: string[]): Result<T>[] {
    return ids.map((id: string) => {
      let text = localStorage.getItem(id);
      if (text == null) {
        return new RangeError(`No data for id: ${id}`);
      }
      // console.log(`Read key: ${key}, ${text.length} bytes`);
      const data: any = JSON.parse(text);
      return data as T;
    });
  }

  public write(item: Item): void {
    this.writeBatch([item]);
  }

  public writeBatch(batch: Item[]): void {
    batch.forEach((data: Item) => {
      const key = data.id;
      const text = JSON.stringify(data);
      localStorage.setItem(key, text);
      // console.log(`Wrote key: ${key}, ${text.length} bytes`);
    });
  }

  public remove(id: string): void {
    this.removeBatch([id]);
  }

  public removeBatch(ids: string[]): void {
    ids.forEach((id: string) => {
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
