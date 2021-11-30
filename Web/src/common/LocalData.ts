import Result from "./Result";

export interface DataPair<T> {
  key: string;
  data: T;
}

export interface ILocalData {
  tryRead<T>(key: string): Result<T>;
  tryReadBatch<T>(keys: string[]): Result<T>[];
  write<T>(key: string, data: T): void;
  writeBatch<T>(pairs: DataPair<T>[]): void;
  remove(key: string): void;
  removeBatch(keys: string[]): void;
  keys(): string[];
  count(): number;
  clear(): void;
}

export default class LocalData implements ILocalData {
  public tryRead<T>(key: string): Result<T> {
    return this.tryReadBatch<T>([key])[0];
  }

  public tryReadBatch<T>(keys: string[]): Result<T>[] {
    return keys.map((key: string) => {
      let text = localStorage.getItem(key);
      if (text == null) {
        return new RangeError(`No such key: ${key}`);
      }
      // console.log(`Read key: ${key}, ${text.length} bytes`);
      return JSON.parse(text);
    });
  }

  public write<T>(key: string, data: T): void {
    this.writeBatch([{ key: key, data: data }]);
  }

  public writeBatch<T>(pairs: DataPair<T>[]): void {
    pairs.forEach((pair: DataPair<T>) => {
      const key = pair.key;
      const text = JSON.stringify(pair.data);
      localStorage.setItem(key, text);
      // console.log(`Wrote key: ${key}, ${text.length} bytes`);
    });
  }

  public remove(key: string): void {
    this.removeBatch([key]);
  }

  public removeBatch(keys: string[]): void {
    keys.forEach((key: string) => {
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
