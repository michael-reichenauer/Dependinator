

import "import-jquery";
import "jquery-ui-bundle";
import "jquery-ui-bundle/jquery-ui.css";
import draw2d from "draw2d";
import { WheelZoomPolicy } from "./WheelZoomPolicy"
import { PanReadOnlyPolicy } from "./PanReadOnlyPolicy"
import { PanEditPolicy } from "./PanEditPolicy"
import { ConnectionCreatePolicy } from "./ConnectionCreatePolicy"
import { random } from '../common/utils'
import {
    createDefaultNode, createDefaultUserNode, createDefaultExternalNode,
    createDefaultSystemNode, getCanvasFiguresRect, createDefaultGroupNode, getFigureName, createInnerNode
} from './figures'
import { exportCanvas } from './serialization'
import { canvasDivBackground } from "./colors";
import { createDefaultConnection } from "./connections";
import { Tweenable } from "shifty"
import { moveAndZoomToShowInnerDiagram } from "./innerDiagram";
import { clearStoredDiagram, loadDiagram, saveDiagram } from "./store";
import { timing } from "../common/timing";


const defaultStoreDiagramName = 'diagram'


export default class Canvas {
    canvasId = null
    canvas = null;
    diagramStack = []
    storeName = defaultStoreDiagramName
    setCanUndo = null
    setCanRedo = null


    constructor(canvasId, setCanUndo, setCanRedo) {
        this.setCanUndo = setCanUndo
        this.setCanRedo = setCanRedo
        this.canvas = this.createCanvas(canvasId)
    }


    delete() {
        this.canvas.destroy()
    }


    createCanvas(canvasId) {
        this.canvasId = canvasId
        const canvas = new draw2d.Canvas(canvasId)
        canvas.setScrollArea("#" + canvasId)
        canvas.setDimension(new draw2d.geo.Rectangle(0, 0, 10000, 10000))
        canvas.regionDragDropConstraint.constRect = new draw2d.geo.Rectangle(0, 0, 10000, 10000)

        const area = canvas.getScrollArea()
        area.scrollLeft(5000)
        area.scrollTop(5000)

        if (!loadDiagram(canvas, this.storeName)) {
            addDefaultNewDiagram(canvas)
        }

        // Pan policy readonly/edit
        canvas.panPolicyCurrent = new PanReadOnlyPolicy(this.togglePanPolicy, this.addDefaultItem)
        canvas.panPolicyOther = new PanEditPolicy(this.togglePanPolicy, this.addDefaultItem)
        canvas.installEditPolicy(canvas.panPolicyCurrent)

        canvas.installEditPolicy(new WheelZoomPolicy());
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

    addDefaultItem = (x, y, shiftKey, ctrlKey) => this.addFigure(createDefaultNode(), { x: x, y: y })


    commandNewDiagram = () => {
        this.clearDiagram()
        clearStoredDiagram()
        console.log('Cleared all stored')
        addDefaultNewDiagram(this.canvas)
    }

    commandShowInnerDiagram = (msg, figure) => {
        const t = timing()
        this.unselectAll()

        const innerNode = createInnerNode(figure)
        t.log('created node')

        this.canvas.add(innerNode, figure.x + 2, figure.y + 2)
        //figure.setVisible(false)
        t.log('added node')

        moveAndZoomToShowInnerDiagram(innerNode, () => {
            console.log('figure', figure)
            const t2 = timing('innerDiagram')
            figure.setVisible(true)

            this.pushDiagram(figure.getId())
            t2.log('Pushed')

            if (!loadDiagram(this.canvas, figure.getId())) {
                const group = createDefaultGroupNode(getFigureName(figure))
                const width = this.canvas.getWidth()
                const x = 5000 + (width - 1000) / 2
                this.canvas.add(group, x, 5250)
            }
            t2.log('showed')
        })
    }

    commandCloseInnerDiagram = () => {
        console.time('show Outer')
        this.popDiagram()
        console.timeEnd('show Outer')
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

    togglePanPolicy = (figure) => {
        let current = this.canvas.panPolicyCurrent
        this.canvas.uninstallEditPolicy(this.canvas.panPolicyCurrent)

        this.canvas.panPolicyCurrent = this.canvas.panPolicyOther
        this.canvas.panPolicyOther = current
        this.canvas.installEditPolicy(this.canvas.panPolicyCurrent)

        if (figure != null) {
            this.canvas.setCurrentSelection(figure)
        }
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

        canvas.setZoom(1)
        area.scrollLeft(5000)
        area.scrollTop(5000)

        this.diagramStack.push(canvasData)
        this.storeName = newStoreName
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
    addFigureToCanvas(canvas, user, { x: 5200, y: 5400 })
    addFigureToCanvas(canvas, system, { x: 5600, y: 5400 })
    addConnectionToCanvas(canvas, createDefaultConnection(user, 'output0', system, 'input0'))
    addFigureToCanvas(canvas, external, { x: 6000, y: 5400 })
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