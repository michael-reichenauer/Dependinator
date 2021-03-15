import "import-jquery";
import "jquery-ui-bundle";
import "jquery-ui-bundle/jquery-ui.css";
import draw2d from "draw2d";
import PubSub from 'pubsub-js'
import { random } from '../common/utils'

import Node from './Node'
import Serializer from './serializer'
import { createDefaultConnection } from "./connections";
import { Tweenable } from "shifty"
import { store } from "./store";
import { timing } from "../common/timing";
import { createCanvas, setBackground, setGridBackground } from "./CanvasEx";
import Group from "./Group";


const defaultStoreDiagramName = 'diagram'


export default class Canvas {
    canvasId = null
    canvas = null;
    diagramStack = []
    storeName = defaultStoreDiagramName
    callbacks = null
    store = store
    serializer = null

    constructor(canvasId, callbacks) {
        this.callbacks = callbacks
        this.canvasId = canvasId
        this.canvas = createCanvas(canvasId, this.onEditMode)
        this.serializer = new Serializer(this.canvas)
    }

    init() {
        if (!this.load(this.storeName)) {
            addDefaultNewDiagram(this.canvas)
        }

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
            this.save(this.canvas, this.storName)
        })
        PubSub.subscribe('canvas.Redo', () => {
            this.canvas.getCommandStack().redo();
            this.save(this.canvas, this.storName)
        })

        PubSub.subscribe('canvas.AddNode', () => this.addNode(Node.nodeType, this.getCenter()))
        PubSub.subscribe('canvas.AddUserNode', () => this.addNode(Node.userType, this.getCenter()))
        PubSub.subscribe('canvas.AddExternalNode', () => this.addNode(Node.externalType, this.getCenter()))
        PubSub.subscribe('canvas.AddDefaultNode', (_, p) => this.addNode(Node.nodeType, p))

        PubSub.subscribe('canvas.ShowTotalDiagram', this.showTotalDiagram)

        PubSub.subscribe('canvas.EditInnerDiagram', this.commandEditInnerDiagram)
        PubSub.subscribe('canvas.CloseInnerDiagram', this.commandCloseInnerDiagram)

        PubSub.subscribe('canvas.SetEditMode', (_, isEditMode) => this.canvas.panPolicy.setEditMode(isEditMode))
        PubSub.subscribe('canvas.NewDiagram', this.commandNewDiagram)
    }

    save = (canvas, storeName) => {
        // Serialize canvas figures and connections into canvas data object
        const canvasData = this.serializer.serialize();

        this.store.write(canvasData, storeName)
    }

    load = (storeName) => {
        const canvasData = this.store.read(storeName)
        if (canvasData == null) {
            return false
        }

        // Deserialize canvas
        this.serializer.deserialize(canvasData)
        return true
    }


    commandNewDiagram = () => {
        this.clearDiagram()
        this.store.clear()
        addDefaultNewDiagram(this.canvas)
    }


    commandEditInnerDiagram = (msg, figure) => {
        const innerDiagram = figure.innerDiagram
        if (innerDiagram == null) {
            // Figure has no inner diagram, thus nothing to edit
            return
        }

        this.withWorkingIndicator(() => {
            const t = timing()

            // Remember the current outer zoom, which is used when zooming ghr inner diagram
            const outerZoom = this.canvas.zoomFactor

            // Get the view coordinates of the inner diagram image where the inner diagram should
            // positioned after the switch 
            const innerDiagramViewPos = innerDiagram.getDiagramViewCoordinate()

            // Remove the inner diagram image from figure (will be updated when popping)
            figure.remove(figure.innerDiagram)
            figure.innerDiagram = null

            // Show the actual inner diagram
            this.pushDiagram(figure.getId())
            t.log('pushed diagram')

            // Load inner diagram or a default group node if first time
            if (!this.load(figure.getId())) {
                addDefaultInnerDiagram(this.canvas, figure.getName())
            }
            t.log('loaded diagram')

            // Zoom inner diagram to correspond to inner diagram image size
            this.canvas.setZoom(outerZoom / innerDiagram.innerZoom)

            // Scroll inner diagram to correspond to where the inner diagram image was
            const innerDiagramRect = getCanvasFiguresRect(this.canvas)
            const left = innerDiagramRect.x - innerDiagramViewPos.left * this.canvas.zoomFactor
            const top = innerDiagramRect.y - innerDiagramViewPos.top * this.canvas.zoomFactor
            this.setScrollInCanvasCoordinate(left, top)

            t.log()
        });
    }


    commandCloseInnerDiagram = () => {
        this.withWorkingIndicator(() => {
            const t = timing()

            // Get the inner diagram zoom to use when zooming outer diagram
            const postInnerZoom = this.canvas.zoomFactor

            // Get inner diagram view position to scroll the outer diagram to same position
            const innerDiagramRect = getCanvasFiguresRect(this.canvas)
            const innerDiagramViewPos = this.fromCanvasToViewCoordinate(innerDiagramRect.x, innerDiagramRect.y)

            // Show outer diagram (closing the inner diagram)
            const figureId = this.popDiagram()

            // Update the figures inner diagram image in the node
            const figure = this.canvas.getFigure(figureId)
            figure.showInnerDiagram()

            // Zoom outer diagram to correspond to the inner diagram
            const preInnerZoom = this.canvas.zoomFactor / figure.innerDiagram.innerZoom
            const newZoom = this.canvas.zoomFactor * (postInnerZoom / preInnerZoom)
            this.canvas.setZoom(newZoom)

            // get the inner diagram margin in outer canvas coordinates
            const imx = figure.innerDiagram.marginX * figure.innerDiagram.innerZoom
            const imy = figure.innerDiagram.marginY * figure.innerDiagram.innerZoom

            // Scroll outer diagram to correspond to inner diagram position
            const sx = figure.x + 2 + imx - (innerDiagramViewPos.x * this.canvas.zoomFactor)
            const sy = figure.y + 2 + imy - (innerDiagramViewPos.y * this.canvas.zoomFactor)

            this.setScrollInCanvasCoordinate(sx, sy)
            t.log('popped diagram')
        });
    }

    onEditMode = (isEditMode) => {
        this.callbacks.setEditMode(isEditMode)

        if (!isEditMode) {
            // Remove grid
            setBackground(this.canvas)
            return
        }

        setGridBackground(this.canvas)
    }


    showTotalDiagram = () => zoomAndMoveShowTotalDiagram(this.canvas)

    unselectAll = () => {
        if (!this.canvas.selection.all.isEmpty()) {
            // Deselect items, since zooming with selected figures is slow
            this.canvas.selection.getAll().each((i, f) => f.unselect())
            this.canvas.selection.clear()
        }
    }


    addNode = (type, p) => {
        const node = new Node(type)
        addFigureToCanvas(this.canvas, node, p)
    }


    export = (result) => {
        const rect = getCanvasFiguresRect(this.canvas)
        this.serializer.export(rect, result)
    }


    tryGetFigure = (x, y) => {
        let cp = this.canvas.fromDocumentToCanvasCoordinate(x, y)
        let figure = this.canvas.getBestFigure(cp.x, cp.y)
        return figure
    }


    fromCanvasToViewCoordinate = (x, y) => {
        return new draw2d.geo.Point(
            ((x * (1 / this.canvas.zoomFactor)) - this.canvas.getScrollLeft()),
            ((y * (1 / this.canvas.zoomFactor)) - this.canvas.getScrollTop()))
    }

    fromViewToCanvasCoordinate = (x, y) => {
        return new draw2d.geo.Point(
            (x + this.canvas.getScrollLeft()) * this.canvas.zoomFactor,
            (y + this.canvas.getScrollTop()) * this.canvas.zoomFactor)
    }

    fromDocumentToCanvasCoordinate = (x, y) => {
        return this.canvas.fromDocumentToCanvasCoordinate(x, y)
    }

    setScrollInCanvasCoordinate = (left, top) => {
        const area = this.canvas.getScrollArea()
        area.scrollLeft(left / this.canvas.zoomFactor)
        area.scrollTop(top / this.canvas.zoomFactor)
    }

    getScrollInCanvasCoordinate = () => {
        const area = this.canvas.getScrollArea()
        return { left: area.scrollLeft() * this.canvas.zoomFactor, top: area.scrollTop() * this.canvas.zoomFactor }
    }

    getCenter = () => {
        let x = (this.canvas.getWidth() / 2 + random(-10, 10) + this.canvas.getScrollLeft()) * this.canvas.getZoom()
        let y = (100 + random(-10, 10) + this.canvas.getScrollTop()) * this.canvas.getZoom()

        return { x: x, y: y }
    }

    clearDiagram = () => {
        const canvas = this.canvas
        canvas.lines.clone().each(function (i, e) {
            canvas.remove(e)
        })

        canvas.figures.clone().each(function (i, e) {
            canvas.remove(e)
        })


        canvas.selection.clear()
        canvas.currentDropTarget = null
        canvas.figures = new draw2d.util.ArrayList()
        canvas.lines = new draw2d.util.ArrayList()
        canvas.commonPorts = new draw2d.util.ArrayList()
        canvas.commandStack.markSaveLocation()
        canvas.linesToRepaintAfterDragDrop = new draw2d.util.ArrayList()
        canvas.lineIntersections = new draw2d.util.ArrayList()
    }

    pushDiagram = (newStoreName) => {
        const canvas = this.canvas
        const area = canvas.getScrollArea()
        const canvasData = {
            storeName: this.storeName,
            zoom: canvas.zoomFactor,
            x: area.scrollLeft(),
            y: area.scrollTop(),
            lines: canvas.lines.clone(),
            figures: canvas.figures.clone(),
            commonPorts: canvas.commonPorts,
            commandStack: canvas.commandStack,
            linesToRepaintAfterDragDrop: canvas.linesToRepaintAfterDragDrop,
            lineIntersections: canvas.lineIntersections,
            figureId: newStoreName
        }

        canvasData.lines.each(function (i, e) {
            canvas.remove(e)
        })

        canvasData.figures.each(function (i, e) {
            canvas.remove(e)
        })


        canvas.selection.clear()
        canvas.currentDropTarget = null
        canvas.figures = new draw2d.util.ArrayList()
        canvas.lines = new draw2d.util.ArrayList()
        canvas.commonPorts = new draw2d.util.ArrayList()

        // canvas.commandStack.markSaveLocation()
        canvas.commandStack = new draw2d.command.CommandStack()
        this.handleEditChanges(canvas)

        canvas.linesToRepaintAfterDragDrop = new draw2d.util.ArrayList()
        canvas.lineIntersections = new draw2d.util.ArrayList()

        this.diagramStack.push(canvasData)
        this.storeName = newStoreName
        this.callbacks.setCanPopDiagram(true)
    }


    popDiagram = () => {
        if (this.diagramStack.length === 0) {
            return
        }
        const canvas = this.canvas
        canvas.lines.clone().each(function (i, e) {
            canvas.remove(e)
        })

        canvas.figures.clone().each(function (i, e) {
            canvas.remove(e)
        })


        const canvasData = this.diagramStack.pop()
        this.callbacks.setCanPopDiagram(this.diagramStack.length > 0)

        canvas.selection.clear()
        canvas.currentDropTarget = null

        canvas.figures = new draw2d.util.ArrayList()
        canvas.lines = new draw2d.util.ArrayList()
        canvas.setZoom(canvasData.zoom)
        const area = canvas.getScrollArea()
        area.scrollLeft(canvasData.x)
        area.scrollTop(canvasData.y)


        canvasData.figures.each(function (i, e) {
            canvas.add(e)
        })

        canvasData.lines.each(function (i, e) {
            canvas.add(e)
        })


        canvas.commonPorts = canvasData.commonPorts
        canvas.commandStack = canvasData.commandStack
        this.callbacks.setCanUndo(canvas.getCommandStack().canUndo())
        this.callbacks.setCanRedo(canvas.getCommandStack().canRedo())

        canvas.linesToRepaintAfterDragDrop = new draw2d.util.ArrayList()
        canvas.lineIntersections = new draw2d.util.ArrayList()

        this.storeName = canvasData.storeName
        return canvasData.figureId
    }

    handleEditChanges = (canvas) => {
        this.callbacks.setCanUndo(canvas.commandStack.canUndo())
        this.callbacks.setCanRedo(canvas.commandStack.canRedo())

        canvas.commandStack.addEventListener(e => {
            // console.log('event:', e)
            this.callbacks.setCanUndo(canvas.commandStack.canUndo())
            this.callbacks.setCanRedo(canvas.commandStack.canRedo())

            if (e.isPostChangeEvent()) {
                // console.log('event isPostChangeEvent:', e)
                if (e.action === "POST_EXECUTE") {
                    this.save(this.canvas, this.storeName)
                }
            }
        });
    }

    handleDoubleClick(canvas) {
        canvas.on('dblclick', (emitter, event) => {
            if (event.figure !== null) {
                return
            }

            if (this.diagramStack.length > 0) {
                // double click out side group node in inner diagram lets pop
                this.commandCloseInnerDiagram()
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


const addFigureToCanvas = (canvas, figure, p) => {
    hidePortsIfReadOnly(canvas, figure)

    const command = new draw2d.command.CommandAdd(canvas, figure, p.x - figure.width / 2, p.y - figure.height / 2);
    canvas.getCommandStack().execute(command);
}

const addConnectionToCanvas = (canvas, connection) => {
    const command = new draw2d.command.CommandAdd(canvas, connection, 0, 0);
    canvas.getCommandStack().execute(command);
}


const hidePortsIfReadOnly = (canvas, figure) => {
    if (canvas.isReadOnlyMode) {
        figure.getPorts().each((i, port) => { port.setVisible(false) })
    }
}

const addDefaultNewDiagram = (canvas) => {
    const user = new Node(Node.userType)
    const system = new Node(Node.nodeType)
    const external = new Node(Node.externalType)
    const b = canvas.getDimension().getWidth() / 2
    addFigureToCanvas(canvas, user, { x: b + 200, y: b + 400 })
    addFigureToCanvas(canvas, system, { x: b + 600, y: b + 400 })
    addConnectionToCanvas(canvas, createDefaultConnection(user, 'output0', system, 'input0'))
    addFigureToCanvas(canvas, external, { x: b + 1000, y: b + 400 })
    addConnectionToCanvas(canvas, createDefaultConnection(system, 'output0', external, 'input0'))

    zoomAndMoveShowTotalDiagram(canvas)
}

const addDefaultInnerDiagram = (canvas, name) => {
    const group = new Group(name)
    const d = canvas.getDimension()
    const x = d.getWidth() / 2 + (canvas.getWidth() - 1000) / 2
    const y = d.getHeight() / 2 + 250
    canvas.add(group, x, y)
}

const zoomAndMoveShowTotalDiagram = (canvas) => {
    if (!canvas.selection.all.isEmpty()) {
        // Deselect items, since zooming with selected figures is slow
        canvas.selection.getAll().each((i, f) => f.unselect())
        canvas.selection.clear()
    }

    moveToShowTotalDiagram(canvas, () => zoomToShowTotalDiagram(canvas))
}

const moveToShowTotalDiagram = (canvas, done) => {
    const area = canvas.getScrollArea()

    const { x, y, w, h } = getCanvasFiguresRect(canvas)

    const zoom = canvas.zoomFactor
    const fc = { x: (x + w / 2) / zoom, y: (y + h / 2) / zoom }
    const cc = { x: canvas.getWidth() / 2, y: canvas.getHeight() / 2 }

    const tp = { x: fc.x - cc.x, y: fc.y - cc.y }

    let tweenable = new Tweenable()
    tweenable.tween({
        from: { x: area.scrollLeft(), y: area.scrollTop() },
        to: { x: (tp.x), y: tp.y },
        duration: 500,
        easing: "easeOutSine",
        step: state => {
            area.scrollLeft(state.x)
            area.scrollTop(state.y)
        },
        finish: state => {
            done()
        }
    })
}

const zoomToShowTotalDiagram = (canvas) => {
    const area = canvas.getScrollArea()

    const { x, y, w, h } = getCanvasFiguresRect(canvas)

    const fc = { x: x + w / 2, y: y + h / 2 }
    const cc = { x: canvas.getWidth() / 2, y: canvas.getHeight() / 2 }

    const targetZoom = Math.max(1, w / (canvas.getWidth() - 100), h / (canvas.getHeight() - 100))

    let tweenable = new Tweenable()
    tweenable.tween({
        from: { 'zoom': canvas.zoomFactor },
        to: { 'zoom': targetZoom },
        duration: 500,
        easing: "easeOutSine",
        step: state => {
            canvas.setZoom(state.zoom, false)

            // Adjust scroll to center, since canvas zoom lacks zoom at center point
            const tp = { x: fc.x - cc.x * state.zoom, y: fc.y - cc.y * state.zoom }
            area.scrollLeft((tp.x) / state.zoom)
            area.scrollTop((tp.y) / state.zoom)
        },
        finish: state => {
        }
    })
}


export const getCanvasFiguresRect = (canvas) => {
    let minX = 10000
    let minY = 10000
    let maxX = 0
    let maxY = 0

    canvas.getFigures().each((i, f) => {
        let fx = f.getAbsoluteX()
        let fy = f.getAbsoluteY()
        let fx2 = fx + f.getWidth()
        let fy2 = fy + f.getHeight()

        if (i === 0) {
            minX = fx
            minY = fy
            maxX = fx2
            maxY = fy2
            return
        }

        if (fx < minX) {
            minX = fx
        }
        if (fy < minY) {
            minY = fy
        }
        if (fx2 > maxX) {
            maxX = fx2
        }
        if (fy2 > maxY) {
            maxY = fy2
        }
    })

    return { x: minX, y: minY, w: maxX - minX, h: maxY - minY, x2: maxX, y2: maxY }
}