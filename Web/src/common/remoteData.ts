import { diKey, singleton } from "./di";
import Result from "./Result";
export const IRemoteDataKey = diKey<IRemoteData>();

export interface Item {
  id: string;
  timestamp?: number;
}

export interface IRemoteData {
  writeBatch(items: Item[]): Promise<Result<void>>;
  tryReadBatch<T>(ids: string[]): Promise<Result<T>[]>;
  tryRead<T>(id: string): Promise<Result<T>>;
}

@singleton(IRemoteDataKey) // eslint-disable-next-line
class RemoteData implements IRemoteData {
  tryRead<T>(id: string): Promise<Result<T>> {
    throw new Error("Method not implemented.");
  }
  async writeBatch(items: Item[]): Promise<Result<void>> {
    console.log("Wrote:", items);
  }

  async tryReadBatch<T>(ids: string[]): Promise<Result<T>[]> {
    throw new Error("Method not implemented.");
  }
}
