import draw2d from "draw2d";
import { timing } from "../common/timing";
import { addDefaultInnerDiagram } from "./addDefault";
import Connection from "./Connection";
import Node from "./Node";

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
        const t = timing()
        const innerDiagram = figure.innerDiagram
        if (innerDiagram == null) {
            // Figure has no inner diagram, thus nothing to edit
            return
        }

        // Remember the current outer zoom, which is used when zooming ghr inner diagram
        const outerZoom = this.canvas.zoomFactor

        // Get the view coordinates of the inner diagram image where the inner diagram should
        // positioned after the switch 
        const innerDiagramViewPos = innerDiagram.getDiagramViewCoordinate()

        const connectedNodes = this.getConnectedNodes(figure)
        // Hide the inner diagram image from figure (will be updated when popping)
        figure.hideInnerDiagram()

        // Push current diagram to make room for new diagram 
        this.canvasStack.pushDiagram(figure.getId())
        t.log('pushed diagram')

        // Load inner diagram or a default group node if first time
        if (!this.load(figure.getId())) {
            addDefaultInnerDiagram(this.canvas, figure.getName())
        }
        this.addConnectedNodes(connectedNodes)
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

    sortNodesOnX(nodes) {
        nodes.sort((d1, d2) => d1.node.x < d2.node.x ? -1 : d1.node.x > d2.node.x ? 1 : 0)
    }
    sortNodesOnY(nodes) {
        nodes.sort((d1, d2) => d1.node.y < d2.node.y ? -1 : d1.node.y > d2.node.y ? 1 : 0)
    }

    getConnectedNodes(figure) {
        const left = figure.getPort('input0').getConnections().asArray()
            .map(c => { return { node: c.sourcePort.parent.serialize(), connection: c.serialize() } })
        const top = figure.getPort('input1').getConnections().asArray()
            .map(c => { return { node: c.sourcePort.parent.serialize(), connection: c.serialize() } })
        const right = figure.getPort('output0').getConnections().asArray()
            .map(c => { return { node: c.targetPort.parent.serialize(), connection: c.serialize() } })
        const bottom = figure.getPort('output1').getConnections().asArray()
            .map(c => { return { node: c.targetPort.parent.serialize(), connection: c.serialize() } })

        this.sortNodesOnY(left)
        this.sortNodesOnY(right)
        this.sortNodesOnX(top)
        this.sortNodesOnX(bottom)

        return { left: left, top: top, right: right, bottom: bottom }
    }

    addConnectedNodes(nodes) {
        const group = this.canvas.group
        const marginGroup = 100
        const marginBetween = 50

        let y = group.y
        let x = group.x - Node.defaultWidth - marginGroup
        nodes.left.forEach(data => {
            const node = this.addNode(data, x, y)
            this.addConnection(data, node, group)
            y = y + Node.defaultHeight + marginBetween
        });

        y = group.y - Node.defaultHeight - marginGroup
        x = group.x
        nodes.top.forEach(data => {
            const node = this.addNode(data, x, y)
            this.addConnection(data, node, group)
            x = x + Node.defaultWidth + marginBetween
        });

        y = group.y
        x = group.x + group.width + marginGroup
        nodes.right.forEach(data => {
            const node = this.addNode(data, x, y)
            this.addConnection(data, group, node)
            y = y + Node.defaultHeight + marginBetween
        });

        y = group.y + group.height + marginGroup
        x = group.x
        nodes.bottom.forEach(data => {
            const node = this.addNode(data, x, y)
            this.addConnection(data, group, node)
            x = x + Node.defaultWidth + marginBetween
        });
    }

    addConnection(data, src, trg) {
        const description = data.connection.deserialize
        const srcPort = data.connection.srcPort
        const trgPort = data.connection.trgPort
        const connection = new Connection(description, src, srcPort, trg, trgPort)
        connection.setDeleteable(false)

        this.canvas.add(connection)
    }

    addNode(data, x, y) {
        const node = Node.deserialize(data.node)
        node.attr({ width: Node.defaultWidth, height: Node.defaultHeight, alpha: 0.8, resizeable: false })
        node.setDeleteable(false)
        this.canvas.add(node, x, y)
        return node
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

        t.log()
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