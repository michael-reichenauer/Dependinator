

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
    createDefaultSystemNode, getCanvasFiguresRect, createDefaultGroupNode, getFigureName
} from './figures'
import { serializeCanvas, deserializeCanvas, exportCanvas } from './serialization'
import { canvasDivBackground } from "./colors";
import { createDefaultConnection } from "./connections";
import { Tweenable } from "shifty"
import { moveAndZoomToShowInnerDiagram } from "./innerDiagram";


const defaultStoreDiagramName = 'diagram'


export default class Canvas {
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
        const canvas = new draw2d.Canvas(canvasId)
        canvas.setScrollArea("#" + canvasId)
        canvas.setDimension(new draw2d.geo.Rectangle(0, 0, 10000, 10000))
        canvas.regionDragDropConstraint.constRect = new draw2d.geo.Rectangle(0, 0, 10000, 10000)

        const area = canvas.getScrollArea()
        area.scrollLeft(5000)
        area.scrollTop(5000)

        if (!restoreDiagram(canvas, this.storeName)) {
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
        canvas.installEditPolicy(new draw2d.policy.canvas.SnapToGridEditPolicy(10, false))


        canvas.getCommandStack().addEventListener(e => {
            // console.log('event:', e)
            this.setCanUndo(canvas.getCommandStack().canUndo())
            this.setCanRedo(canvas.getCommandStack().canRedo())

            if (e.isPostChangeEvent()) {
                // console.log('event isPostChangeEvent:', e)
                if (e.action === "POST_EXECUTE") {
                    saveDiagram(canvas, this.storeName)
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
        saveDiagram(this.canvas, this.storName)
    }

    commandRedo = () => {
        this.canvas.getCommandStack().redo();
        saveDiagram(this.canvas, this.storName)
    }

    commandAddNode = () => this.addFigure(createDefaultNode(), this.randomCenterPoint())
    commandAddUserNode = () => this.addFigure(createDefaultUserNode(), this.randomCenterPoint())
    commandAddExternalNode = () => this.addFigure(createDefaultExternalNode(), this.randomCenterPoint())
    //commandAddExternalNode = () => this.addFigure(createDefaultGroupNode(), this.randomCenterPoint())

    addDefaultItem = (x, y, shiftKey, ctrlKey) => this.addFigure(createDefaultNode(), { x: x, y: y })


    commandNewDiagram = () => {
        this.clearDiagram()
        localStorage.clear()
        console.log('Cleared all stored')
        addDefaultNewDiagram(this.canvas)
    }

    commandShowInnerDiagram = (msg, figure) => {
        moveAndZoomToShowInnerDiagram(figure, () => {
            this.pushDiagram(figure.getId())
            if (!restoreDiagram(this.canvas, figure.getId())) {
                const group = createDefaultGroupNode(getFigureName(figure))
                this.canvas.add(group, 5200, 5400)
            }

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

    pushDiagram = (newStoreName) => {
        const canvas = this.canvas
        const canvasData = {
            storeName: this.storeName,
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

        this.setCanUndo(canvas.getCommandStack().canUndo())
        this.setCanRedo(canvas.getCommandStack().canRedo())

        canvas.getCommandStack().addEventListener(e => {
            // console.log('event:', e)
            this.setCanUndo(canvas.getCommandStack().canUndo())
            this.setCanRedo(canvas.getCommandStack().canRedo())

            if (e.isPostChangeEvent()) {
                // console.log('event isPostChangeEvent:', e)
                if (e.action === "POST_EXECUTE") {
                    saveDiagram(canvas, this.storeName)
                }
            }
        });

        canvas.linesToRepaintAfterDragDrop = new draw2d.util.ArrayList()
        canvas.lineIntersections = new draw2d.util.ArrayList()

        this.diagramStack.push(canvasData)
        this.storeName = newStoreName
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
        this.setCanUndo(canvas.getCommandStack().canUndo())
        this.setCanRedo(canvas.getCommandStack().canRedo())

        canvas.linesToRepaintAfterDragDrop = new draw2d.util.ArrayList()
        canvas.lineIntersections = new draw2d.util.ArrayList()

        this.storeName = canvasData.storeName
    }

    //     canvas.addAll = function (figures) {
    //         for(var i = 0; i < figures.length; i++) {
    //            var figure = figures[i].figure, x = figures[i].x, y = figures[i].y;

    //            if (figure.getCanvas() === this) { return; }

    //            if (figure instanceof draw2d.shape.basic.Line) {
    //                this.lines.add(figure);
    //            } else {
    //                this.figures.add(figure);
    //                if (typeof y !== "undefined") {
    //                    figure.setPosition(x, y);
    //                } else if (typeof x !== "undefined") {
    //                    figure.setPosition(x);
    //                }
    //            }

    //            figure.setCanvas(this);

    //            // to avoid drag&drop outside of this canvas
    //            figure.installEditPolicy(this.regionDragDropConstraint);

    //            // important inital call
    //            figure.getShapeElement();

    //            // fire the figure:add event before the "move" event and after the figure.repaint() call!
    //            //   - the move event can only be fired if the figure part of the canvas.
    //            //     and in this case the notification event should be fired to the listener before
    //            this.fireEvent("figure:add", {figure: figure, canvas: this});

    //            // fire the event that the figure is part of the canvas
    //            figure.fireEvent("added", {figure: figure, canvas: this});

    //            // ...now we can fire the initial move event
    //            figure.fireEvent("move", {figure: figure, dx: 0, dy: 0});

    //            if (figure instanceof draw2d.shape.basic.PolyLine) {
    //                this.calculateConnectionIntersection();
    //            }
    //        }
    //        console.debug("Added all figures", performance.now());

    //        console.debug("Repainting figures", performance.now());
    //        this.figures.each(function(i, fig){
    //            fig.repaint();
    //        });
    //        console.debug("Repainted figures", performance.now());

    //        console.debug("Repainting lines", performance.now());
    //        this.lines.each(function (i, line) {
    //            line.svgPathString = null;
    //            line.repaint();
    //        });
    //        console.debug("Repainted lines", performance.now());
    //        return this;
    //    };
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


const saveDiagram = (canvas, storeName) => {
    // Serialize canvas figures and connections into canvas data object
    const canvasData = serializeCanvas(canvas);

    // Store canvas data in local storage
    const canvasText = JSON.stringify(canvasData)
    localStorage.setItem(storeName, canvasText)
    console.log('saved', storeName)
}

const restoreDiagram = (canvas, storeName) => {
    // Get canvas data from local storage.
    let canvasText = localStorage.getItem(storeName)


    if (canvasText == null) {
        console.log('no stored diagram for', storeName)
        return false
    }
    //console.log('saved', canvasText)
    const canvasData = JSON.parse(canvasText)
    if (canvasData == null || canvasData.figures == null || canvasData.figures.lengths === 0) {
        console.log('no diagram could be parsed (or no figures) for', storeName)
        return false
    }

    // Deserialize canvas
    console.log('loaded', storeName)
    deserializeCanvas(canvas, canvasData)
    return true
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
    if (!canvas.selection.all.isEmpty()) {
        // Deselect items, since zooming with selected figures is slow
        canvas.selection.getAll().each((i, f) => f.unselect())
        canvas.selection.clear()
    }

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
    if (!canvas.selection.all.isEmpty()) {
        // Deselect items, since zooming with selected figures is slow
        canvas.selection.getAll().each((i, f) => f.unselect())
        canvas.selection.clear()
    }

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