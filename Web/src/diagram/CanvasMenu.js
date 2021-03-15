import React from "react";
import ContextMenu, { Item } from "../common/ContextMenu";
import Node from './Node'


export default function CanvasMenu({ canvas, onClose, x, y }) {
    if (canvas == null) {
        return null
    }

    const add = type => canvas.addNode(type, canvas.fromDocumentToCanvasCoordinate(x, y))

    const items = [
        new Item('Add Node', () => add(Node.nodeType)),
        new Item('Add User Node', () => add(Node.userType)),
        new Item('Add External Node', () => add(Node.externalType))
    ]

    return (
        <ContextMenu items={items} onClose={onClose} x={x - 1} y={y - 4} />
    )
}

