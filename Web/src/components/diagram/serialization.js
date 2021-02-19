import draw2d from "draw2d";
import { serializeFigures, deserializeFigures } from './figures'
import { serializeConnections, deserializeConnections } from './connections'

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
        canvas.add(figures[i])
    }

    const connections = deserializeConnections(canvas, canvasData.connections)
    for (let i = 0; i < connections.length; i++) {
        canvas.add(connections[i])
    }
}




export const exportCanvas = (canvas, rect, result) => {
    var writer = new draw2d.io.svg.Writer();
    writer.marshal(canvas, (svg) => {
        // Export size (A4)
        const prefix = `<svg width="210mm" height="297mm" style="border:1px solid silver" version="1.1" `

        // Replace svg size with A4 size
        const index = svg.indexOf('xmlns="http://www.w3.org/2000/svg"')
        let res = prefix + svg.substr(index)

        // Show diagram with some margin
        const margin = 25
        const r = {
            x: rect.x - margin,
            y: rect.y - margin,
            w: rect.w + margin * 2,
            h: rect.h + margin * 2
        }
        res = res.replace('viewBox="0 0 10000 10000"', `viewBox="${r.x} ${r.y} ${r.w} ${r.h}"`)

        result(res)
    });
}


