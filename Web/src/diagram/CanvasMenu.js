import React from "react";
import ContextMenu from "../common/ContextMenu";
import Node from "./figures";


export default function CanvasMenu({ canvas, onClose, x, y }) {
    if (canvas == null) {
        return null
    }

    const add = type => canvas.addNode(type, canvas.fromDocumentToCanvasCoordinate(x, y))

    const items = [
        { text: 'Add Node', do: () => add(Node.nodeType) },
        { text: 'Add User Node', do: () => add(Node.userType) },
        { text: 'Add External Node', do: () => add(Node.externalType) }
    ]

    return (
        <ContextMenu items={items} onClose={onClose} x={x - 1} y={y - 4} />
    )
}

