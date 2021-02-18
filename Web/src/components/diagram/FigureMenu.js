import React from "react";
import ContextMenu from "../../common/ContextMenu";
import { nodeColorNames } from "./colors";
import { CommandChangeColor } from "./commandChangeColor";


export default function FigureMenu({ figure, onClose, x, y }) {
    if (figure == null) {
        return null
    }

    const setColor = (figure, colorName) => {
        const command = new CommandChangeColor(figure, colorName);
        figure.getCanvas().getCommandStack().execute(command);
    }

    const items = nodeColorNames().map((color) => {
        return { text: color, do: () => setColor(figure, color) }
    })

    return (
        <ContextMenu items={items} onClose={onClose} x={x - 1} y={y - 4} />
    )
}


