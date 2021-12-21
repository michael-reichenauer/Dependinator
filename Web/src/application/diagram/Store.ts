import cuid from "cuid";
import Result, { isError } from "../../common/Result";
import assert from "assert";
import { di, singleton, diKey } from "../../common/di";
import { ILocalFiles, ILocalFilesKey } from "../../common/LocalFiles";
import { IStoreSync, IStoreSyncKey, SyncRequest } from "./StoreSync";
import {
  ApplicationDto,
  applicationKey,
  CanvasDto,
  DiagramDto,
  DiagramInfoDto,
  DiagramInfoDtos,
  FileDto,
} from "./StoreDtos";
import { Entity as LocalEntity } from "../../common/LocalDB";
import { Entity as RemoteEntity } from "../../common/remoteDB";

const rootCanvasId = "root";
const defaultApplicationDto: ApplicationDto = { diagramInfos: {} };
const defaultDiagramDto: DiagramDto = { id: "", name: "", canvases: {} };

export const IStoreKey = diKey<IStore>();
export interface IStore {
  initialize(): Promise<void>;

  openNewDiagram(): DiagramDto;
  tryOpenDiagram(diagramId: string): Promise<Result<DiagramDto>>;

  setDiagramName(name: string): void;
  exportDiagram(): DiagramDto; // Used for print or export

  getRootCanvas(): CanvasDto;
  getCanvas(canvasId: string): CanvasDto;
  writeCanvas(canvas: CanvasDto): void;

  getMostResentDiagramId(): Result<string>;
  getRecentDiagrams(): DiagramInfoDto[];

  deleteDiagram(diagramId: string): void;

  saveDiagramToFile(): void;
  loadDiagramFromFile(): Promise<Result<string>>;
  saveAllDiagramsToFile(): Promise<void>;
}

@singleton(IStoreKey) // eslint-disable-next-line
class Store implements IStore {
  private currentDiagramId: string = "";

  constructor(
    // private localData: ILocalData = di(ILocalDataKey),
    private localFiles: ILocalFiles = di(ILocalFilesKey),
    private storeSync: IStoreSync = di(IStoreSyncKey)
  ) {}

  public async initialize(): Promise<void> {
    this.storeSync.initialize();

    const requests = Object.keys(this.getApplicationDto().diagramInfos).map(
      (key) => ({ key: key, onConflict: this.onDiagramConflict })
    ) as SyncRequest<any>[];
    requests.push({
      key: applicationKey,
      onConflict: this.onApplicationConflict,
    });

    this.storeSync.triggerSync(requests, false);
  }

  public openNewDiagram(): DiagramDto {
    const now = Date.now();
    const id = cuid();
    const name = this.getUniqueName();
    console.log("new diagram", id, name);

    const diagramDto: DiagramDto = {
      id: id,
      name: name,
      canvases: {},
    };

    const applicationDto = this.getApplicationDto();
    applicationDto.diagramInfos[id] = {
      id: id,
      name: name,
      accessed: now,
    };

    this.storeSync.writeBatch<any>([
      { key: applicationKey, value: applicationDto },
      { key: id, value: diagramDto },
    ]);

    this.triggerSync(id);

    this.currentDiagramId = id;
    return diagramDto;
  }

  public async tryOpenDiagram(id: string): Promise<Result<DiagramDto>> {
    const diagramDto =
      await this.storeSync.tryReadLocalThenRemoteAsync<DiagramDto>(id);
    if (isError(diagramDto)) {
      return diagramDto;
    }

    // Mark diagram as accessed now, to support most recently used diagram feature
    const applicationDto = this.getApplicationDto();
    applicationDto.diagramInfos[id] = {
      ...applicationDto.diagramInfos[id],
      accessed: Date.now(),
    };

    this.storeSync.writeBatch([{ key: applicationKey, value: applicationDto }]);

    this.triggerSync(id);

    this.currentDiagramId = id;
    return diagramDto;
  }

  public getRootCanvas(): CanvasDto {
    return this.getCanvas(rootCanvasId);
  }

  public getCanvas(canvasId: string): CanvasDto {
    const diagramDto = this.getDiagramDto();

    const canvasDto = diagramDto.canvases[canvasId];
    assert(canvasDto);

    return canvasDto;
  }

  public writeCanvas(canvasDto: CanvasDto): void {
    const diagramDto = this.getDiagramDto();
    const id = diagramDto.id;

    diagramDto.canvases[canvasDto.id] = canvasDto;

    this.storeSync.writeBatch<any>([{ key: id, value: diagramDto }]);

    this.triggerSync(id);
  }

  public getRecentDiagrams(): DiagramInfoDto[] {
    return Object.values(this.getApplicationDto().diagramInfos).sort((i1, i2) =>
      i1.accessed < i2.accessed ? 1 : i1.accessed > i2.accessed ? -1 : 0
    );
  }

  // For printing/export
  public exportDiagram(): DiagramDto {
    return this.getDiagramDto();
  }

  public deleteDiagram(id: string): void {
    console.log("Delete diagram", id);

    const applicationDto = this.getApplicationDto();
    delete applicationDto.diagramInfos[id];

    this.storeSync.writeBatch([{ key: applicationKey, value: applicationDto }]);
    this.storeSync.removeBatch([id]);

    this.triggerSync(id);
  }

  public setDiagramName(name: string): void {
    const diagramDto = this.getDiagramDto();
    const id = diagramDto.id;
    diagramDto.name = name;

    const applicationDto = this.getApplicationDto();
    applicationDto.diagramInfos[id] = {
      ...applicationDto.diagramInfos[id],
      name: name,
    };

    this.storeSync.writeBatch<any>([
      { key: applicationKey, value: applicationDto },
      { key: id, value: diagramDto },
    ]);

    this.triggerSync(id);
  }

  public async loadDiagramFromFile(): Promise<Result<string>> {
    const fileText = await this.localFiles.loadFile();
    const fileDto: FileDto = JSON.parse(fileText);

    // if (!(await this.sync.uploadDiagrams(fileDto.diagrams))) {
    //   // save locally
    //   fileDto.diagrams.forEach((d: DiagramDto) => this.local.writeDiagram(d));
    // }

    //fileDto.diagrams.forEach((d: DiagramDto) => this.local.writeDiagram(d));

    const firstDiagramId = fileDto.diagrams[0]?.id;
    if (!firstDiagramId) {
      return new Error("No valid diagram in file");
    }
    return firstDiagramId;
  }

  public saveDiagramToFile(): void {
    const diagramDto = this.getDiagramDto();

    const fileDto: FileDto = { diagrams: [diagramDto] };
    const fileText = JSON.stringify(fileDto, null, 2);
    this.localFiles.saveFile(`${diagramDto.name}.json`, fileText);
  }

  public async saveAllDiagramsToFile(): Promise<void> {
    // let diagrams = await this.sync.downloadAllDiagrams();
    // if (!diagrams) {
    //   // Read from local
    //   diagrams = this.local.readAllDiagrams();
    // }
    //   let diagrams = this.local.readAllDiagrams();
    //   const fileDto = { diagrams: diagrams };
    //   const fileText = JSON.stringify(fileDto, null, 2);
    //   this.localFiles.saveFile(`diagrams.json`, fileText);
  }

  public getMostResentDiagramId(): Result<string> {
    const resentDiagrams = this.getRecentDiagrams();
    if (resentDiagrams.length === 0) {
      return new RangeError("not found");
    }

    return resentDiagrams[0].id;
  }

  public getApplicationDto(): ApplicationDto {
    return this.storeSync.readLocal<ApplicationDto>(
      applicationKey,
      defaultApplicationDto
    );
  }

  private triggerSync(diagramId: string) {
    this.storeSync.triggerSync<any>(
      [
        { key: applicationKey, onConflict: this.onApplicationConflict },
        { key: diagramId, onConflict: this.onDiagramConflict },
      ],
      true
    );
  }

  private onApplicationConflict(
    local: LocalEntity<ApplicationDto>,
    remote: RemoteEntity<ApplicationDto>
  ): LocalEntity<ApplicationDto> {
    console.log("Application conflict", local, remote);

    const mergeDiagramInfos = (
      newerDiagrams: DiagramInfoDtos,
      olderDiagrams: DiagramInfoDtos
    ): DiagramInfoDtos => {
      let mergedDiagrams = { ...olderDiagrams, ...newerDiagrams };
      Object.keys(newerDiagrams).forEach((key) => {
        if (!(key in newerDiagrams)) {
          delete mergedDiagrams[key];
        }
      });
      return mergedDiagrams;
    };

    if (local.version >= remote.version) {
      // Local entity has more edits
      const applicationDto: ApplicationDto = {
        diagramInfos: mergeDiagramInfos(
          local.value.diagramInfos,
          remote.value.diagramInfos
        ),
      };
      return { ...local, value: applicationDto };
    }

    // Remote entity since that has more edits
    const applicationDto: ApplicationDto = {
      diagramInfos: mergeDiagramInfos(
        remote.value.diagramInfos,
        local.value.diagramInfos
      ),
    };

    return {
      key: remote.key,
      timestamp: remote.timestamp,
      version: remote.version,
      synced: remote.timestamp,
      value: applicationDto,
    };
  }

  private onDiagramConflict(
    local: LocalEntity<DiagramDto>,
    remote: RemoteEntity<DiagramDto>
  ): LocalEntity<DiagramDto> {
    console.log("Diagram conflict", local, remote);
    if (local.version >= remote.version) {
      // use local since it has more edits
      return local;
    }

    // Use remote entity since that has more edits
    return {
      key: remote.key,
      timestamp: remote.timestamp,
      version: remote.version,
      synced: remote.timestamp,
      value: remote.value,
    };
  }

  private getDiagramDto(): DiagramDto {
    return this.storeSync.readLocal<DiagramDto>(
      this.currentDiagramId,
      defaultDiagramDto
    );
  }

  private getUniqueName(): string {
    const diagrams = Object.values(this.getApplicationDto().diagramInfos);

    for (let i = 0; i < 99; i++) {
      const name = "Name" + (i > 0 ? ` (${i})` : "");
      if (!diagrams.find((d) => d.name === name)) {
        return name;
      }
    }

    return "Name";
  }
}
