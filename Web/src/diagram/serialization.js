import draw2d from "draw2d";
import { serializeFigures, deserializeFigures, getCanvasFiguresRect } from './figures'
import { serializeConnections, deserializeConnections } from './connections'
import { canvasDivBackground } from "./colors";

export const serializeCanvas = (canvas) => {
    const bb = getCanvasFiguresRect(canvas)
    const figures = serializeFigures(canvas);
    const connections = serializeConnections(canvas)

    const canvasData = {
        box: bb,
        figures: figures,
        connections: connections,
        zoom: canvas.getZoom()
    }

    return canvasData
}


export const deserializeCanvas = (canvas, canvasData) => {
    const figures = deserializeFigures(canvasData.figures)
    for (let i = 0; i < figures.length; i++) {
        canvas.add(figures[i])
    }

    const connections = deserializeConnections(canvas, canvasData.connections)
    for (let i = 0; i < connections.length; i++) {
        canvas.add(connections[i])
    }
    // canvas.addAll(deserializeFigures(canvasData.figures))
    // canvas.addAll(deserializeConnections(canvas, canvasData.connections))
}


export const exportCanvas = (canvas, rect, result) => {
    var writer = new draw2d.io.svg.Writer();
    writer.marshal(canvas, (svg) => {
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
         viewBox="${r.x} ${r.y} ${r.w} ${r.h}" style="background-color:${canvasDivBackground}" `

        // Replace svg size with A4 size and view box
        const index = svg.indexOf('xmlns="http://www.w3.org/2000/svg"')
        let res = prefix + svg.substr(index)

        // Remove org view box (if it exists)
        res = res.replace('viewBox="0 0 10000 10000"', '')

        result(res)
    });
}


