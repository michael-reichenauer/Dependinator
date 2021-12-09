import { ILocalFiles, ILocalFilesKey } from "../../common/LocalFiles";
import {
  ApplicationDto,
  applicationKey,
  CanvasDto,
  DiagramDto,
  DiagramInfoDto,
  FileDto,
} from "./StoreDtos";
import Result, { expectValue, isError } from "../../common/Result";
import { ILocalDataKey } from "../../common/LocalData";
import { di, singleton } from "../../common/di";
import cuid from "cuid";
import { ILocalData } from "./../../common/LocalData";
import assert from "assert";
import { diKey } from "./../../common/di";

const rootCanvasId = "root";

export interface RecentDiagram {
  id: string;
  name: string;
}

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
  getRecentDiagrams(): RecentDiagram[];

  deleteDiagram(diagramId: string): void;

  saveDiagramToFile(): void;
  loadDiagramFromFile(): Promise<Result<string>>;
  saveAllDiagramsToFile(): Promise<void>;
}

@singleton(IStoreKey)
class Store implements IStore {
  private currentDiagramId: string = "";

  constructor(
    private localData: ILocalData = di(ILocalDataKey),
    private localFiles: ILocalFiles = di(ILocalFilesKey)
  ) {}

  public async initialize(): Promise<void> {
    // return await this.sync.initialize();
  }

  public openNewDiagram(): DiagramDto {
    const now = Date.now();
    const id = cuid();
    const name = this.getUniqueName();
    console.log("new diagram", id, name);

    const diagramInfoDto: DiagramInfoDto = {
      id: id,
      name: name,
      accessed: now,
      written: now,
    };

    const diagramDto: DiagramDto = {
      id: id,
      diagramInfo: diagramInfoDto,
      canvases: [],
    };

    this.storeDiagram(diagramDto);
    this.currentDiagramId = id;
    return diagramDto;
  }

  public async tryOpenDiagram(id: string): Promise<Result<DiagramDto>> {
    const diagramDto = this.localData.tryRead<DiagramDto>(id);
    if (isError(diagramDto)) {
      return diagramDto;
    }

    // Mark diagram as accessed now, to support most recently used diagram feature
    diagramDto.diagramInfo.accessed = Date.now();
    this.storeDiagram(diagramDto);

    this.currentDiagramId = id;
    return diagramDto;
  }

  public getRootCanvas(): CanvasDto {
    return this.getCanvas(rootCanvasId);
  }

  public getCanvas(canvasId: string): CanvasDto {
    const diagramDto = this.getDiagramDto();

    const canvasDto = diagramDto.canvases.find((c) => c.id === canvasId);
    assert(canvasDto, `Canvas ${canvasId} not found ${this.currentDiagramId}`);

    return canvasDto as CanvasDto;
  }

  public writeCanvas(canvasDto: CanvasDto): void {
    const diagramDto = this.getDiagramDto();

    this.setDiagramCanvas(diagramDto, canvasDto);

    const now = Date.now();
    diagramDto.diagramInfo.accessed = now;
    diagramDto.diagramInfo.written = now;

    this.storeDiagram(diagramDto);
  }

  private storeDiagram(diagramDto: DiagramDto): void {
    const applicationDto = this.getApplicationDto();
    this.setApplicationDiagramInfo(applicationDto, diagramDto.diagramInfo);
    this.localData.writeBatch([applicationDto, diagramDto]);
  }

  public getRecentDiagrams(): RecentDiagram[] {
    return this.getRecentDiagramInfos().map((d) => ({
      id: d.id,
      name: d.name,
    }));
  }

  // For printing/export
  public exportDiagram(): DiagramDto {
    return this.getDiagramDto();
  }

  public deleteDiagram(diagramId: string): void {
    console.log("Delete diagram", diagramId);

    const applicationDto = this.getApplicationDto();
    this.removeApplicationDiagramInfo(applicationDto, diagramId);

    this.localData.writeBatch([applicationDto]);
    this.localData.remove(diagramId);
  }

  public setDiagramName(name: string): void {
    const now = Date.now();

    const diagramDto = this.getDiagramDto();
    diagramDto.diagramInfo.name = name;
    diagramDto.diagramInfo.accessed = now;
    diagramDto.diagramInfo.written = now;

    this.storeDiagram(diagramDto);
  }

  public async loadDiagramFromFile(): Promise<Result<string>> {
    const fileText = await this.localFiles.loadFile();
    const fileDto: FileDto = JSON.parse(fileText);

    // if (!(await this.sync.uploadDiagrams(fileDto.diagrams))) {
    //   // save locally
    //   fileDto.diagrams.forEach((d: DiagramDto) => this.local.writeDiagram(d));
    // }

    //fileDto.diagrams.forEach((d: DiagramDto) => this.local.writeDiagram(d));

    const firstDiagramId = fileDto.diagrams[0]?.diagramInfo?.id;
    if (!firstDiagramId) {
      return new Error("No valid diagram in file");
    }
    return firstDiagramId;
  }

  public saveDiagramToFile(): void {
    const diagramDto = this.getDiagramDto();

    const fileDto: FileDto = { diagrams: [diagramDto] };
    const fileText = JSON.stringify(fileDto, null, 2);
    this.localFiles.saveFile(`${diagramDto.diagramInfo.name}.json`, fileText);
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

  private getRecentDiagramInfos(): DiagramInfoDto[] {
    return this.getApplicationDto()
      .diagramInfos.sort((i1, i2) =>
        i1.accessed < i2.accessed ? -1 : i1.accessed > i2.accessed ? 1 : 0
      )
      .reverse(); // Remove this reverse (fix comp)
  }

  public getMostResentDiagramId(): Result<string> {
    const resentDiagrams = this.getRecentDiagramInfos();
    if (resentDiagrams.length === 0) {
      return new RangeError("not found");
    }

    return resentDiagrams[0].id;
  }

  private getApplicationDto(): ApplicationDto {
    let dto = this.localData.tryRead<ApplicationDto>(applicationKey);
    if (isError(dto)) {
      // First access, lets store default data for future access
      dto = { id: applicationKey, diagramInfos: [] };
      this.localData.write(dto);
    }
    return dto;
  }

  private getDiagramDto(): DiagramDto {
    return expectValue(
      this.localData.tryRead<DiagramDto>(this.currentDiagramId)
    );
  }

  private removeDiagramCanvas(diagramDto: DiagramDto, canvasId: string): void {
    const index = diagramDto.canvases.findIndex(
      (c: CanvasDto) => c.id === canvasId
    );
    if (index === -1) {
      return;
    }
    diagramDto.canvases.splice(index, 1);
  }

  private setDiagramCanvas(diagramDto: DiagramDto, canvasDto: CanvasDto): void {
    const index = diagramDto.canvases.findIndex(
      (c: CanvasDto) => c.id === canvasDto.id
    );
    if (index === -1) {
      diagramDto.canvases.push(canvasDto);
      return;
    }
    diagramDto.canvases[index] = canvasDto;
  }

  private setApplicationDiagramInfo(
    applicationDto: ApplicationDto,
    diagramInfoDto: DiagramInfoDto
  ): void {
    const index = applicationDto.diagramInfos.findIndex(
      (d: DiagramInfoDto) => d.id === diagramInfoDto.id
    );

    if (index === -1) {
      applicationDto.diagramInfos.push(diagramInfoDto);
      return;
    }

    applicationDto.diagramInfos[index] = diagramInfoDto;
  }

  private removeApplicationDiagramInfo(
    applicationDto: ApplicationDto,
    diagramId: string
  ) {
    const index = applicationDto.diagramInfos.findIndex(
      (d: DiagramInfoDto) => d.id === diagramId
    );
    if (index === -1) {
      console.log("already removed");
      return;
    }

    applicationDto.diagramInfos.splice(index, 1);
  }

  private getUniqueName(): string {
    const applicationDto = this.getApplicationDto();
    for (let i = 0; i < 99; i++) {
      const name = "Name" + (i > 0 ? ` (${i})` : "");
      if (!applicationDto.diagramInfos.find((d) => d.name === name)) {
        return name;
      }
    }

    return "Name";
  }
}

export const store = new Store();
