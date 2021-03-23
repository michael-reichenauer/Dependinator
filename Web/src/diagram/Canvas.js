import "import-jquery";
import "jquery-ui-bundle";
import "jquery-ui-bundle/jquery-ui.css";
import PubSub from 'pubsub-js'
import { random } from '../common/utils'
import Node from './Node'
import Serializer from './serializer'
import { store } from "./store";
import CanvasEx from "./CanvasEx";
import { Item } from "../common/ContextMenu";
import CanvasStack from "./CanvasStack";
import { zoomAndMoveShowTotalDiagram } from "./showTotalDiagram";
import { addDefaultNewDiagram, addFigureToCanvas, setSystemNodeReadOnly } from "./addDefault";
import { InnerDiagramCanvas } from "./InnerDiagramCanvas";


export default class Canvas {
    static defaultWidth = 100000
    static defaultHeight = 100000

    canvasStack = null
    serializer = null
    store = store
    inner = null

    canvas = null;
    callbacks = null

    constructor(canvasId, callbacks) {
        this.callbacks = callbacks
        this.canvas = new CanvasEx(canvasId, this.onEditMode, Canvas.defaultWidth, Canvas.defaultHeight)
        this.canvas.canvas = this
        this.serializer = new Serializer(this.canvas)
        this.canvasStack = new CanvasStack(this.canvas)
        this.inner = new InnerDiagramCanvas(this.canvas, this.canvasStack, this.store, this.serializer)
    }

    init() {
        if (!this.load(this.canvas.name)) {
            addDefaultNewDiagram(this.canvas)
        }
        const systemNode = setSystemNodeReadOnly(this.canvas)
        this.callbacks.setTitle(systemNode.getName())


        this.handleDoubleClick(this.canvas)
        this.handleEditChanges(this.canvas)
        this.handleCommands()
    }


    delete() {
        this.canvas.destroy()
    }

    handleCommands = () => {
        PubSub.subscribe('canvas.Undo', () => {
            this.canvas.getCommandStack().undo();
            this.save(this.canvas.name)
        })
        PubSub.subscribe('canvas.Redo', () => {
            this.canvas.getCommandStack().redo();
            this.save(this.canvas.name)
        })

        PubSub.subscribe('canvas.AddNode', () => this.addNode(Node.nodeType, this.getCenter()))
        PubSub.subscribe('canvas.AddUserNode', () => this.addNode(Node.userType, this.getCenter()))
        PubSub.subscribe('canvas.AddExternalNode', () => this.addNode(Node.externalType, this.getCenter()))
        PubSub.subscribe('canvas.AddDefaultNode', (_, p) => this.addNode(Node.nodeType, p))

        PubSub.subscribe('canvas.ShowTotalDiagram', this.showTotalDiagram)

        PubSub.subscribe('canvas.EditInnerDiagram', this.commandEditInnerDiagram)
        PubSub.subscribe('canvas.PopInnerDiagram', this.commandPopFromInnerDiagram)

        PubSub.subscribe('canvas.SetEditMode', (_, isEditMode) => this.canvas.panPolicy.setEditMode(isEditMode))
        PubSub.subscribe('canvas.NewDiagram', this.commandNewDiagram)
    }

    getContextMenuItems(x, y) {
        const pos = this.canvas.fromDocumentToCanvasCoordinate(x, y)

        return [
            new Item('Add node', () => this.addNode(Node.nodeType, pos)),
            new Item('Add external user', () => this.addNode(Node.userType, pos)),
            new Item('Add external system', () => this.addNode(Node.externalType, pos)),
            new Item('Pop to surrounding diagram (dbl-click)', () => PubSub.publish('canvas.PopInnerDiagram'),
                true, !this.canvasStack.isRoot()),
        ]
    }

    save = (storeName) => {
        console.log('save', storeName)
        // Serialize canvas figures and connections into canvas data object
        const canvasData = this.serializer.serialize();

        this.store.write(canvasData, storeName)
    }

    load = (storeName) => {
        console.log('load', storeName)
        const canvasData = this.store.read(storeName)
        if (canvasData == null) {
            return false
        }

        // Deserialize canvas
        this.serializer.deserialize(canvasData)
        return true
    }


    commandNewDiagram = () => {
        this.canvas.clearDiagram()
        this.store.clear()
        addDefaultNewDiagram(this.canvas)
    }


    commandEditInnerDiagram = (msg, figure) => {
        this.withWorkingIndicator(() => {
            this.inner.editInnerDiagram(figure)
            this.updateToolbarButtonsStates()
            this.save(this.canvas.name)
        });
    }

    commandPopFromInnerDiagram = () => {
        this.withWorkingIndicator(() => {
            this.inner.popFromInnerDiagram()
            this.updateToolbarButtonsStates()
            this.save(this.canvas.name)
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


    export = (result) => {
        const rect = this.canvas.getFiguresRect()
        this.serializer.export(rect, result)
    }


    tryGetFigure = (x, y) => {
        let cp = this.canvas.fromDocumentToCanvasCoordinate(x, y)
        let figure = this.canvas.getBestFigure(cp.x, cp.y)
        return figure
    }


    getCenter = () => {
        let x = (this.canvas.getWidth() / 2 + random(-10, 10) + this.canvas.getScrollLeft()) * this.canvas.getZoom()
        let y = (100 + random(-10, 10) + this.canvas.getScrollTop()) * this.canvas.getZoom()

        return { x: x, y: y }
    }


    handleEditChanges = (canvas) => {
        this.updateToolbarButtonsStates()

        canvas.commandStack.addEventListener(e => {
            //console.log('event:', e)
            this.updateToolbarButtonsStates()

            if (e.isPostChangeEvent()) {
                // console.log('event isPostChangeEvent:', e)
                if (e.action === "POST_EXECUTE") {
                    if (e.command?.figure?.parent?.getId() === 'system') {
                        // Update the title whenever the system node name changes
                        this.callbacks.setTitle(e.command.figure.parent.getName())
                    }
                    this.save(canvas.name)
                }
            }
        });
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

    withWorkingIndicator = (action) => {
        this.callbacks.setProgress(true)
        setTimeout(() => {
            action()
            this.callbacks.setProgress(false)
        }, 20);
    }
}
