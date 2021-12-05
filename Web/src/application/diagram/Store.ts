import { ILocalFilesKey } from "../../common/LocalFiles";
import {
  ApplicationDto,
  applicationKey,
  CanvasDto,
  DiagramDto,
  DiagramInfoDto,
} from "./StoreDtos";
import Result, { expectValue, isError } from "../../common/Result";
import { ILocalDataKey } from "../../common/LocalData";
import { di } from "../../common/di";
import cuid from "cuid";

const rootCanvasId = "root";

export interface RecentDiagram {
  id: string;
  name: string;
}

export interface IStore {
  initialize(): Promise<void>;

  getMostResentDiagramId(): Result<string>;
  getRecentDiagrams(): RecentDiagram[];

  newDiagram(): DiagramDto;
  tryOpenDiagram(diagramId: string): Promise<Result<DiagramDto>>;

  setDiagramName(diagramId: string, name: string): void;
  tryGetDiagram(diagramId: string): Result<DiagramDto>; // Used for print or export

  getRootCanvas(diagramId: string): CanvasDto;
  getCanvas(diagramId: string, canvasId: string): CanvasDto;
  writeCanvas(diagramId: string, canvas: CanvasDto): void;

  deleteDiagram(diagramId: string): void;

  saveDiagramToFile(diagramId: string): void;
  loadDiagramFromFile(): Promise<string>;
  saveAllDiagramsToFile(): Promise<void>;
}

class Store implements IStore {
  constructor(
    private localData = di(ILocalDataKey),
    private localFiles = di(ILocalFilesKey)
  ) {}

  public async initialize(): Promise<void> {
    // return await this.sync.initialize();
  }

  public newDiagram(): DiagramDto {
    const applicationDto = this.getApplicationDto();

    const diagramId = cuid();
    const name = this.getUniqueName(applicationDto);
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

    this.setApplicationDiagramInfo(applicationDto, diagramInfoDto);

    this.localData.writeBatch([applicationDto, diagramDto]);
    return diagramDto;
  }

  public getRecentDiagrams(): RecentDiagram[] {
    return this.getRecentDiagramInfos().map((d) => ({
      id: d.id,
      name: d.name,
    }));
  }

  public async tryOpenDiagram(diagramId: string): Promise<Result<DiagramDto>> {
    const diagramDto = this.localData.tryRead<DiagramDto>(diagramId);
    if (isError(diagramDto)) {
      return diagramDto;
    }

    const now = Date.now();
    diagramDto.diagramInfo.accessed = now;

    const applicationDto = this.getApplicationDto();
    this.setApplicationDiagramInfo(applicationDto, diagramDto.diagramInfo);

    this.localData.writeBatch([applicationDto, diagramDto]);
    return diagramDto;
  }

  public getRootCanvas(diagramId: string): CanvasDto {
    return this.getCanvas(diagramId, rootCanvasId);
  }

  public getCanvas(diagramId: string, canvasId: string): CanvasDto {
    const diagramDto = expectValue(
      this.localData.tryRead<DiagramDto>(diagramId)
    );
    return expectValue(this.tryGetDiagramCanvas(diagramDto, canvasId));
  }

  public writeCanvas(diagramId: string, canvasDto: CanvasDto): void {
    const diagramDto = this.getDiagramDto(diagramId);

    this.setDiagramCanvas(diagramDto, canvasDto);

    const now = Date.now();
    diagramDto.diagramInfo.accessed = now;
    diagramDto.diagramInfo.written = now;

    const applicationDto = this.getApplicationDto();
    this.setApplicationDiagramInfo(applicationDto, diagramDto.diagramInfo);

    this.localData.writeBatch([applicationDto, diagramDto]);
  }

  public deleteDiagram(diagramId: string): void {
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
      (c: CanvasDto) => c.id === canvasId
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

  private getUniqueName(applicationDto: ApplicationDto): string {
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
