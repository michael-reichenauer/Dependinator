import Result from "./Result";

// export interface Data<T> {
//   key: string;
//   data: T;
// }

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
      return JSON.parse(text);
    });
  }

  public write(data: any): void {
    this.writeBatch([data]);
  }

  public writeBatch(batch: any[]): void {
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
