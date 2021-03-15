import React from "react";
import ContextMenu, { Item, NestedItem } from "../common/ContextMenu";
import Colors from "./colors";
import { CommandChangeColor } from "./commandChangeColor";


export default function FigureMenu({ figure, onClose, x, y }) {
    if (figure == null) {
        return null
    }

    const setColor = (figure, colorName) => {
        const command = new CommandChangeColor(figure, colorName);
        figure.getCanvas().getCommandStack().execute(command);
    }

    const colorItems = Colors.nodeColorNames().map((colorName) => {
        return new Item(colorName, () => setColor(figure, colorName))
    })
    const items = [new NestedItem('Set color', colorItems)]

    return (
        <ContextMenu items={items} onClose={onClose} x={x - 1} y={y - 4} />
    )
}


