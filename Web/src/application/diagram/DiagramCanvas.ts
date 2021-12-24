import "import-jquery";
import "jquery-ui-bundle";
import "jquery-ui-bundle/jquery-ui.css";
import PubSub from "pubsub-js";
import {
  imgDataUrlToPngDataUrl,
  publishAsDownload,
  random,
} from "../../common/utils";
import Node from "./Node";
import { IStore, IStoreKey } from "./Store";
import Canvas from "./Canvas";
import CanvasStack from "./CanvasStack";
import { zoomAndMoveShowTotalDiagram } from "./showTotalDiagram";
import { addDefaultNewDiagram, addFigureToCanvas } from "./addDefault";
import InnerDiagramCanvas from "./InnerDiagramCanvas";
import Printer from "../../common/Printer";
import { setProgress } from "../../common/Progress";
import {
  setErrorMessage,
  setInfoMessage,
  setSuccessMessage,
} from "../../common/MessageSnackbar";
import NodeGroup from "./NodeGroup";
import { greenNumberIconKey } from "../../common/icons";
import NodeNumber from "./NodeNumber";
import { svgToSvgDataUrl, fetchFiles } from "../../common/utils";
import { Canvas2d } from "./draw2dTypes";
import { isError } from "../../common/Result";
import { DiagramDto } from "./StoreDtos";
import { di } from "./../../common/di";

const a4Width = 793.7007874; // "210mm" A4
const a4Height = 1046.9291339; // "277mm" A4
const a4Margin = 50;
const imgMargin = 5;

export default class DiagramCanvas {
  static defaultWidth = 100000;
  static defaultHeight = 100000;

  canvasStack: CanvasStack;
  private store: IStore = di(IStoreKey);
  inner: InnerDiagramCanvas;
  diagramId: string = "";
  diagramName: string = "";

  canvas: Canvas;
  callbacks: any;

  constructor(htmlElementId: string, callbacks: any) {
    this.callbacks = callbacks;
    this.canvas = new Canvas(
      htmlElementId,
      this.onEditMode,
      DiagramCanvas.defaultWidth,
      DiagramCanvas.defaultHeight
    );
    this.canvasStack = new CanvasStack(this.canvas);
    this.inner = new InnerDiagramCanvas(
      this.canvas,
      this.canvasStack,
      this.store
    );
  }

  init() {
    this.loadInitialDiagram();

    this.handleDoubleClick(this.canvas);
    this.handleEditChanges(this.canvas);
    this.handleSelect(this.canvas);
    this.handleCommands();
  }

  delete() {
    this.canvas.destroy();
  }

  handleCommands = () => {
    PubSub.subscribe("canvas.Undo", () => this.commandUndo());
    PubSub.subscribe("canvas.Redo", () => this.commandRedo());

    PubSub.subscribe("canvas.AddNode", (_, data) => this.addNode(data));
    PubSub.subscribe("canvas.ShowTotalDiagram", this.showTotalDiagram);

    PubSub.subscribe("canvas.EditInnerDiagram", this.commandEditInnerDiagram);
    PubSub.subscribe("canvas.TuneSelected", (_, data) =>
      this.commandTuneSelected(data.x, data.y)
    );
    PubSub.subscribe("canvas.PopInnerDiagram", this.commandPopFromInnerDiagram);

    PubSub.subscribe("canvas.SetEditMode", (_, isEditMode) =>
      this.canvas.panPolicy.setEditMode(isEditMode)
    );
    PubSub.subscribe("canvas.NewDiagram", this.commandNewDiagram);
    PubSub.subscribe("canvas.OpenDiagram", this.commandOpenDiagram);
    PubSub.subscribe("canvas.RenameDiagram", this.commandRenameDiagram);
    PubSub.subscribe("canvas.DeleteDiagram", this.commandDeleteDiagram);
    PubSub.subscribe("canvas.SaveDiagramToFile", this.commandSaveToFile);
    PubSub.subscribe("canvas.Save", () => this.save());
    PubSub.subscribe("canvas.OpenFile", this.commandOpenFile);
    PubSub.subscribe("canvas.ArchiveToFile", this.commandArchiveToFile);
    PubSub.subscribe("canvas.Print", this.commandPrint);
    PubSub.subscribe("canvas.Export", (_, data) => this.commandExport(data));
  };

  commandUndo = () => {
    this.canvas.getCommandStack().undo();
    this.save();
  };

  commandRedo = () => {
    this.canvas.getCommandStack().redo();
  };

  commandNewDiagram = async () => {
    setProgress(true);

    //store.loadFile(file => console.log('File:', file))
    console.log("Command new diagram");
    this.canvas.clearDiagram();

    this.showNewDiagram();

    setProgress(false);
  };

  commandOpenDiagram = async (_msg: string, diagramId: string) => {
    setProgress(true);

    console.log("open", diagramId);
    const diagramDto = await this.store.tryOpenDiagram(diagramId);
    if (isError(diagramDto)) {
      setProgress(false);
      setErrorMessage("Failed to load diagram");
      return;
    }

    this.canvas.clearDiagram();
    this.showDiagram(diagramDto);
    setProgress(false);
  };

  commandRenameDiagram = async (_msg: string, name: string) => {
    this.setName(name);
    this.save();
  };

  commandDeleteDiagram = async () => {
    setProgress(true);

    this.store.deleteDiagram(this.diagramId);
    this.canvas.clearDiagram();

    await this.showRecentDiagramOrNew();
    setProgress(false);
  };

  commandSaveToFile = () => {
    this.store.saveDiagramToFile();
  };

  commandOpenFile = async () => {
    setProgress(true);

    const diagramId = await this.store.loadDiagramFromFile();
    if (isError(diagramId)) {
      setErrorMessage("Failed to load file");
      setProgress(false);
      return;
    }

    this.commandOpenDiagram("", diagramId);

    setProgress(false);
  };

  commandArchiveToFile = async () => {
    setProgress(true);
    try {
      this.store.saveAllDiagramsToFile();
    } catch (error) {
      setErrorMessage("Failed to save all diagram");
    } finally {
      setProgress(false);
    }
  };

  commandPrint = () => {
    this.withWorkingIndicator(() => {
      const diagram = this.store.exportDiagram();

      const pages: string[] = Object.values(diagram.canvases).map((d) =>
        this.canvas.exportAsSvg(d, a4Width, a4Height, a4Margin)
      );
      const printer = new Printer();
      printer.print(pages);
    });
  };

  commandExport = (data: any) => {
    this.withWorkingIndicator(() => {
      const diagram = this.store.exportDiagram();
      const diagramName = diagram.name;
      const rect = this.canvas.getFiguresRect();
      const imgWidth = rect.w + imgMargin * 2;
      const imgHeight = rect.h + imgMargin * 2;

      let pages: string[] = Object.values(diagram.canvases).map((d) =>
        this.canvas.exportAsSvg(d, imgWidth, imgHeight, imgMargin)
      );
      let svgText = pages[0];

      // Since icons are nested svg with external links, the links must be replaced with
      // the actual icon image as an dataUrl. Let pars unique urls
      const nestedSvgPaths = this.parseNestedSvgPaths(svgText);

      // Fetch the actual icon svg files
      fetchFiles(nestedSvgPaths, (files) => {
        // Replace all the links with dataUrl of the files.
        svgText = this.replacePathsWithSvgDataUrls(
          svgText,
          nestedSvgPaths,
          files
        );

        // Make one svgDataUrl of the diagram
        let svgDataUrl = svgToSvgDataUrl(svgText);

        if (data.type === "png") {
          imgDataUrlToPngDataUrl(
            svgDataUrl,
            imgWidth,
            imgHeight,
            (pngDataUrl) => {
              publishAsDownload(pngDataUrl, `${diagramName}.png`);
            }
          );
        } else if (data.type === "svg") {
          publishAsDownload(svgDataUrl, `${diagramName}.svg`);
        }
      });
    });
  };

  parseNestedSvgPaths(text: string) {
    const regexp = new RegExp('xlink:href="/static/media[^"]*', "g");

    let uniquePaths: string[] = [];

    let match;
    while ((match = regexp.exec(text)) !== null) {
      const ref = `${match[0]}`;
      const path = ref.substring(12);
      if (!uniquePaths.includes(path)) {
        uniquePaths.push(path);
      }
    }
    return uniquePaths;
  }

  replacePathsWithSvgDataUrls(
    svgText: string,
    paths: string[],
    svgImages: string[]
  ) {
    for (let i = 0; i < paths.length; i++) {
      const path = paths[i];
      const svgImage = svgImages[i];
      const svgDataUrl =
        "data:image/svg+xml;charset=utf-8," + encodeURIComponent(svgImage);
      svgText = svgText.replaceAll(
        `xlink:href="${path}"`,
        `xlink:href="${svgDataUrl}"`
      );
    }
    return svgText;
  }

  commandEditInnerDiagram = (_msg: string, figure: any) => {
    this.withWorkingIndicator(() => {
      this.inner.editInnerDiagram(figure);
      this.callbacks.setTitle(this.diagramName);
      this.updateToolbarButtonsStates();
      this.save();
    });
  };

  commandTuneSelected = (x: number, y: number) => {
    console.log("tune");
    // Get target figure or use canvas as target
    let target = this.canvas.getSelection().primary;

    if (typeof target?.getContextMenuItems !== "function") {
      // No context menu on target
      return;
    }

    const menuItems = target.getContextMenuItems();
    this.callbacks.setContextMenu({ items: menuItems, x: x, y: y });
  };

  commandPopFromInnerDiagram = () => {
    this.withWorkingIndicator(() => {
      this.inner.popFromInnerDiagram();
      this.callbacks.setTitle(this.diagramName);
      this.updateToolbarButtonsStates();
      this.save();
    });
  };

  onEditMode = (isEditMode: boolean) => {
    this.callbacks.setEditMode(isEditMode);
    if (!isEditMode) {
      this.callbacks.setSelectMode(false);
    }

    if (!isEditMode) {
      // Remove grid
      this.canvas.setNormalBackground();
      return;
    }
  };

  showTotalDiagram = () => zoomAndMoveShowTotalDiagram(this.canvas);

  addNode = (data: any) => {
    if (data.group) {
      this.addGroup(data.icon, data.position);
      return;
    }

    if (data.icon === greenNumberIconKey) {
      this.addNumber(data);
      return;
    }
    var { icon, position } = data;
    if (!position) {
      position = this.getCenter();
    }

    var options = null;
    if (icon) {
      options = { icon: icon };
    }

    const node = new Node(Node.nodeType, options);
    const x = position.x - node.width / 2;
    const y = position.y - node.height / 2;

    addFigureToCanvas(this.canvas, node, x, y);
  };

  addGroup = (icon: any, position: any) => {
    const group = new NodeGroup({ icon: icon });
    var x = 0;
    var y = 0;

    if (!position) {
      position = this.getCenter();
      x = position.x - group.width / 2;
      y = position.y - 20;
    } else {
      x = position.x - 20;
      y = position.y - 20;
    }

    addFigureToCanvas(this.canvas, group, x, y);
  };

  addNumber = (data: any) => {
    var { position } = data;
    if (!position) {
      position = this.getCenter();
    }

    const node = new NodeNumber();
    const x = position.x - node.width / 2;
    const y = position.y - node.height / 2;

    addFigureToCanvas(this.canvas, node, x, y);
  };

  tryGetFigure = (x: number, y: number) => {
    let cp = this.canvas.fromDocumentToCanvasCoordinate(x, y);
    let figure = this.canvas.getBestFigure(cp.x, cp.y);
    return figure;
  };

  save() {
    // Serialize canvas figures and connections into canvas data object
    const canvasData = this.canvas.serialize();
    this.store.writeCanvas(canvasData);
  }

  onRemoteChanged = async () => {
    setInfoMessage("Diagram updated by other device");
    setProgress(true);
    this.canvas.clearDiagram();

    await this.showRecentDiagramOrNew();
    setProgress(false);
  };

  onSyncConnectionChange = async (connected: boolean) => {
    if (!connected) {
      setErrorMessage("Connection failed, device sync is disabled");
      return;
    }

    setSuccessMessage("Connection OK, device sync enabled");
  };

  async loadInitialDiagram() {
    setProgress(true);

    this.store.initialize(this.onRemoteChanged, this.onSyncConnectionChange);

    await this.showRecentDiagramOrNew();

    setProgress(false);
  }

  async showRecentDiagramOrNew(): Promise<void> {
    // Get the last used diagram and show
    const diagramId = this.store.getMostResentDiagramId();
    if (isError(diagramId)) {
      this.showNewDiagram();
      return;
    }

    const diagramDto = await this.store.tryOpenDiagram(diagramId);
    if (isError(diagramDto)) {
      this.showNewDiagram();
      return;
    }
    this.showDiagram(diagramDto);
  }

  showDiagram(diagramDto: DiagramDto) {
    const canvasDto = this.store.getRootCanvas();
    this.canvas.deserialize(canvasDto);
    this.diagramId = diagramDto.id;
    this.diagramName = diagramDto.name;
    this.canvas.canvasId = "root";
    this.callbacks.setTitle(diagramDto.name);

    this.showTotalDiagram();
  }

  async showNewDiagram() {
    const diagramDto = this.store.openNewDiagram();

    this.diagramId = diagramDto.id;
    this.diagramName = diagramDto.name;
    this.canvas.canvasId = "root";

    addDefaultNewDiagram(this.canvas);
    this.save();

    this.callbacks.setTitle(this.diagramName);
    this.showTotalDiagram();
  }

  // xxcreateNewDiagram = (): DiagramDto => {
  //   const diagramDto = this.store.newDiagram();

  //   this.canvas.diagramId = diagramDto.id;
  //   this.canvas.diagramName = diagramDto.diagramInfo.name;
  //   this.canvas.canvasId = "root";

  //   addDefaultNewDiagram(this.canvas);
  //   this.save();
  //   return diagramDto;
  // };

  async deactivated() {
    console.log("Diagram deactivated");
    this.store.configure(false);
  }

  async activated() {
    console.log("Diagram activated");
    this.store.configure(true);
    // try {
    //   if (!(await this.store.serverHadChanges())) {
    //     return;
    //   }
    //   const diagramId = this.store.getMostResentDiagramId();
    //   if (!diagramId) {
    //     throw new Error("No resent diagram");
    //   }
    //   this.commandOpenDiagram("", diagramId);
    // } catch (error) {
    //   // No resent diagram data, lets create new diagram
    //   setErrorMessage("Activation error");
    // }
  }

  getCenter() {
    let x =
      (this.canvas.getWidth() / 2 +
        random(-10, 10) +
        this.canvas.getScrollLeft()) *
      this.canvas.getZoom();
    let y =
      (100 + random(-10, 10) + this.canvas.getScrollTop()) *
      this.canvas.getZoom();

    return { x: x, y: y };
  }

  handleEditChanges(canvas: Canvas2d) {
    this.updateToolbarButtonsStates();

    canvas.commandStack.addEventListener((e: any) => {
      // console.log('change event:', e)
      this.updateToolbarButtonsStates();

      if (e.isPostChangeEvent()) {
        // console.log('event isPostChangeEvent:', e)
        // if (e.command?.figure?.parent?.id === this.canvas.mainNodeId) {
        //     // Update the title whenever the main node changes
        //     this.callbacks.setTitle(this.getTitle())
        //     this.store.setDiagramName(this.canvas.diagramId, this.getName())
        // }

        if (e.action === "POST_EXECUTE") {
          // console.log('save')
          this.save();
        }
      }
    });
  }

  setName(name: string) {
    this.diagramName = name;
    this.store.setDiagramName(name);
    this.callbacks.setTitle(name);
  }

  updateToolbarButtonsStates() {
    this.callbacks.setCanPopDiagram(!this.canvasStack.isRoot());
    this.callbacks.setCanUndo(this.canvas.getCommandStack().canUndo());
    this.callbacks.setCanRedo(this.canvas.getCommandStack().canRedo());
  }

  handleDoubleClick(canvas: Canvas2d) {
    canvas.on("dblclick", (_emitter: any, event: any) => {
      if (event.figure !== null) {
        return;
      }

      if (!this.canvasStack.isRoot()) {
        // double click out side group node in inner diagram lets pop
        this.commandPopFromInnerDiagram();
        return;
      }
      PubSub.publish("nodes.showDialog", { add: true, x: event.x, y: event.y });
    });
  }

  handleSelect(canvas: Canvas2d) {
    canvas.on("select", (_emitter: any, event: any) => {
      if (event.figure !== null) {
        this.callbacks.setSelectMode(true);
      } else {
        this.callbacks.setSelectMode(false);
      }
    });
  }

  withWorkingIndicator(action: any) {
    setProgress(true);
    setTimeout(() => {
      action();
      setProgress(false);
    }, 20);
  }
}
