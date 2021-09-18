import draw2d from "draw2d";
import Connection from './Connection'
import Colors from "./Colors";
import Group from "./Group";
import Node from "./Node";
import NodeGroup from "./NodeGroup";



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
        return canvasData
    }


    deserialize(canvasData) {
        this.canvas.diagramId = canvasData.diagramId
        this.canvas.diagramName = canvasData.diagramName
        this.canvas.canvasId = canvasData.canvasId
        this.canvas.mainNodeId = canvasData.mainNodeId
        this.canvas.addAll(this.deserializeFigures(canvasData.figures))
        this.canvas.addAll(this.deserializeConnections(canvasData.connections))
    }



    export(rect, resultHandler) {
        const a4margin = -70
        const a4Width = 793.7007874 - a4margin
        const a4Height = 1122.519685 - a4margin

        var writer = new draw2d.io.svg.Writer();
        writer.marshal(this.canvas, (svg) => {
            // console.log('svg org:', svg)

            // Show diagram with some margin
            const margin = 0
            const r = {
                x: rect.x - margin,
                y: rect.y - margin,
                w: rect.w + margin * 2,
                h: rect.h + margin * 2
            }

            if (r.w < a4Width && r.h < a4Height) {
                const xd = a4Width - r.w
                const yd = a4Height - r.h

                r.w = r.w + xd
                r.x = r.x - xd / 2
                r.h = r.h + yd
                r.y = r.y - yd / 2
            }


            // Export size (A4) and view box
            const prefix = `<svg width="210mm" height="277mm" version="1.1 style="page-break-after: always;"
             viewBox="${r.x} ${r.y} ${r.w} ${r.h}" style="background-color:${Colors.canvasDivBackground}" `

            // Replace svg size with A4 size and view box
            const index = svg.indexOf('xmlns="http://www.w3.org/2000/svg"')
            let res = prefix + svg.substr(index)

            // Remove org view box (if it exists)
            res = res.replace('viewBox="0 0 10000 10000"', '')

            resultHandler(res)
        });
    }


    serializeFigures = () => {
        return this.canvas.getFigures().asArray().map((figure) => figure.serialize());
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
