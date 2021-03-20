import draw2d from "draw2d";
import { timing } from "../common/timing";
import { addDefaultInnerDiagram } from "./addDefault";

export class CanvasInner {
    canvas = null
    canvasStack = null
    store = null
    serializer = null

    constructor(canvas, canvasStack, store, serializer) {
        this.canvas = canvas
        this.canvasStack = canvasStack
        this.store = store
        this.serializer = serializer
    }

    editInnerDiagram = (figure) => {
        const innerDiagram = figure.innerDiagram
        if (innerDiagram == null) {
            // Figure has no inner diagram, thus nothing to edit
            return
        }

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
        //this.pushDiagram(figure.getId())
        this.canvasStack.pushDiagram()
        this.canvas.name = figure.getId()

        t.log('pushed diagram')

        // Load inner diagram or a default group node if first time
        if (!this.load(figure.getId())) {
            addDefaultInnerDiagram(this.canvas, figure.getName())
        }
        t.log('loaded diagram')

        // Zoom inner diagram to correspond to inner diagram image size
        this.canvas.setZoom(outerZoom / innerDiagram.innerZoom)

        // Scroll inner diagram to correspond to where the inner diagram image was
        const innerDiagramRect = this.getInnerDiagramRect()
        const left = innerDiagramRect.x - innerDiagramViewPos.left * this.canvas.zoomFactor
        const top = innerDiagramRect.y - innerDiagramViewPos.top * this.canvas.zoomFactor
        this.setScrollInCanvasCoordinate(left, top)

        t.log()
    }

    popFromInnerDiagram = () => {
        const t = timing()

        // Get the inner diagram zoom to use when zooming outer diagram
        const postInnerZoom = this.canvas.zoomFactor

        // Get inner diagram view position to scroll the outer diagram to same position
        const innerDiagramRect = this.getInnerDiagramRect()
        const innerDiagramViewPos = this.fromCanvasToViewCoordinate(innerDiagramRect.x, innerDiagramRect.y)

        // Show outer diagram (closing the inner diagram)
        const figureId = this.canvas.name
        this.canvasStack.popDiagram()

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
    }


    fromCanvasToViewCoordinate = (x, y) => {
        return new draw2d.geo.Point(
            ((x * (1 / this.canvas.zoomFactor)) - this.canvas.getScrollLeft()),
            ((y * (1 / this.canvas.zoomFactor)) - this.canvas.getScrollTop()))
    }

    setScrollInCanvasCoordinate = (left, top) => {
        const area = this.canvas.getScrollArea()
        area.scrollLeft(left / this.canvas.zoomFactor)
        area.scrollTop(top / this.canvas.zoomFactor)
    }


    getInnerDiagramRect() {
        const g = this.canvas.group
        return { x: g.x, y: g.y, w: g.width, h: g.heigh }
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
}