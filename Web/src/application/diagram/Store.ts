import { ILocalFiles, ILocalFilesKey } from "../../common/LocalFiles";
import {
  ApplicationDto,
  applicationKey,
  CanvasDto,
  DiagramDto,
  DiagramInfoDto,
  FileDto,
} from "./StoreDtos";
import Result, { isError } from "../../common/Result";
import { di, singleton } from "../../common/di";
import cuid from "cuid";
import assert from "assert";
import { diKey } from "./../../common/di";
import { IStoreSync, IStoreSyncKey } from "./StoreSync";
import { ILocalData, ILocalDataKey } from "./../../common/LocalData";

const rootCanvasId = "root";

// Init
// get app => new empty app
// new diagram
// edit diagram
// enable sync
// get remote app => merge local and remote => sync app, sync local diagrams
// edit diagram
// get remote app => merge local and remote => sync app, sync local diagrams
// get remote app => merge local and remote => sync app, sync local diagrams

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
    private localData: ILocalData = di(ILocalDataKey),
    private storeSync: IStoreSync = di(IStoreSyncKey)
  ) {}

  public async initialize(): Promise<void> {
    let dto = this.localData.tryRead<ApplicationDto>(applicationKey);
    if (isError(dto)) {
      // First access, lets store default data for future access
      dto = { id: applicationKey, diagramInfos: {} };
      dto.timestamp = Date.now();
      this.localData.writeBatch([dto]);
    }

    this.storeSync.initialize();
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
      written: now,
    };

    this.storeSync.writeBatch([applicationDto, diagramDto]);

    this.currentDiagramId = id;
    return diagramDto;
  }

  public async tryOpenDiagram(id: string): Promise<Result<DiagramDto>> {
    const diagramDto = await this.storeSync.tryReadAsync<DiagramDto>(id);
    if (isError(diagramDto)) {
      return diagramDto;
    }

    // Mark diagram as accessed now, to support most recently used diagram feature
    const applicationDto = this.getApplicationDto();
    applicationDto.diagramInfos[id] = {
      ...applicationDto.diagramInfos[id],
      accessed: Date.now(),
    };

    this.storeSync.writeBatch([applicationDto]);

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

    const now = Date.now();
    const applicationDto = this.getApplicationDto();
    applicationDto.diagramInfos[id] = {
      ...applicationDto.diagramInfos[id],
      accessed: now,
      written: now,
    };

    this.storeSync.writeBatch([applicationDto, diagramDto]);
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

  public deleteDiagram(diagramId: string): void {
    console.log("Delete diagram", diagramId);

    console.log("Delete diagram", diagramId);

    const applicationDto = this.getApplicationDto();
    delete applicationDto.diagramInfos[diagramId];

    this.storeSync.writeBatch([applicationDto]);
    this.storeSync.removeBatch([diagramId]);
  }

  public setDiagramName(name: string): void {
    const now = Date.now();

    const diagramDto = this.getDiagramDto();
    const id = diagramDto.id;
    diagramDto.name = name;

    const applicationDto = this.getApplicationDto();
    applicationDto.diagramInfos[id] = {
      ...applicationDto.diagramInfos[id],
      name: name,
      accessed: now,
      written: now,
    };

    this.storeSync.writeBatch([applicationDto, diagramDto]);
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
    return this.storeSync.read<ApplicationDto>(applicationKey);
  }

  private getDiagramDto(): DiagramDto {
    return this.storeSync.read<DiagramDto>(this.currentDiagramId);
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
