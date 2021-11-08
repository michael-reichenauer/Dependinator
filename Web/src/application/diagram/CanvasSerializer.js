import draw2d from "draw2d";
import Connection from './Connection'
import Colors from "./Colors";
import Group from "./Group";
import Node from "./Node";
import NodeGroup from "./NodeGroup";
import NodeNumber from "./NodeNumber";



export default class CanvasSerializer {
    constructor(canvas) {
        this.canvas = canvas
    }

    serialize() {
        // If canvas is a group, mark all nodes within the group as group to be included in data
        const node = this.canvas.getFigure(this.canvas.mainNodeId)
        if (node instanceof Group) {
            node.getAboardFigures(true).each((i, f) => f.group = node)
        }

        const canvasData = {
            diagramId: this.canvas.diagramId,
            diagramName: this.canvas.diagramName,
            canvasId: this.canvas.canvasId,
            mainNodeId: this.canvas.mainNodeId,
            box: this.canvas.getFiguresRect(),
            figures: this.serializeFigures(),
            connections: this.serializeConnections(),
            zoom: this.canvas.getZoom()
        }

        // Unmark all nodes 
        this.canvas.getFigures().each((i, f) => f.group = null)
        // console.log('data', canvasData)
        return canvasData
    }


    deserialize(canvasData) {
        // console.log('data', canvasData)
        this.canvas.diagramId = canvasData.diagramId
        this.canvas.diagramName = canvasData.diagramName
        this.canvas.canvasId = canvasData.canvasId
        this.canvas.mainNodeId = canvasData.mainNodeId
        // const figures = this.deserializeFigures(canvasData.figures)
        // figures.forEach(figure => this.canvas.add(figure));

        // const connection = this.deserializeConnections(canvasData.connections)
        // connection.forEach(connection => this.canvas.add(connection));
        this.canvas.addAll(this.deserializeFigures(canvasData.figures))

        this.canvas.addAll(this.deserializeConnections(canvasData.connections))
    }


    export(rect, width, height, margin, resultHandler) {
        var writer = new draw2d.io.svg.Writer();
        writer.marshal(this.canvas, (svg) => {
            // console.log('svg org:', svg)

            const areaWidth = width + margin * 2
            const areaHeight = height + margin * 2
            if (rect.w < areaWidth && rect.h < areaHeight) {
                // Image smaller than area; Center image and resize to normal size
                const xd = areaWidth - rect.w
                const yd = areaHeight - rect.h

                rect.x = rect.x - xd / 2
                rect.y = rect.y - yd / 2
                rect.w = rect.w + xd
                rect.h = rect.h + yd
            } else {
                // Image larger than area; Resize and add margin for image larger than area
                rect.x = rect.x - margin
                rect.y = rect.y - margin
                rect.w = rect.w + margin * 2
                rect.h = rect.h + margin * 2
            }

            // Export size (A4) and view box
            const prefix = `<svg width="${width}" height="${height}" version="1.1" viewBox="${rect.x} ${rect.y} ${rect.w} ${rect.h}" `

            // Replace svg size with A4 size and view box
            const index = svg.indexOf('xmlns="http://www.w3.org/2000/svg"')
            let res = prefix + svg.substr(index)

            // Adjust style for color and page brake
            res = res.replace('style="', `style="background-color:${Colors.canvasDivBackground};`)
            res = res.replace('style="', `style="page-break-after: always;`)

            // Remove org view box (if it exists)
            res = res.replace('viewBox="0 0 10000 10000"', '')

            resultHandler(res)
        });
    }


    serializeFigures = () => {
        const figures = this.canvas.getFigures().clone()
        figures.sort(function (a, b) {
            // return 1  if a before b
            // return -1 if b before a
            return a.getZOrder() > b.getZOrder() ? 1 : -1;
        });

        return figures.asArray().map((figure) => figure.serialize());
    }

    deserializeFigures = (figures) => {
        return figures.map(f => this.deserializeFigure(f)).filter(f => f != null)
    }

    deserializeFigure = (f) => {
        let figure
        if (f.type === Group.groupType) {
            figure = Group.deserialize(f)
        } else if (f.type === NodeGroup.nodeType) {
            figure = NodeGroup.deserialize(f)
        } else if (f.type === NodeNumber.nodeType) {
            figure = NodeNumber.deserialize(f)
        } else {
            figure = Node.deserialize(f)
        }

        figure.x = f.x
        figure.y = f.y
        return figure
    }

    serializeConnections() {
        return this.canvas.getLines().asArray().map((connection) => connection.serialize())
    }

    deserializeConnections(connections) {
        return connections.map(c => Connection.deserialize(this.canvas, c)).filter(c => c != null)
    }
}
