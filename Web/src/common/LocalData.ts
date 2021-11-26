import Result from "./Result";
import trust from "./trust";

export interface ILocalData {
  exists(key: string): boolean;
  read<T = any>(key: string): T;
  tryRead<T = any>(key: string): Result<T>;
  write<T = any>(key: string, data: T): void;
  remove(key: string): void;

  forEach<T = any>(callbackfn: (value: T, key: string) => void): void;
  whereKey<T = any>(
    keyFilter: (key: string) => boolean,
    callbackfn: (value: T, key: string) => void
  ): void;
  count(): number;

  clear(): void;
}

export default class LocalData implements ILocalData {
  exists(key: string): boolean {
    return localStorage.getItem(key) !== null;
  }

  read<T = any>(key: string): T {
    let text = localStorage.getItem(key);
    trust(text !== null);
    return JSON.parse(text);
  }

  tryRead<T = any>(key: string): Result<T> {
    let text = localStorage.getItem(key);
    if (text == null) {
      return new RangeError(`No such key: ${key}`);
    }
    return JSON.parse(text);
  }

  write<T = any>(key: string, data: T): void {
    const text = JSON.stringify(data);
    localStorage.setItem(key, text);
  }

  remove(key: string): void {
    localStorage.removeItem(key);
  }

  forEach<T = any>(callbackfn: (value: T, key: string) => void): void {
    this.whereKey((_key: string) => true, callbackfn);
  }

  whereKey<T = any>(
    keyFilter: (key: string) => boolean,
    callbackfn: (value: T, key: string) => void
  ): void {
    for (var i = 0, len = localStorage.length; i < len; i++) {
      const key: string = localStorage.key(i) as string;
      if (!keyFilter(key)) {
        // Item excluded
        continue;
      }
      const value: T = this.read<T>(key);
      callbackfn(value, key);
    }
  }

  count(): number {
    return localStorage.length;
  }

  clear(): void {
    localStorage.clear();
  }
}
