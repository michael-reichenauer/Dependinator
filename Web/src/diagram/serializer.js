import draw2d from "draw2d";
import { figures, getCanvasFiguresRect } from './figures'
import { deserializeConnections, serializeConnections } from './connections'
import Colors from "./colors";


export default class Serializer {
    constructor(canvas) {
        this.canvas = canvas
    }


    serialize = () => {
        const canvas = this.canvas
        const bb = getCanvasFiguresRect(canvas)
        const figs = figures.serializeFigures(canvas);
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
        // const figures = deserializeFigures(canvasData.figures) 
        // for (let i = 0; i < figures.length; i++) {
        //     canvas.add(figures[i])
        // }

        // const connections = deserializeConnections(canvas, canvasData.connections)
        // for (let i = 0; i < connections.length; i++) {
        //     canvas.add(connections[i])
        // }
        this.canvas.addAll(figures.deserializeFigures(canvasData.figures))
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
}
