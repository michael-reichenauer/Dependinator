import draw2d from "draw2d";

import { deserializeConnections, serializeConnections } from './connections'
import Colors from "./colors";
import { getCanvasFiguresRect } from "./Canvas";
import Group from "./Group";
import Node from "./Node";


export default class Serializer {
    constructor(canvas) {
        this.canvas = canvas
    }


    serialize = () => {
        const canvas = this.canvas
        const bb = getCanvasFiguresRect(canvas)
        const figs = this.serializeFigures();
        const conns = serializeConnections(canvas)

        const canvasData = {
            box: bb,
            figures: figs,
            connections: conns,
            zoom: canvas.getZoom()
        }

        return canvasData
    }


    deserialize = (canvasData) => {
        this.canvas.addAll(this.deserializeFigures(canvasData.figures))
        this.canvas.addAll(deserializeConnections(this.canvas, canvasData.connections))
    }


    export = (rect, result) => {
        var writer = new draw2d.io.svg.Writer();
        writer.marshal(this.canvas, (svg) => {
            console.log('svg org:', svg)

            // Show diagram with some margin
            const margin = 25
            const r = {
                x: rect.x - margin,
                y: rect.y - margin,
                w: rect.w + margin * 2,
                h: rect.h + margin * 2
            }

            // Export size (A4) and view box
            const prefix = `<svg width="210mm" height="297mm" version="1.1"
             viewBox="${r.x} ${r.y} ${r.w} ${r.h}" style="background-color:${Colors.canvasDivBackground}" `

            // Replace svg size with A4 size and view box
            const index = svg.indexOf('xmlns="http://www.w3.org/2000/svg"')
            let res = prefix + svg.substr(index)

            // Remove org view box (if it exists)
            res = res.replace('viewBox="0 0 10000 10000"', '')

            result(res)
        });
    }


    serializeFigures = () => {
        return this.canvas.getFigures().asArray().map((f) => f.serialize());
    }

    deserializeFigures = (figures) => {
        return figures.map(f => this.deserializeFigure(f)).filter(f => f != null)
    }

    deserializeFigure = (f) => {
        let figure
        if (f.type === Group.groupType) {
            figure = Group.deserialize(f)
        } else {
            figure = Node.deserialize(f)
        }

        figure.x = f.x
        figure.y = f.y
        return figure
    }
}
