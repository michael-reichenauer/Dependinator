import { ILocalData, ILocalDataKey } from "../../common/LocalData";
import { IRemoteData, IRemoteDataKey } from "../../common/remoteData";
import Result, { expectValue, isError } from "../../common/Result";
import { di, diKey, singleton } from "./../../common/di";

export interface Entity {
  id: string;
  timestamp?: number;
}

interface LocalItem extends Entity {
  synced?: number;
}

export const IStoreSyncKey = diKey<IStoreSync>();
export interface IStoreSync {
  initialize(): void;
  writeBatch(data: Entity[]): void;
  removeBatch(ids: string[]): void;
  tryReadAsync<T extends Entity>(id: string): Promise<Result<T>>;
  read<T>(id: string): T;
}

@singleton(IStoreSyncKey) // eslint-disable-next-line
class StoreSync implements IStoreSync {
  constructor(
    private localData: ILocalData = di(ILocalDataKey),
    private remoteData: IRemoteData = di(IRemoteDataKey)
  ) {}

  initialize(): void {}

  public read<T>(id: string): T {
    return expectValue(this.localData.tryRead<T>(id));
  }

  public async tryReadAsync<T extends Entity>(id: string): Promise<Result<T>> {
    const localDto = this.localData.tryRead<T>(id);
    if (isError(localDto)) {
      // Dto not cached locally, lets try get from remote location
      const remoteDto = await this.remoteData.tryRead<T>({ id: id });
      if (isError(remoteDto)) {
        return new RangeError(`id ${id} not found,` + remoteDto);
      }

      // Cache remote data locally as synced
      const item = remoteDto as LocalItem;
      item.synced = remoteDto.timestamp;
      this.localData.write(item);
      return remoteDto;
    }

    return localDto;
  }

  public writeBatch(items: Entity[]): void {
    const now = Date.now();
    items.forEach((item) => {
      item.timestamp = now;
    });

    // Cache data locally
    this.localData.writeBatch(items);

    // Sync data to server
    this.remoteData.writeBatch(items).then((response) => {
      if (isError(response)) {
        // Signal sync error !!!!!!!
        console.warn("Sync error while writing");
        return;
      }

      // Stamp existing local items with synced time stamp and
      const ids = items.map((item) => item.id);
      const existingItems = this.localData
        .tryReadBatch<LocalItem>(ids)
        .filter((r) => !isError(r)) as LocalItem[];
      const syncedItems = existingItems.map((item) => ({
        ...item,
        synced: now,
      }));

      this.localData.writeBatch(syncedItems);
    });
  }

  public removeBatch(ids: string[]): void {
    this.localData.removeBatch(ids);
  }

  public async triggerSyncCheck(): Promise<void> {
    //   const dto = await this.remoteData.tryRead<ApplicationDto>({
    //     id: applicationKey,
    //   });
    //   if (isError(dto)) {
    //     // Signal error check !!!!!!
    //     console.warn("Sync error while reading app");
    //     return;
    //   }
    //   // Merge local app and remote app
    //   //
  }
}
