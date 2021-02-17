import React from "react";
import { Menu, MenuItem } from "@material-ui/core";

export default function ContextMenu({ items, onClose, x, y }) {
    const onClick = (item) => {
        onClose()
        item.do()
    }
    return (
        <Menu
            keepMounted
            open={true}
            onClose={onClose}
            anchorReference="anchorPosition"
            anchorPosition={{ left: x, top: y }}
        >
            {items.map((item, i) => (
                <MenuItem onClick={() => onClick(item)} key={`item-${i}`}>{item.text}</MenuItem>
            ))}

        </Menu>
    )
}

