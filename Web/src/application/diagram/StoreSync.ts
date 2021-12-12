import { ILocalData, ILocalDataKey } from "../../common/LocalData";
import { IRemoteData, IRemoteDataKey } from "../../common/remoteData";
import Result, { expectValue, isError } from "../../common/Result";
import { di, diKey, singleton } from "./../../common/di";
import { ApplicationDto, applicationKey } from "./StoreDtos";

export interface Item {
  id: string;
  timestamp?: number;
}

export const IStoreSyncKey = diKey<IStoreSync>();
export interface IStoreSync {
  getApplicationDto(): ApplicationDto;
  writeBatch(data: Item[]): void;
  removeBatch(ids: string[]): void;
  tryReadAsync<T extends Item>(id: string): Promise<Result<T>>;
  read<T>(id: string): T;
}

@singleton(IStoreSyncKey) // eslint-disable-next-line
class StoreSync implements IStoreSync {
  constructor(
    private localData: ILocalData = di(ILocalDataKey),
    private remoteData: IRemoteData = di(IRemoteDataKey)
  ) {}

  public read<T>(id: string): T {
    return expectValue(this.localData.tryRead<T>(id));
  }

  public async tryReadAsync<T extends Item>(id: string): Promise<Result<T>> {
    const localDto = this.localData.tryRead<T>(id);
    if (isError(localDto)) {
      // Dto not cached locally, lets try get from remote location
      const remoteDto = await this.remoteData.tryRead<T>(id);
      if (isError(remoteDto)) {
        return new RangeError(`id ${id} not found,` + remoteDto);
      }

      // Cache remote data locally
      this.localData.write(remoteDto);
      return remoteDto;
    }

    return localDto;
  }

  public writeBatch(items: Item[]): void {
    const now = Date.now();
    items.forEach((item) => {
      item.timestamp = now;
    });

    // Cache data locally
    this.localData.writeBatch(items);

    // Sync data to server
    this.remoteData.writeBatch(items);
  }

  public removeBatch(ids: string[]): void {
    this.localData.removeBatch(ids);
  }

  public getApplicationDto(): ApplicationDto {
    let dto = this.localData.tryRead<ApplicationDto>(applicationKey);
    if (isError(dto)) {
      // First access, lets store default data for future access
      dto = { id: applicationKey, diagramInfos: {} };
      dto.timestamp = Date.now();
      this.localData.writeBatch([dto]);
    }

    return dto;
  }

  public async triggerSyncCheck(): Promise<void> {
    const dto = await this.remoteData.tryRead<ApplicationDto>(applicationKey);
    if (isError(dto)) {
      // Signal error check
      return;
    }
  }
}
