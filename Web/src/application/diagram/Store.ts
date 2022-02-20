import cuid from "cuid";
import Result, { isError } from "../../common/Result";
import assert from "assert";
import { di, singleton, diKey } from "../../common/di";
import { ILocalFiles, ILocalFilesKey } from "../../common/LocalFiles";
import { IStoreDB, IStoreDBKey, MergeEntity } from "../../common/db/StoreDB";
import {
  ApplicationDto,
  applicationKey,
  CanvasDto,
  DiagramDto,
  DiagramInfoDto,
  DiagramInfoDtos,
  FileDto,
} from "./StoreDtos";
import { LocalEntity } from "../../common/db/LocalDB";
import { RemoteEntity } from "../../common/db/RemoteDB";

export interface Configuration {
  onRemoteChanged: (keys: string[]) => void;
  onSyncChanged: (isOK: boolean, error?: Error) => void;
  isSyncEnabled: boolean;
}

export const IStoreKey = diKey<IStore>();
export interface IStore {
  configure(config: Partial<Configuration>): void;
  triggerSync(): Promise<Result<void>>;

  openNewDiagram(): DiagramDto;
  tryOpenMostResentDiagram(): Promise<Result<DiagramDto>>;
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

const rootCanvasId = "root";
const defaultApplicationDto: ApplicationDto = { diagramInfos: {} };
const defaultDiagramDto: DiagramDto = { id: "", name: "", canvases: {} };

@singleton(IStoreKey)
export class Store implements IStore {
  private currentDiagramId: string = "";
  private config: Configuration = {
    onRemoteChanged: () => {},
    onSyncChanged: () => {},
    isSyncEnabled: false,
  };

  constructor(
    // private localData: ILocalData = di(ILocalDataKey),
    private localFiles: ILocalFiles = di(ILocalFilesKey),
    private db: IStoreDB = di(IStoreDBKey)
  ) {}

  public configure(config: Partial<Configuration>): void {
    this.config = { ...this.config, ...config };

    this.db.configure({
      onConflict: (local: LocalEntity, remote: RemoteEntity) =>
        this.onEntityConflict(local, remote),
      ...config,
      onRemoteChanged: (keys: string[]) => this.onRemoteChange(keys),
    });
  }

  public triggerSync(): Promise<Result<void>> {
    return this.db.triggerSync();
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

    this.db.monitorRemoteEntities([id, applicationKey]);
    this.db.writeBatch([
      { key: applicationKey, value: applicationDto },
      { key: id, value: diagramDto },
    ]);

    this.currentDiagramId = id;
    return diagramDto;
  }

  public async tryOpenMostResentDiagram(): Promise<Result<DiagramDto>> {
    const id = this.getMostResentDiagramId();
    if (isError(id)) {
      return id as Error;
    }

    const diagramDto = await this.db.tryReadLocalThenRemote<DiagramDto>(id);
    if (isError(diagramDto)) {
      return diagramDto;
    }

    this.db.monitorRemoteEntities([id, applicationKey]);
    this.currentDiagramId = id;

    return diagramDto;
  }

  public async tryOpenDiagram(id: string): Promise<Result<DiagramDto>> {
    const diagramDto = await this.db.tryReadLocalThenRemote<DiagramDto>(id);
    if (isError(diagramDto)) {
      return diagramDto;
    }

    this.db.monitorRemoteEntities([id, applicationKey]);
    this.currentDiagramId = id;

    // Too support most recently used diagram feature, we update accessed time
    const applicationDto = this.getApplicationDto();
    const diagramInfo = applicationDto.diagramInfos[id];
    applicationDto.diagramInfos[id] = { ...diagramInfo, accessed: Date.now() };
    this.db.writeBatch([{ key: applicationKey, value: applicationDto }]);

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

    this.db.writeBatch([{ key: id, value: diagramDto }]);
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

    this.db.writeBatch([{ key: applicationKey, value: applicationDto }]);
    this.db.removeBatch([id]);
  }

  public setDiagramName(name: string): void {
    const diagramDto = this.getDiagramDto();
    const id = diagramDto.id;
    diagramDto.name = name;

    const applicationDto = this.getApplicationDto();
    applicationDto.diagramInfos[id] = {
      ...applicationDto.diagramInfos[id],
      name: name,
      accessed: Date.now(),
    };

    this.db.writeBatch([
      { key: applicationKey, value: applicationDto },
      { key: id, value: diagramDto },
    ]);
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
    return this.db.readLocal<ApplicationDto>(
      applicationKey,
      defaultApplicationDto
    );
  }

  private onRemoteChange(keys: string[]) {
    this.config.onRemoteChanged(keys);
  }

  private onEntityConflict(
    local: LocalEntity,
    remote: RemoteEntity
  ): MergeEntity {
    if ("diagramInfos" in local.value) {
      return this.onApplicationConflict(local, remote);
    }
    return this.onDiagramConflict(local, remote);
  }

  private onApplicationConflict(
    local: LocalEntity,
    remote: RemoteEntity
  ): MergeEntity {
    console.warn("Application conflict", local, remote);

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
      // Local entity has more edits, merge diagram infos, but priorities remote
      const applicationDto: ApplicationDto = {
        diagramInfos: mergeDiagramInfos(
          local.value.diagramInfos,
          remote.value.diagramInfos
        ),
      };

      return {
        key: local.key,
        value: applicationDto,
        version: local.version,
      };
    }

    // Remote entity since that has more edits, merge diagram infos, but priorities local
    const applicationDto: ApplicationDto = {
      diagramInfos: mergeDiagramInfos(
        remote.value.diagramInfos,
        local.value.diagramInfos
      ),
    };

    return {
      key: remote.key,
      value: applicationDto,
      version: remote.version,
    };
  }

  private onDiagramConflict(
    local: LocalEntity,
    remote: RemoteEntity
  ): MergeEntity {
    console.warn("Diagram conflict", local, remote);
    if (local.version >= remote.version) {
      // use local since it has more edits
      return {
        key: local.key,
        value: local.value,
        version: local.version,
      };
    }

    // Use remote entity since that has more edits
    return {
      key: remote.key,
      value: remote.value,
      version: remote.version,
    };
  }

  private getDiagramDto(): DiagramDto {
    return this.db.readLocal<DiagramDto>(
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
