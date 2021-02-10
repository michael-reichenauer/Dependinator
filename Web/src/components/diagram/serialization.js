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


