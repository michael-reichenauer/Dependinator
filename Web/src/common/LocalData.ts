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
  tryRead<T>(key: string): Result<T> {
    return this.tryReadBatch<T>([key])[0];
  }

  tryReadBatch<T>(keys: string[]): Result<T>[] {
    return keys.map((key: string) => {
      let text = localStorage.getItem(key);
      if (text == null) {
        return new RangeError(`No such key: ${key}`);
      }
      return JSON.parse(text);
    });
  }

  write<T>(key: string, data: T): void {
    this.writeBatch([{ key: key, data: data }]);
  }

  writeBatch<T>(pairs: DataPair<T>[]): void {
    pairs.forEach((pair: DataPair<T>) => {
      const text = JSON.stringify(pair.data);
      localStorage.setItem(pair.key, text);
    });
  }

  remove(key: string): void {
    this.removeBatch([key]);
  }

  removeBatch(keys: string[]): void {
    keys.forEach((key: string) => {
      localStorage.removeItem(key);
    });
  }

  keys(): string[] {
    const keys: string[] = [];
    for (var i = 0, len = localStorage.length; i < len; i++) {
      const key: string = localStorage.key(i) as string;
      keys.push(key);
    }
    return keys;
  }

  count(): number {
    return localStorage.length;
  }

  clear(): void {
    localStorage.clear();
  }
}
