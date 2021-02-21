

import "import-jquery";
import "jquery-ui-bundle";
import "jquery-ui-bundle/jquery-ui.css";
import draw2d from "draw2d";
import { WheelZoomPolicy } from "./WheelZoomPolicy"
import { PanReadOnlyPolicy } from "./PanReadOnlyPolicy"
import { PanEditPolicy } from "./PanEditPolicy"
import { ConnectionCreatePolicy } from "./ConnectionCreatePolicy"
import { random } from '../../common/utils'
import {
    createDefaultNode, createDefaultUserNode, createDefaultExternalNode,
    createDefaultSystemNode, zoomAndMoveShowTotalDiagram, getCanvasFiguresRect,
    createDefaultGroupNode, defaultNodeWidth
} from './figures'
import { serializeCanvas, deserializeCanvas, exportCanvas } from './serialization'
import { canvasDivBackground } from "./colors";
import { createDefaultConnection } from "./connections";
import { Tweenable } from "shifty"


const storeDiagramName = 'diagram'


export default class Canvas {
    canvas = null;
    diagramStack = []
    setCanUndo = null
    setCanRedo = null


    constructor(canvasId, setCanUndo, setCanRedo) {
        this.canvas = this.createCanvas(canvasId, setCanUndo, setCanRedo)
        this.setCanUndo = setCanUndo
        this.setCanRedo = setCanRedo
    }


    delete() {
        this.canvas.destroy()
    }


    createCanvas(canvasId, setCanUndo, setCanRedo) {
        const canvas = new draw2d.Canvas(canvasId)
        canvas.setScrollArea("#" + canvasId)
        canvas.setDimension(new draw2d.geo.Rectangle(0, 0, 10000, 10000))
        canvas.regionDragDropConstraint.constRect = new draw2d.geo.Rectangle(0, 0, 10000, 10000)

        restoreDiagram(canvas)

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
        canvas.installEditPolicy(new draw2d.policy.canvas.SnapToGridEditPolicy(10, false))

        canvas.getCommandStack().addEventListener(function (e) {
            // console.log('event:', e)
            setCanUndo(canvas.getCommandStack().canUndo())
            setCanRedo(canvas.getCommandStack().canRedo())

            if (e.isPostChangeEvent()) {
                // console.log('event isPostChangeEvent:', e)
                if (e.action === "POST_EXECUTE") {
                    saveDiagram(canvas)
                }
            }
        });
        return canvas
    }


    showTotalDiagram = () => {
        zoomAndMoveShowTotalDiagram(this.canvas)
    }

    commandUndo = () => {
        this.canvas.getCommandStack().undo();
        saveDiagram(this.canvas)
    }

    commandRedo = () => {
        this.canvas.getCommandStack().redo();
        saveDiagram(this.canvas)
    }

    commandAddNode = () => this.addFigure(createDefaultNode(), this.randomCenterPoint())
    commandAddUserNode = () => this.addFigure(createDefaultUserNode(), this.randomCenterPoint())
    // commandAddExternalNode = () => this.addFigure(createDefaultExternalNode(), this.randomCenterPoint())
    commandAddExternalNode = () => this.addFigure(createDefaultGroupNode(), this.randomCenterPoint())

    addDefaultItem = (x, y, shiftKey, ctrlKey) => this.addFigure(createDefaultNode(), { x: x, y: y })


    commandNewDiagram = () => {
        this.clearDiagram()
        addDefaultNewDiagram(this.canvas)
    }

    commandShowInnerDiagram = (msg, figure) => {
        zoomAndMoveShowInnerDiagram(figure, () => {
            this.pushDiagram()
            const group = createDefaultGroupNode()

            this.canvas.add(group, 5200, 5400)
            zoomAndMoveShowTotalDiagram(this.canvas)
        })
    }

    commandCloseInnerDiagram = () => {
        this.popDiagram()
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

    pushDiagram = () => {
        const canvas = this.canvas
        const canvasData = {
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
        const setCanUndo = this.setCanUndo
        const setCanRedo = this.setCanRedo
        canvas.getCommandStack().addEventListener(function (e) {
            // console.log('event:', e)
            setCanUndo(canvas.getCommandStack().canUndo())
            setCanRedo(canvas.getCommandStack().canRedo())

            if (e.isPostChangeEvent()) {
                // console.log('event isPostChangeEvent:', e)
                if (e.action === "POST_EXECUTE") {
                    //saveDiagram(canvas)
                }
            }
        });

        canvas.linesToRepaintAfterDragDrop = new draw2d.util.ArrayList()
        canvas.lineIntersections = new draw2d.util.ArrayList()

        this.diagramStack.push(canvasData)
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

        canvasData.figures.each(function (i, e) {
            canvas.add(e)
        })

        canvasData.lines.each(function (i, e) {
            canvas.add(e)
        })


        canvas.commonPorts = canvasData.commonPorts
        canvas.commandStack = canvasData.commandStack
        canvas.linesToRepaintAfterDragDrop = new draw2d.util.ArrayList()
        canvas.lineIntersections = new draw2d.util.ArrayList()
    }
}

const zoomAndMoveShowInnerDiagram = (figure, done) => {
    const canvas = figure.getCanvas()
    let tweenable = new Tweenable()

    const targetZoom = 0.2 * figure.width / defaultNodeWidth
    let area = canvas.getScrollArea()


    const fc = { x: figure.x + figure.width / 2, y: figure.y + figure.height / 2 }
    const cc = { x: canvas.getWidth() / 2, y: canvas.getHeight() / 2 }

    tweenable.tween({
        from: { 'zoom': canvas.zoomFactor },
        to: { 'zoom': targetZoom },
        duration: 1000,
        easing: "easeOutSine",
        step: params => {
            canvas.setZoom(params.zoom, false)

            // Scroll figure to center
            const tp = { x: fc.x - cc.x * params.zoom, y: fc.y - cc.y * params.zoom }
            area.scrollLeft((tp.x) / params.zoom)
            area.scrollTop((tp.y) / params.zoom)
        },
        finish: params => {
            canvas.setZoom(targetZoom, false)

            const tp = { x: fc.x - cc.x * targetZoom, y: fc.y - cc.y * targetZoom }
            area.scrollLeft((tp.x) / targetZoom)
            area.scrollTop((tp.y) / targetZoom)
            done()
        }
    })

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


const saveDiagram = (canvas) => {
    // Serialize canvas figures and connections into canvas data object
    const canvasData = serializeCanvas(canvas);

    // Store canvas data in local storage
    const canvasText = JSON.stringify(canvasData)
    localStorage.setItem(storeDiagramName, canvasText)
    // console.log('save', canvasText)
}

const restoreDiagram = (canvas) => {
    // Get canvas data from local storage.
    let canvasText = localStorage.getItem(storeDiagramName)
    //console.log('load', canvasText)

    if (canvasText == null) {
        console.log('no diagram')
        addDefaultNewDiagram(canvas)
        return
    }
    //console.log('saved', canvasText)
    const canvasData = JSON.parse(canvasText)
    if (canvasData == null || canvasData.figures == null || canvasData.figures.lengths === 0) {
        return
    }

    // Deserialize canvas
    deserializeCanvas(canvas, canvasData)
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

