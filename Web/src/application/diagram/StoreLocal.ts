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

export default class StoreLocal {
  canvasKey = (diagramId: string, canvasId: string) =>
    `${diagramKey}.${diagramId}.${canvasId}`;
  diagramKey = (diagramId: string) =>
    `${diagramKey}.${diagramId}.${diagramInfoKey}`;

  clearAllData(): void {
    localStorage.clear();
  }

  getSync(): SyncDto {
    return this.readData(syncKey) ?? {};
  }

  updateSync(data: SyncDto): SyncDto {
    const sync = { ...this.getSync(), ...data };
    this.writeData(syncKey, sync);
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

    return keys
      .map((key) => this.readData(key))
      .filter((data) => data != null) as CanvasDto[];
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

    keys.forEach((key) => this.removeData(key));
  }

  readAllDiagramsInfos() {
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
    if (diagramInfo == null) {
      return null;
    }
    const canvases = this.readCanvases(diagramId);
    const diagram: DiagramDto = {
      diagramInfo: diagramInfo,
      canvases: canvases,
    };

    return diagram;
  }

  readDiagramInfo(diagramId: string): DiagramInfoDto {
    return this.readData(this.diagramKey(diagramId)) as DiagramInfoDto;
  }

  writeDiagramInfo(diagramInfo: DiagramInfoDto): void {
    this.writeData(this.diagramKey(diagramInfo.diagramId), diagramInfo);
  }

  updateDiagramInfo(diagramId: string, data: Dto): void {
    const diagramInfo = this.readDiagramInfo(diagramId);
    if (diagramInfo == null) {
      return;
    }
    this.writeDiagramInfo({ ...diagramInfo, ...data });
  }

  readCanvas(diagramId: string, canvasId: string): CanvasDto {
    return this.readData(this.canvasKey(diagramId, canvasId)) as CanvasDto;
  }

  writeCanvas(canvas: CanvasDto) {
    const { diagramId, canvasId } = canvas;
    this.writeData(this.canvasKey(diagramId, canvasId), canvas);
  }

  readData(key: string): Dto | null {
    let text = localStorage.getItem(key);
    if (text == null) {
      return null;
    }
    return JSON.parse(text);
  }

  writeData(key: string, data: Dto) {
    const text = JSON.stringify(data);
    localStorage.setItem(key, text);
  }

  removeData(key: string): void {
    localStorage.removeItem(key);
  }
}
