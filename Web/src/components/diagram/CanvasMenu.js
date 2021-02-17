import React from "react";
import ContextMenu from "../../common/ContextMenu";
import { createDefaultExternalNode, createDefaultNode, createDefaultUserNode } from "./figures";


export default function CanvasMenu({ canvas, isCanvas, onClose, x, y }) {
    if (!isCanvas) {
        return null
    }

    const add = figure => canvas.addFigure(figure, canvas.toCanvasCoordinate(x, y))

    const items = [
        { text: 'Add Node', do: () => add(createDefaultNode()) },
        { text: 'Add User Node', do: () => add(createDefaultUserNode()) },
        { text: 'Add External Node', do: () => add(createDefaultExternalNode()) }
    ]

    return (
        <ContextMenu items={items} onClose={onClose} x={x - 1} y={y - 4} />
    )
}

