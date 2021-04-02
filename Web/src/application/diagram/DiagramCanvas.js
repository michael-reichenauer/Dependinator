import "import-jquery";
import "jquery-ui-bundle";
import "jquery-ui-bundle/jquery-ui.css";
import PubSub from 'pubsub-js'
import cuid from 'cuid'
import { random } from '../../common/utils'
import Node from './Node'
import { store } from "./Store";
import Canvas from "./Canvas";
import { menuItem } from "../../common/Menus";
import CanvasStack from "./CanvasStack";
import { zoomAndMoveShowTotalDiagram } from "./showTotalDiagram";
import { addDefaultNewDiagram, addFigureToCanvas } from "./addDefault";
import InnerDiagramCanvas from "./InnerDiagramCanvas";
import Printer from "../../common/Printer";


export default class DiagramCanvas {
    static defaultWidth = 100000
    static defaultHeight = 100000

    canvasStack = null
    store = store
    inner = null

    canvas = null;
    callbacks = null

    constructor(htmlElementId, callbacks) {
        this.callbacks = callbacks
        this.canvas = new Canvas(htmlElementId, this.onEditMode, DiagramCanvas.defaultWidth, DiagramCanvas.defaultHeight)
        this.canvasStack = new CanvasStack(this.canvas)
        this.inner = new InnerDiagramCanvas(this.canvas, this.canvasStack, this.store)
    }

    init() {
        this.store.setErrorHandler(this.callbacks.errorHandler)
        this.loadInitialDiagram()

        this.handleDoubleClick(this.canvas)
        this.handleEditChanges(this.canvas)
        this.handleCommands()
    }

    delete() {
        this.canvas.destroy()
    }

    handleCommands = () => {
        PubSub.subscribe('canvas.Undo', () => this.commandUndo())
        PubSub.subscribe('canvas.Redo', () => this.commandRedo())

        PubSub.subscribe('canvas.AddNode', () => this.addNode(Node.nodeType, this.getCenter()))
        PubSub.subscribe('canvas.AddUserNode', () => this.addNode(Node.userType, this.getCenter()))
        PubSub.subscribe('canvas.AddExternalNode', () => this.addNode(Node.externalType, this.getCenter()))
        PubSub.subscribe('canvas.AddDefaultNode', (_, p) => this.addNode(Node.nodeType, p))

        PubSub.subscribe('canvas.ShowTotalDiagram', this.showTotalDiagram)

        PubSub.subscribe('canvas.EditInnerDiagram', this.commandEditInnerDiagram)
        PubSub.subscribe('canvas.PopInnerDiagram', this.commandPopFromInnerDiagram)

        PubSub.subscribe('canvas.SetEditMode', (_, isEditMode) => this.canvas.panPolicy.setEditMode(isEditMode))
        PubSub.subscribe('canvas.NewDiagram', this.commandNewDiagram)
        PubSub.subscribe('canvas.OpenDiagram', this.commandOpenDiagram)
        PubSub.subscribe('canvas.DeleteDiagram', this.commandDeleteDiagram)
        PubSub.subscribe('canvas.SaveDiagramToFile', this.commandSaveToFile)
        PubSub.subscribe('canvas.OpenFile', this.commandOpenFile)
        PubSub.subscribe('canvas.ArchiveToFile', this.commandArchiveToFile)
        PubSub.subscribe('canvas.Print', this.commandPrint)
    }


    getContextMenuItems(x, y) {
        const mouseXY = this.canvas.fromDocumentToCanvasCoordinate(x, y)

        return [
            menuItem('Add node', () => this.addNode(Node.nodeType, mouseXY)),
            menuItem('Add external user', () => this.addNode(Node.userType, mouseXY)),
            menuItem('Add external system', () => this.addNode(Node.externalType, mouseXY)),
            menuItem('Pop to surrounding diagram (dbl-click)', () => PubSub.publish('canvas.PopInnerDiagram'),
                true, !this.canvasStack.isRoot()),
        ]
    }

    commandUndo = () => {
        this.canvas.getCommandStack().undo()
        this.save()
    }

    commandRedo = () => {
        this.canvas.getCommandStack().redo()
    }

    commandNewDiagram = () => {
        //store.loadFile(file => console.log('File:', file))
        this.canvas.clearDiagram()
        this.createNewDiagram()
        this.callbacks.setTitle(this.getTitle())
        this.showTotalDiagram()
    }

    commandOpenDiagram = (msg, diagramId) => {
        const canvasData = this.store.getDiagramRootCanvas(diagramId)
        if (canvasData == null) {
            this.callbacks.errorHandler('Failed to load diagram')
            return
        }
        this.canvas.clearDiagram()

        // Deserialize canvas
        this.canvas.deserialize(canvasData)
        this.callbacks.setTitle(this.getTitle())
        this.showTotalDiagram()
    }

    commandDeleteDiagram = () => {
        this.store.deleteDiagram(this.canvas.diagramId)
        this.canvas.clearDiagram()

        // Try get first diagram to open
        const canvasData = this.store.getFirstDiagramRootCanvas()
        if (canvasData == null) {
            // No data for that id, lets create new diagram
            this.createNewDiagram()
            this.callbacks.setTitle(this.getTitle())
            this.showTotalDiagram()
            return
        }

        // Deserialize canvas
        this.canvas.deserialize(canvasData)
        this.callbacks.setTitle(this.getTitle())
        this.showTotalDiagram()
    }

    commandSaveToFile = () => {
        this.store.saveDiagramToFile(this.canvas.diagramId)
    }

    commandOpenFile = () => {
        this.store.loadDiagramFromFile(diagramId => {
            if (diagramId == null) {
                this.callbacks.errorHandler('Failed to load file')
            }
            this.commandOpenDiagram('', diagramId)
        })
    }

    commandArchiveToFile = () => {
        this.store.saveAllDiagramsToFile()
    }

    commandPrint = () => {
        this.withWorkingIndicator(() => {
            const diagram = this.store.getDiagram(this.canvas.diagramId)

            const pages = diagram.canvases.map(d => this.canvas.exportAsSvg(d))
            const printer = new Printer()
            printer.print(pages)
        })
    }

    commandEditInnerDiagram = (msg, figure) => {
        this.withWorkingIndicator(() => {
            this.inner.editInnerDiagram(figure)
            this.callbacks.setTitle(this.getTitle())
            this.updateToolbarButtonsStates()
            this.save()
        });
    }

    commandPopFromInnerDiagram = () => {
        this.withWorkingIndicator(() => {
            this.inner.popFromInnerDiagram()
            this.callbacks.setTitle(this.getTitle())
            this.updateToolbarButtonsStates()
            this.save()
        });
    }

    onEditMode = (isEditMode) => {
        this.callbacks.setEditMode(isEditMode)

        if (!isEditMode) {
            // Remove grid
            this.canvas.setNormalBackground()
            return
        }

        this.canvas.setGridBackground()
    }

    showTotalDiagram = () => zoomAndMoveShowTotalDiagram(this.canvas)

    addNode = (type, p) => {
        const node = new Node(type)
        addFigureToCanvas(this.canvas, node, p)
    }

    tryGetFigure = (x, y) => {
        let cp = this.canvas.fromDocumentToCanvasCoordinate(x, y)
        let figure = this.canvas.getBestFigure(cp.x, cp.y)
        return figure
    }

    save() {
        // Serialize canvas figures and connections into canvas data object
        const canvasData = this.canvas.serialize();
        this.store.setCanvas(canvasData)
    }

    async loadInitialDiagram() {
        const canvasData = await this.store.getLastUsedCanvas()
        if (canvasData == null) {
            // No data for that id, lets create it
            this.createNewDiagram()
            return
        }
        // Deserialize canvas
        this.canvas.deserialize(canvasData)
        this.callbacks.setTitle(this.getTitle())
    }

    createNewDiagram = () => {
        const diagramId = cuid()
        this.canvas.diagramId = diagramId
        addDefaultNewDiagram(this.canvas)

        const canvasData = this.canvas.serialize();
        this.store.newDiagram(diagramId, this.getName(), canvasData)
    }

    getCenter() {
        let x = (this.canvas.getWidth() / 2 + random(-10, 10) + this.canvas.getScrollLeft()) * this.canvas.getZoom()
        let y = (100 + random(-10, 10) + this.canvas.getScrollTop()) * this.canvas.getZoom()

        return { x: x, y: y }
    }


    handleEditChanges(canvas) {
        this.updateToolbarButtonsStates()

        canvas.commandStack.addEventListener(e => {
            // console.log('event:', e)
            this.updateToolbarButtonsStates()

            if (e.isPostChangeEvent()) {
                // console.log('event isPostChangeEvent:', e)
                if (e.command?.figure?.parent?.id === this.canvas.mainNodeId) {
                    // Update the title whenever the main node changes
                    this.callbacks.setTitle(this.getTitle())
                    this.store.setDiagramName(this.canvas.diagramId, this.getName())
                }

                if (e.action === "POST_EXECUTE") {
                    this.save()
                }
            }
        });
    }

    getTitle() {
        const name = this.getName()
        switch (this.canvasStack.getLevel()) {
            case 0:
                return name + ' - Context'
            case 1:
                return name + ' - Container'
            case 2:
                return name + ' - Component'
            default:
                return name + ' - Code'
        }
    }

    getName() {
        return this.canvas.getFigure(this.canvas.mainNodeId)?.getName() ?? ''
    }

    updateToolbarButtonsStates() {
        this.callbacks.setCanPopDiagram(!this.canvasStack.isRoot())
        this.callbacks.setCanUndo(this.canvas.getCommandStack().canUndo())
        this.callbacks.setCanRedo(this.canvas.getCommandStack().canRedo())
    }

    handleDoubleClick(canvas) {
        canvas.on('dblclick', (emitter, event) => {
            if (event.figure !== null) {
                return
            }

            if (!this.canvasStack.isRoot()) {
                // double click out side group node in inner diagram lets pop
                this.commandPopFromInnerDiagram()
                return
            }

            PubSub.publish('canvas.AddDefaultNode', { x: event.x, y: event.y })
        });
    }

    withWorkingIndicator(action) {
        this.callbacks.setProgress(true)
        setTimeout(() => {
            action()
            this.callbacks.setProgress(false)
        }, 20);
    }
}
