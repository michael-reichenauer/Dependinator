import LocalFiles, { ILocalFiles } from "../../common/LocalFiles";
import StoreLocal, { IStoreLocal } from "./StoreLocal";
import StoreSync, { rootCanvasId } from "./StoreSync";
import { User } from "./Api";
import { CanvasDto, DiagramDto, DiagramInfoDto, SyncDto } from "./StoreDtos";
import Result, { isError } from "../../common/Result";

// User
// State
// - mru diagram
//
// Diagrams
// - id
// - name
// - write stamp
//
// Diagram
// - Info
// - Canvases
// - - Canvas

export interface IStore {
  initialize(): Promise<void>;

  getRecentDiagramInfos(): DiagramInfoDto[];
  openDiagramRootCanvas(diagramId: string): Promise<CanvasDto>;
  openMostResentDiagramCanvas(): Promise<CanvasDto>;
  newDiagram(diagramId: string, name: string, canvas: CanvasDto): Promise<void>;
  setDiagramName(diagramId: string, name: string): void;
  getDiagram(diagramId: string): DiagramDto | null;
  getMostResentDiagramId(): string | null;
  setCanvas(canvas: CanvasDto): void;
  tryGetCanvas(diagramId: string, canvasId: string): Result<CanvasDto>;
  deleteDiagram(diagramId: string): Promise<void>;

  saveDiagramToFile(diagramId: string): void;
  loadDiagramFromFile(): Promise<string>;
  saveAllDiagramsToFile(): Promise<void>;

  serverHadChanges(): Promise<boolean>;
  connectUser(user: User): Promise<void>;
}

class Store implements IStore {
  private localFiles: ILocalFiles;
  private local: IStoreLocal;
  private sync: StoreSync;

  //private isSyncEnabled = false;

  // private isCloudSyncEnabled = () => this.sync.isSyncEnabled;
  // private isLocal = () =>
  //   window.location.hostname === "localhost" ||
  //   window.location.hostname === "127.0.0.1";

  constructor();
  constructor(localFiles?: ILocalFiles, storeLocal?: IStoreLocal) {
    this.localFiles = localFiles ?? new LocalFiles();
    this.local = storeLocal ?? new StoreLocal();
    this.sync = new StoreSync(this, this.local);
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
      const canvasX = this.local.tryReadCanvas(diagramId, rootCanvasId);
      if (isError(canvasX)) {
        throw new Error("Diagram not found");
      }

      this.local.updateAccessedDiagram(canvasX.diagramId);
      return canvasX;
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

  tryGetCanvas(diagramId: string, canvasId: string): Result<CanvasDto> {
    return this.local.tryReadCanvas(diagramId, canvasId);
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
    const fileText = await this.localFiles.loadFile();
    const fileDto = JSON.parse(fileText);

    if (!(await this.sync.uploadDiagrams(fileDto.diagrams))) {
      // save locally
      fileDto.diagrams.forEach((d: DiagramDto) => this.local.writeDiagram(d));
    }

    const firstDiagramId = fileDto.diagrams[0]?.diagramInfo.diagramId;
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

    const fileDto = { diagrams: [diagram] };
    const fileText = JSON.stringify(fileDto, null, 2);
    this.localFiles.saveFile(`${diagram.diagramInfo.name}.json`, fileText);
  }

  async saveAllDiagramsToFile(): Promise<void> {
    let diagrams = await this.sync.downloadAllDiagrams();
    if (!diagrams) {
      // Read from local
      diagrams = this.local.readAllDiagrams();
    }

    const fileDto = { diagrams: diagrams };
    const fileText = JSON.stringify(fileDto, null, 2);
    this.localFiles.saveFile(`diagrams.json`, fileText);
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
