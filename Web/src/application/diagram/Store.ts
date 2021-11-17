import StoreFiles from "./StoreFiles";
import StoreLocal from "./StoreLocal";
import StoreSync, { rootCanvasId } from "./StoreSync";
import { User } from "./Api";
import { CanvasDto, DiagramDto, DiagramInfoDto, SyncDto } from "./StoreDtos";

export class Store {
  files: StoreFiles = new StoreFiles();
  local: StoreLocal = new StoreLocal();
  sync: StoreSync;

  isSyncEnabled = false;

  isCloudSyncEnabled = () => this.sync.isSyncEnabled;
  isLocal = () =>
    window.location.hostname === "localhost" ||
    window.location.hostname === "127.0.0.1";

  constructor() {
    this.sync = new StoreSync(this);
  }

  async initialize(): Promise<void> {
    return await this.sync.initialize();
  }

  async connectUser(user: User): Promise<void> {
    return await this.sync.connectUser(user);
  }

  disableCloudSync(): void {
    this.sync.disableCloudSync();
  }

  async serverHadChanges(): Promise<boolean> {
    return await this.sync.serverHadChanges();
  }

  async checkCloudConnection(): Promise<void> {
    return await this.sync.checkCloudConnection();
  }

  //   async retryCloudConnection() {
  //     return await this.sync.retryCloudConnection();
  //   }

  async openMostResentDiagramCanvas(): Promise<CanvasDto> {
    const diagramId = this.getMostResentDiagramId();
    if (!diagramId) {
      console.log("No recent diagram");
      throw new Error("No resent diagram");
    }

    return await this.openDiagramRootCanvas(diagramId);
  }

  getSync(): SyncDto {
    return this.local.getSync();
  }

  async openDiagramRootCanvas(diagramId: string): Promise<CanvasDto> {
    try {
      let canvas = await this.sync.openDiagramRootCanvas(diagramId);
      if (canvas) {
        // Got diagram via cloud
        return canvas;
      }

      // Local mode: read the root canvas from local store
      canvas = this.local.readCanvas(diagramId, rootCanvasId);
      if (!canvas) {
        throw new Error("Diagram not found");
      }

      this.local.updateAccessedDiagram(canvas.diagramId);
      return canvas;
    } catch (error) {
      this.local.removeDiagram(diagramId);
      throw error;
    } finally {
      await this.sync.syncDiagrams();
    }
  }

  getUniqueSystemName(): string {
    const infos = this.getRecentDiagramInfos();

    for (let i = 0; i < 20; i++) {
      const name = i === 0 ? "System" : `System (${i})`;
      if (!infos.find((info) => name === info.name)) {
        // No other info with that name
        return name;
      }
    }

    // Seems all names are used, lets just reuse System
    return "System";
  }

  getMostResentDiagramId(): string | null {
    return this.getRecentDiagramInfos()[0]?.diagramId;
  }

  async newDiagram(
    diagramId: string,
    name: string,
    canvas: CanvasDto
  ): Promise<void> {
    console.log("new diagram", diagramId, name);
    const now = Date.now();
    const diagram: DiagramDto = {
      diagramInfo: {
        diagramId: diagramId,
        name: name,
        etag: "",
        timestamp: now,
        accessed: now,
        written: now,
      },
      canvases: [canvas],
    };
    this.local.writeDiagram(diagram);

    await this.sync.newDiagram(diagram);
  }

  setCanvas(canvas: CanvasDto): void {
    this.local.writeCanvas(canvas);
    this.local.updateWrittenDiagram(canvas.diagramId);

    this.sync.setCanvas(canvas);
  }

  async deleteDiagram(diagramId: string): Promise<void> {
    console.log("Delete diagram", diagramId);
    this.local.removeDiagram(diagramId);

    await this.sync.deleteDiagram(diagramId);
  }

  setDiagramName(diagramId: string, name: string): void {
    this.local.updateDiagramInfo(diagramId, { name: name });
    this.local.updateWrittenDiagram(diagramId);

    this.sync.setDiagramName(diagramId, name);
  }

  getCanvas(diagramId: string, canvasId: string): CanvasDto {
    return this.local.readCanvas(diagramId, canvasId);
  }

  getRecentDiagramInfos(): DiagramInfoDto[] {
    return this.local
      .readAllDiagramsInfos()
      .sort((i1, i2) =>
        i1.accessed < i2.accessed ? -1 : i1.accessed > i2.accessed ? 1 : 0
      )
      .reverse();
  }

  async loadDiagramFromFile(): Promise<string> {
    const file = await this.files.loadFile();

    if (!(await this.sync.uploadDiagrams(file.diagrams))) {
      // save locally
      file.diagrams.forEach((d: DiagramDto) => this.local.writeDiagram(d));
    }

    const firstDiagramId = file.diagrams[0]?.diagramInfo.diagramId;
    if (!firstDiagramId) {
      throw new Error("No diagram in file");
    }
    return firstDiagramId;
  }

  saveDiagramToFile(diagramId: string): void {
    const diagram = this.local.readDiagram(diagramId);
    if (diagram == null) {
      return;
    }

    const file = { diagrams: [diagram] };
    this.files.saveFile(`${diagram.diagramInfo.name}.json`, file);
  }

  async saveAllDiagramsToFile(): Promise<void> {
    let diagrams = await this.sync.downloadAllDiagrams();
    if (!diagrams) {
      // Read from local
      diagrams = this.local.readAllDiagrams();
    }

    const file = { diagrams: diagrams };
    this.files.saveFile(`diagrams.json`, file);
  }

  clearLocalData(): void {
    this.local.clearAllData();
  }

  async clearRemoteData(): Promise<boolean> {
    return this.sync.clearRemoteData();
  }

  // For printing
  getDiagram(diagramId: string): DiagramDto | null {
    return this.local.readDiagram(diagramId);
  }
}

export const store = new Store();
