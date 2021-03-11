import "import-jquery";
import "jquery-ui-bundle";
import "jquery-ui-bundle/jquery-ui.css";
import draw2d from "draw2d";
import { WheelZoomPolicy } from "./WheelZoomPolicy"
import { ConnectionCreatePolicy } from "./ConnectionCreatePolicy"
import { random } from '../common/utils'
import {
    createDefaultNode, createDefaultUserNode, createDefaultExternalNode,
    createDefaultSystemNode, getCanvasFiguresRect, getFigureName, createDefaultGroupNode,
    showInnerDiagram
} from './figures'
import { exportCanvas } from './serialization'
import { canvasDivBackground } from "./colors";
import { createDefaultConnection } from "./connections";
import { Tweenable } from "shifty"
import { clearStoredDiagram, loadDiagram, saveDiagram } from "./store";
import { timing } from "../common/timing";
import { CanvasEx } from './CanvasEx'
import { PanPolicy } from "./PanPolicy";

const defaultStoreDiagramName = 'diagram'
const diagramSize = 100000

export default class Canvas {
    canvasId = null
    canvas = null;
    diagramStack = []
    storeName = defaultStoreDiagramName
    setCanUndo = null
    setCanRedo = null


    constructor(canvasId, setCanUndo, setCanRedo, setProgress, setCanPopDiagram, setEditMode) {
        this.setCanUndo = setCanUndo
        this.setCanRedo = setCanRedo
        this.setProgress = setProgress
        this.setCanPopDiagram = setCanPopDiagram
        this.setEditMode = setEditMode
        this.canvas = this.createCanvas(canvasId)
    }


    delete() {
        this.canvas.destroy()
    }


    createCanvas(canvasId) {
        this.canvasId = canvasId
        const canvas = new CanvasEx(canvasId)
        canvas.setScrollArea("#" + canvasId)
        canvas.setDimension(new draw2d.geo.Rectangle(0, 0, diagramSize, diagramSize))
        canvas.regionDragDropConstraint.constRect = new draw2d.geo.Rectangle(0, 0, diagramSize, diagramSize)

        const area = canvas.getScrollArea()
        area.scrollLeft(diagramSize / 2)
        area.scrollTop(diagramSize / 2)

        if (!loadDiagram(canvas, this.storeName)) {
            addDefaultNewDiagram(canvas)
        }

        canvas.on('dblclick', (emitter, event) => {
            if (event.figure !== null) {
                return
            }

            if (this.diagramStack.length > 0) {
                // double click out side group node in inner diagram lets pop
                this.commandCloseInnerDiagram()
                return
            }
            // console.log('abs', this.canvas.getAbsoluteX(), this.canvas.getAbsoluteY())
            // console.log('mouse', mouseX, mouseY)
            // const doc = this.canvas.fromCanvasToDocumentCoordinate(mouseX, mouseY)
            // console.log('doc', doc)
            // console.log('scroll inner', this.getScrollInCanvasCoordinate())
            this.addDefaultItem(event.x, event.y)
        });

        canvas.panPolicy = new PanPolicy(this.onEditMode)
        canvas.installEditPolicy(canvas.panPolicy)

        this.zoomPolicy = new WheelZoomPolicy()
        canvas.installEditPolicy(this.zoomPolicy);
        canvas.installEditPolicy(new ConnectionCreatePolicy())
        canvas.installEditPolicy(new draw2d.policy.canvas.CoronaDecorationPolicy());

        canvas.html.find("svg").css('background-color', canvasDivBackground)

        canvas.installEditPolicy(new draw2d.policy.canvas.SnapToGeometryEditPolicy())
        canvas.installEditPolicy(new draw2d.policy.canvas.SnapToInBetweenEditPolicy())
        canvas.installEditPolicy(new draw2d.policy.canvas.SnapToCenterEditPolicy())
        //canvas.installEditPolicy(new draw2d.policy.canvas.SnapToGridEditPolicy(10, false))

        this.enableCommandStackHandler(canvas.commandStack)

        return canvas
    }

    commandSetEditMode = () => {
        this.canvas.panPolicy.setEditMode(true)
    }

    commandSetReadOnlyMode = () => {
        this.canvas.panPolicy.setEditMode(false)
    }


    fromCanvasToScreenViewCoordinate = (x, y) => {
        return new draw2d.geo.Point(
            ((x * (1 / this.canvas.zoomFactor)) - this.canvas.getScrollLeft()),
            ((y * (1 / this.canvas.zoomFactor)) - this.canvas.getScrollTop()))
    }

    fromScreenViewToCanvasCoordinate = (x, y) => {
        return new draw2d.geo.Point(
            (x + this.canvas.getScrollLeft()) * this.canvas.zoomFactor,
            (y + this.canvas.getScrollTop()) * this.canvas.zoomFactor)
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


    onEditMode = (isEditMode) => {
        this.setEditMode(isEditMode)
        if (!isEditMode) {
            this.canvas.html.find("svg").css({
                'background-color': canvasDivBackground,
                "background": canvasDivBackground,
                "background-size": 0
            })
            return
        }

        const bgColor = canvasDivBackground
        const color = new draw2d.util.Color('#E0E3E3').rgba()
        const interval = 10
        const gridStroke = 1

        let background =
            ` linear-gradient(to right,  ${color} ${gridStroke}px, transparent ${gridStroke}px),
              linear-gradient(to bottom, ${color} ${gridStroke}px, ${bgColor}  ${gridStroke}px)`
        let backgroundSize = `${interval}px ${interval}px`

        this.canvas.html.find("svg").css({
            "background": background,
            "background-size": backgroundSize
        })
    }

    getId = () => this.canvas.canvasId

    showTotalDiagram = () => zoomAndMoveShowTotalDiagram(this.canvas)


    commandUndo = () => {
        this.canvas.getCommandStack().undo();
        saveDiagram(this.canvas, this.storName)
    }

    commandRedo = () => {
        this.canvas.getCommandStack().redo();
        saveDiagram(this.canvas, this.storName)
    }


    commandAddNode = () => this.addFigure(createDefaultNode(), this.randomCenterPoint())
    commandAddUserNode = () => this.addFigure(createDefaultUserNode(), this.randomCenterPoint())
    commandAddExternalNode = () => this.addFigure(createDefaultExternalNode(), this.randomCenterPoint())

    commandAddDefaultItem = (msg, p) => {
        this.addDefaultItem(p.x, p.y)
    }

    addDefaultItem = (x, y, shiftKey, ctrlKey) => this.addFigure(createDefaultNode(), { x: x, y: y })


    commandNewDiagram = () => {
        this.clearDiagram()
        clearStoredDiagram()
        console.log('Cleared all stored')
        addDefaultNewDiagram(this.canvas)
    }

    commandEditInnerDiagram = (msg, figure) => {
        const innerDiagram = figure.innerDiagram
        if (innerDiagram == null) {
            return
        }

        this.setProgress(true)

        setTimeout(() => {
            const t = timing()

            const zoomFactor = this.canvas.zoomFactor

            // get the inner diagram margin in outer canvas coordinates
            const imx = innerDiagram.marginX * innerDiagram.innerZoom
            const imy = innerDiagram.marginY * innerDiagram.innerZoom

            // get the inner diagram pos in outer coordinates
            const innerScroll = this.getScrollInCanvasCoordinate()
            const xd = (figure.x + 2 + imx - innerScroll.left) / zoomFactor
            const yd = (figure.y + 2 + imy - innerScroll.top) / zoomFactor

            // Remove the inner diagram image from figure (will be updated when popping)
            figure.remove(figure.innerDiagram)
            figure.innerDiagram = null

            // Show the inner diagram
            this.pushDiagram(figure.getId())
            t.log('pushed diagram')

            // Load inner diagram or a default group node if first time
            if (!loadDiagram(this.canvas, figure.getId())) {
                const group = createDefaultGroupNode(getFigureName(figure))
                const width = this.canvas.getWidth()
                const x = diagramSize / 2 + (width - 1000) / 2
                this.canvas.add(group, x, diagramSize / 2 + 250)
            }
            t.log('loaded diagram')

            // Zoom and scroll inner diagram to correspond to outer diagram
            const b = getCanvasFiguresRect(this.canvas)
            this.canvas.setZoom(zoomFactor / innerDiagram.innerZoom)

            this.setScrollInCanvasCoordinate(b.x - xd * this.canvas.zoomFactor, b.y - yd * this.canvas.zoomFactor)
            this.setProgress(false)
            t.log()
        }, 30);
    }


    commandCloseInnerDiagram = () => {
        this.setProgress(true)
        setTimeout(() => {
            const t = timing()

            // Remember inner diagram zoom and position relative screen view
            const postInnerZoom = this.canvas.zoomFactor
            const innerDiagramBox = getCanvasFiguresRect(this.canvas)
            const b = this.fromCanvasToScreenViewCoordinate(innerDiagramBox.x, innerDiagramBox.y)

            // Show outer diagram (closing the inner diagram)
            const figureId = this.popDiagram()

            // Update the figures inner diagram image 
            const figure = this.canvas.getFigure(figureId)
            showInnerDiagram(figure)

            // Zoom outer diagram to correspond to the inner diagram
            const preInnerZoom = this.canvas.zoomFactor / figure.innerDiagram.innerZoom
            const newZoom = this.canvas.zoomFactor * (postInnerZoom / preInnerZoom)
            this.canvas.setZoom(newZoom)

            // get the inner diagram margin in outer canvas coordinates
            const imx = figure.innerDiagram.marginX * figure.innerDiagram.innerZoom
            const imy = figure.innerDiagram.marginY * figure.innerDiagram.innerZoom

            // Scroll outer diagram to correspond to inner diagram position
            const sx = figure.x + 2 + imx - (b.x * this.canvas.zoomFactor)
            const sy = figure.y + 2 + imy - (b.y * this.canvas.zoomFactor)
            this.setScrollInCanvasCoordinate(sx, sy)

            this.setProgress(false)
            t.log('popped diagram')
        }, 30);
    }


    unselectAll = () => {
        if (!this.canvas.selection.all.isEmpty()) {
            // Deselect items, since zooming with selected figures is slow
            this.canvas.selection.getAll().each((i, f) => f.unselect())
            this.canvas.selection.clear()
        }
    }


    addFigure = (figure, p) => {
        addFigureToCanvas(this.canvas, figure, p)
        this.enableEditMode()
    }

    export = (result) => {
        const rect = getCanvasFiguresRect(this.canvas)
        exportCanvas(this.canvas, rect, result)
    }

    tryGetFigure = (x, y) => {
        let cp = this.toCanvasCoordinate(x, y)
        let figure = this.canvas.getBestFigure(cp.x, cp.y)
        return figure
    }

    toCanvasCoordinate = (x, y) => {
        return this.canvas.fromDocumentToCanvasCoordinate(x, y)
    }


    randomCenterPoint = () => {
        let x = (this.canvas.getWidth() / 2 + random(-10, 10) + this.canvas.getScrollLeft()) * this.canvas.getZoom()
        let y = (this.canvas.getHeight() / 2 + random(-10, 10) + this.canvas.getScrollTop()) * this.canvas.getZoom()

        return { x: x, y: y }
    }


    enableEditMode = () => {
        if (!this.canvas.isReadOnlyMode) {
            return
        }
        this.togglePanPolicy()
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
        this.enableCommandStackHandler(canvas.commandStack)

        this.setCanUndo(canvas.getCommandStack().canUndo())
        this.setCanRedo(canvas.getCommandStack().canRedo())

        canvas.linesToRepaintAfterDragDrop = new draw2d.util.ArrayList()
        canvas.lineIntersections = new draw2d.util.ArrayList()

        this.diagramStack.push(canvasData)
        this.storeName = newStoreName
        this.setCanPopDiagram(true)
    }

    enableCommandStackHandler = (commandStack) => {
        commandStack.addEventListener(e => {
            // console.log('event:', e)
            this.setCanUndo(commandStack.canUndo())
            this.setCanRedo(commandStack.canRedo())

            if (e.isPostChangeEvent()) {
                // console.log('event isPostChangeEvent:', e)
                if (e.action === "POST_EXECUTE") {
                    saveDiagram(this.canvas, this.storeName)
                }
            }
        });
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
        this.setCanPopDiagram(this.diagramStack.length > 0)

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
        this.setCanUndo(canvas.getCommandStack().canUndo())
        this.setCanRedo(canvas.getCommandStack().canRedo())

        canvas.linesToRepaintAfterDragDrop = new draw2d.util.ArrayList()
        canvas.lineIntersections = new draw2d.util.ArrayList()

        this.storeName = canvasData.storeName
        return canvasData.figureId
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
    const user = createDefaultUserNode()
    const system = createDefaultSystemNode()
    const external = createDefaultExternalNode()
    const b = diagramSize / 2
    addFigureToCanvas(canvas, user, { x: b + 200, y: b + 400 })
    addFigureToCanvas(canvas, system, { x: b + 600, y: b + 400 })
    addConnectionToCanvas(canvas, createDefaultConnection(user, 'output0', system, 'input0'))
    addFigureToCanvas(canvas, external, { x: b + 1000, y: b + 400 })
    addConnectionToCanvas(canvas, createDefaultConnection(system, 'output0', external, 'input0'))

    zoomAndMoveShowTotalDiagram(canvas)
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