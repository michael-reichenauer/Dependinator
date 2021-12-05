import Result from "./Result";
import { diKey, singleton } from "./di";

// eslint-disable-next-line
export interface Data<T> {
  id: string;
  timestamp?: number;
}

export const ILocalDataKey = diKey<ILocalData>();
export interface ILocalData {
  tryRead<T>(key: string): Result<T>;
  tryReadBatch<T>(ids: string[]): Result<T>[];
  write(data: any): void;
  writeBatch(datas: any[]): void;
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

  public write<T = any>(data: Data<T>): void {
    this.writeBatch<T>([data]);
  }

  public writeBatch<T = any>(batch: Data<T>[]): void {
    const now = Date.now();
    batch.forEach((data: any) => {
      const key = data.id;
      data.timestamp = now;
      const text = JSON.stringify(data);
      localStorage.setItem(key, text);
      // console.log(`Wrote key: ${key}, ${text.length} bytes`);
    });
  }

  public remove(id: string): void {
    this.removeBatch([id]);
  }

  public removeBatch(ids: string[]): void {
    ids.forEach((key: string) => {
      localStorage.removeItem(key);
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
