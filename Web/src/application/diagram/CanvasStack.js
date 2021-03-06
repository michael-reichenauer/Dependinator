import draw2d from "draw2d";


export default class CanvasStack {
    diagramStack = []

    constructor(canvas) {
        this.canvas = canvas
    }

    isRoot = () => this.diagramStack.length === 0

    getLevel = () => this.diagramStack.length

    pushDiagram() {
        const canvas = this.canvas
        const canvasData = this.getCanvasData(canvas)

        // Store the canvas data so it can be popped later
        this.diagramStack.push(canvasData)

        this.clearCanvas(canvas)

        // new command stack, but reuse command stack event listeners from parent
        canvas.commandStack.eventListeners = canvasData.commandStack.eventListeners
    }


    popDiagram() {
        if (this.diagramStack.length === 0) {
            return
        }
        const canvas = this.canvas

        this.clearCanvas(canvas)

        // pop canvas data and restore canvas
        const canvasData = this.diagramStack.pop()
        this.restoreCanvasData(canvasData, canvas)

    }

    clearCanvas(canvas) {
        // Remove all connections and nodes
        canvas.lines.each((i, e) => e.setCanvas(null))
        canvas.figures.each((i, e) => e.setCanvas(null))
        // Clear all canvas data
        canvas.selection.clear()
        canvas.currentDropTarget = null
        canvas.figures = new draw2d.util.ArrayList()
        canvas.lines = new draw2d.util.ArrayList()
        canvas.commonPorts = new draw2d.util.ArrayList()
        canvas.linesToRepaintAfterDragDrop = new draw2d.util.ArrayList()
        canvas.lineIntersections = new draw2d.util.ArrayList()
        canvas.commandStack = new draw2d.command.CommandStack()
        canvas.mainNodeId = null
        canvas.canvasId = null
    }


    getCanvasData(canvas) {
        const area = canvas.getScrollArea()
        return {
            diagramId: canvas.diagramId,
            canvasId: canvas.canvasId,
            mainNodeId: canvas.mainNodeId,
            zoom: canvas.zoomFactor,
            x: area.scrollLeft(),
            y: area.scrollTop(),
            lines: canvas.lines,
            figures: canvas.figures,
            commonPorts: canvas.commonPorts,
            commandStack: canvas.commandStack,
            linesToRepaintAfterDragDrop: canvas.linesToRepaintAfterDragDrop,
            lineIntersections: canvas.lineIntersections,
        }
    }

    restoreCanvasData(canvasData, canvas) {
        canvas.diagramId = canvasData.diagramId
        canvas.canvasId = canvasData.canvasId
        canvas.mainNodeId = canvasData.mainNodeId

        canvas.setZoom(canvasData.zoom)
        const area = canvas.getScrollArea()
        area.scrollLeft(canvasData.x)
        area.scrollTop(canvasData.y)
        canvas.figures = canvasData.figures
        canvas.lines = canvasData.lines
        canvas.commonPorts = canvasData.commonPorts
        canvas.commandStack = canvasData.commandStack

        // Set canvas first in lines (needed to restore e.g group)
        canvasData.lines.each((i, e) => e.setCanvas(canvas))

        // Set canvas for figures (group need that lines have been set)
        canvasData.figures.each((i, e) => e.setCanvas(canvas))

        // Repaint lines and figures
        canvasData.lines.each((i, e) => e.repaint())
        canvasData.figures.each((i, e) => e.repaint())
    }
}