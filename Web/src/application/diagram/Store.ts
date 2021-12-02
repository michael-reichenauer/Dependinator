import LocalFiles, { ILocalFiles } from "../../common/LocalFiles";
import {
  ApplicationDto,
  applicationKey,
  CanvasDto,
  DiagramDto,
  DiagramInfoDto,
} from "./StoreDtos";
import Result, { expectValue, isError } from "../../common/Result";
import LocalData, { ILocalData } from "../../common/LocalData";

// SyncDto
// - isEnabled
// - user, token, ...
//
// ApplicationDto (synced)
//   write stamp
//   State
//   - mru diagram state (canvas id, zoom, center, ...)
//   Diagrams
//   - id
//   - name
//   - read/write stamps
//
// DiagramDto
// - Info
// - Canvases
// - - Canvas
const rootCanvasId = "root";

export interface ResentDiagram {
  id: string;
  name: string;
}

export interface IStore {
  initialize(): Promise<void>;

  getMostResentDiagramId(): Result<string>;
  getRecentDiagrams(): ResentDiagram[];

  tryOpenDiagramRootCanvas(diagramId: string): Promise<Result<CanvasDto>>;
  newDiagram(diagramId: string, name: string, canvas: CanvasDto): void;
  setDiagramName(diagramId: string, name: string): void;
  tryGetDiagram(diagramId: string): Result<DiagramDto>; // Used for print or export

  writeCanvas(canvas: CanvasDto): void;

  deleteDiagram(diagramId: string): Promise<void>;

  saveDiagramToFile(diagramId: string): void;
  loadDiagramFromFile(): Promise<string>;
  saveAllDiagramsToFile(): Promise<void>;
}

class Store implements IStore {
  private localFiles: ILocalFiles;
  // private local: IStoreLocal;
  private localData: ILocalData = new LocalData();

  constructor(localFiles?: ILocalFiles) {
    this.localFiles = localFiles ?? new LocalFiles();
    //   this.local = storeLocal ?? new StoreLocal();
  }

  async initialize(): Promise<void> {
    // return await this.sync.initialize();
  }

  public newDiagram(diagramId: string, name: string, canvas: CanvasDto): void {
    console.log("new diagram", diagramId, name);
    const now = Date.now();

    const diagramInfoDto: DiagramInfoDto = {
      id: diagramId,
      name: name,
      accessed: now,
      written: now,
    };

    const diagramDto: DiagramDto = {
      id: diagramId,
      diagramInfo: diagramInfoDto,
      canvases: [],
    };

    const applicationDto = this.getApplicationDto();
    this.setApplicationDiagramInfo(applicationDto, diagramInfoDto);

    this.localData.writeBatch([applicationDto, diagramDto]);
  }

  public getRecentDiagrams(): ResentDiagram[] {
    return this.getRecentDiagramInfos().map((d) => ({
      id: d.id,
      name: d.name,
    }));
  }

  public async tryOpenDiagramRootCanvas(
    diagramId: string
  ): Promise<Result<CanvasDto>> {
    const diagramDto = this.localData.tryRead<DiagramDto>(diagramId);
    if (isError(diagramDto)) {
      return diagramDto;
    }

    const canvasDto = this.tryGetDiagramCanvas(diagramDto, rootCanvasId);
    if (isError(canvasDto)) {
      return canvasDto;
    }

    const now = Date.now();
    diagramDto.diagramInfo.accessed = now;

    const applicationDto = this.getApplicationDto();
    this.setApplicationDiagramInfo(applicationDto, diagramDto.diagramInfo);

    this.localData.writeBatch([applicationDto, diagramDto]);

    // this.local.updateAccessedDiagram(canvasX.diagramId);
    return canvasDto;
  }

  public writeCanvas(canvasDto: CanvasDto): void {
    const diagramId = canvasDto.diagramId;
    const diagramDto = this.getDiagramDto(diagramId);

    this.setDiagramCanvas(diagramDto, canvasDto);

    const now = Date.now();
    diagramDto.diagramInfo.accessed = now;
    diagramDto.diagramInfo.written = now;

    const applicationDto = this.getApplicationDto();
    this.setApplicationDiagramInfo(applicationDto, diagramDto.diagramInfo);

    this.localData.writeBatch([applicationDto, diagramDto]);
  }

  public async deleteDiagram(diagramId: string): Promise<void> {
    console.log("Delete diagram", diagramId);

    const applicationDto = this.getApplicationDto();
    this.removeApplicationDiagramInfo(applicationDto, diagramId);

    this.localData.writeBatch([applicationDto]);
    this.localData.remove(diagramId);
  }

  public setDiagramName(diagramId: string, name: string): void {
    const now = Date.now();

    const diagramDto = this.getDiagramDto(diagramId);
    diagramDto.diagramInfo.name = name;
    diagramDto.diagramInfo.accessed = now;
    diagramDto.diagramInfo.written = now;

    const applicationDto = this.getApplicationDto();
    this.setApplicationDiagramInfo(applicationDto, diagramDto.diagramInfo);

    this.localData.writeBatch([applicationDto, diagramDto]);
  }

  public async loadDiagramFromFile(): Promise<string> {
    const fileText = await this.localFiles.loadFile();
    const fileDto = JSON.parse(fileText);

    // if (!(await this.sync.uploadDiagrams(fileDto.diagrams))) {
    //   // save locally
    //   fileDto.diagrams.forEach((d: DiagramDto) => this.local.writeDiagram(d));
    // }

    //fileDto.diagrams.forEach((d: DiagramDto) => this.local.writeDiagram(d));

    const firstDiagramId = fileDto.diagrams[0]?.diagramInfo.diagramId;
    if (!firstDiagramId) {
      throw new Error("No diagram in file");
    }
    return firstDiagramId;
  }

  public saveDiagramToFile(diagramId: string): void {
    // const diagram = this.local.readDiagram(diagramId);
    // if (diagram == null) {
    //   return;
    // }
    // const fileDto = { diagrams: [diagram] };
    // const fileText = JSON.stringify(fileDto, null, 2);
    // this.localFiles.saveFile(`${diagram.diagramInfo.name}.json`, fileText);
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

  // For printing/export
  public tryGetDiagram(diagramId: string): Result<DiagramDto> {
    return this.localData.tryRead<DiagramDto>(diagramId);
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

  private getDiagramDto(diagramId: string): DiagramDto {
    return expectValue(this.localData.tryRead<DiagramDto>(diagramId));
  }

  private tryGetDiagramCanvas(
    diagramDto: DiagramDto,
    canvasId: string
  ): Result<CanvasDto> {
    const canvasDto = diagramDto.canvases.find(
      (c: CanvasDto) => c.canvasId === canvasId
    );
    if (canvasDto === null) {
      return new RangeError(
        `Canvas ${canvasId} for not found in ${diagramDto.diagramInfo.id}`
      );
    }
    return canvasDto as CanvasDto;
  }

  private removeDiagramCanvas(diagramDto: DiagramDto, canvasId: string): void {
    const index = diagramDto.canvases.findIndex(
      (c: CanvasDto) => c.canvasId === canvasId
    );
    if (index === -1) {
      return;
    }
    diagramDto.canvases = diagramDto.canvases.splice(index, 1);
  }

  private setDiagramCanvas(diagramDto: DiagramDto, canvasDto: CanvasDto): void {
    const index = diagramDto.canvases.findIndex(
      (c: CanvasDto) => c.canvasId === canvasDto.canvasId
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
      return;
    }

    applicationDto.diagramInfos = applicationDto.diagramInfos.splice(index, 1);
  }
}

export const store = new Store();
