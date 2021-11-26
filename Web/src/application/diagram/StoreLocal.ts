import LocalData, { ILocalData } from "../../common/LocalData";
import Result, { isError } from "../../common/Result";
import {
  CanvasDto,
  DiagramDto,
  DiagramInfoDto,
  Dto,
  SyncDto,
} from "./StoreDtos";

const diagramKey = "diagram";
const diagramInfoKey = "diagramInfo";
const syncKey = "sync";

export interface IStoreLocal {
  getSync(): SyncDto;
  updateSync(data: Dto): SyncDto;
  tryReadCanvas(diagramId: string, canvasId: string): Result<CanvasDto>;
  updateAccessedDiagram(diagramId: string): void;
  removeDiagram(key: string): void;
  writeDiagram(diagram: DiagramDto): void;
  writeCanvas(canvas: CanvasDto): void;
  writeDiagramInfo(diagramInfo: DiagramInfoDto): void;
  updateWrittenDiagram(diagramId: string): void;
  updateDiagramInfo(diagramId: string, data: Dto): void;
  readAllDiagramsInfos(): DiagramInfoDto[];
  readDiagram(diagramId: string): DiagramDto | null;
  readAllDiagrams(): DiagramDto[];
  clearAllData(): void;
}

export default class StoreLocal implements IStoreLocal {
  canvasKey = (diagramId: string, canvasId: string) =>
    `${diagramKey}.${diagramId}.${canvasId}`;
  diagramKey = (diagramId: string) =>
    `${diagramKey}.${diagramId}.${diagramInfoKey}`;

  private localData: ILocalData = new LocalData();

  clearAllData(): void {
    this.localData.clear();
  }

  getSync(): SyncDto {
    return { token: null } as SyncDto;
    // return this.readData(syncKey) as SyncDto;
  }

  updateSync(data: Dto): SyncDto {
    const sync = { ...this.getSync(), ...data };
    this.localData.write(syncKey, sync);
    return sync;
  }

  readAllDiagrams(): DiagramDto[] {
    return this.readAllDiagramsInfos()
      .map((d) => this.readDiagram(d.diagramId))
      .filter((d) => d != null) as DiagramDto[];
  }

  readCanvases(diagramId: string): CanvasDto[] {
    const keys = [];

    for (var i = 0, len = localStorage.length; i < len; i++) {
      var key = localStorage.key(i);
      if (key?.startsWith(diagramKey)) {
        const parts = key.split(".");
        const id = parts[1];
        const name = parts[2];
        if (id === diagramId && name !== diagramInfoKey) {
          keys.push(key);
        }
      }
    }

    return keys.map((key) => this.localData.read<CanvasDto>(key));
  }

  removeDiagram(diagramId: string): void {
    let keys = [];

    for (var i = 0, len = localStorage.length; i < len; i++) {
      var key = localStorage.key(i);
      if (key?.startsWith(diagramKey)) {
        const parts = key.split(".");
        const id = parts[1];
        if (id === diagramId) {
          keys.push(key);
        }
      }
    }

    keys.forEach((key) => this.localData.remove(key));
  }

  readAllDiagramsInfos(): DiagramInfoDto[] {
    const diagrams = [];
    for (var i = 0, len = localStorage.length; i < len; i++) {
      var key = localStorage.key(i);
      if (key?.endsWith(diagramInfoKey)) {
        const diagramInfo = JSON.parse(localStorage[key]);
        diagrams.push(diagramInfo);
      }
    }

    return diagrams;
  }

  updateAccessedDiagram(diagramId: string): void {
    this.updateDiagramInfo(diagramId, { accessed: Date.now() });
  }

  updateWrittenDiagram(diagramId: string): void {
    const now = Date.now();
    this.updateDiagramInfo(diagramId, { accessed: now, written: now });
  }

  writeDiagram(diagram: DiagramDto): void {
    this.writeDiagramInfo(diagram.diagramInfo);
    diagram.canvases.forEach((canvas) => this.writeCanvas(canvas));
  }

  readDiagram(diagramId: string): DiagramDto | null {
    const diagramInfo = this.readDiagramInfo(diagramId);
    if (isError(diagramInfo)) {
      return null;
    }
    const canvases = this.readCanvases(diagramId);
    const diagram: DiagramDto = {
      diagramInfo: diagramInfo,
      canvases: canvases,
    };

    return diagram;
  }

  readDiagramInfo(diagramId: string): Result<DiagramInfoDto> {
    return this.localData.tryRead<DiagramInfoDto>(this.diagramKey(diagramId));
  }

  writeDiagramInfo(diagramInfo: DiagramInfoDto): void {
    this.localData.write(this.diagramKey(diagramInfo.diagramId), diagramInfo);
  }

  updateDiagramInfo(diagramId: string, data: Dto): void {
    const diagramInfo = this.readDiagramInfo(diagramId);
    if (isError(diagramInfo)) {
      return;
    }
    this.writeDiagramInfo({ ...diagramInfo, ...data });
  }

  tryReadCanvas(diagramId: string, canvasId: string): Result<CanvasDto> {
    return this.localData.tryRead<CanvasDto>(
      this.canvasKey(diagramId, canvasId)
    );
  }

  writeCanvas(canvas: CanvasDto): void {
    const { diagramId, canvasId } = canvas;
    this.localData.write(this.canvasKey(diagramId, canvasId), canvas);
  }
}
