import draw2d from "draw2d";
import { serializeFigures, deserializeFigures } from './figures'
import { serializeConnections, deserializeConnections } from './connections'
import { canvasDivBackground } from "./colors";

export const serializeCanvas = (canvas) => {
    const figures = serializeFigures(canvas);
    const connections = serializeConnections(canvas)

    const canvasData = {
        figures: figures,
        connections: connections,
        zoom: canvas.getZoom()
    }

    return canvasData
}


export const deserializeCanvas = (canvas, canvasData) => {
    const figures = deserializeFigures(canvasData.figures)
    for (let i = 0; i < figures.length; i++) {
        const f = figures[i]
        if (f != null) {
            canvas.add(f)
        }
    }

    const connections = deserializeConnections(canvas, canvasData.connections)
    for (let i = 0; i < connections.length; i++) {
        const c = connections[i]
        if (c != null) {
            canvas.add(c)
        }
    }
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

