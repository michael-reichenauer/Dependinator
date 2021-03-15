import React from "react";
import { Menu, MenuItem } from "@material-ui/core";
import NestedMenuItem from "material-ui-nested-menu-item";


export class Item {
    constructor(text, action) {
        this.text = text
        this.action = action
    }
}

export class NestedItem {
    items = []
    constructor(text, items) {
        this.text = text
        this.items = items
    }
}

export default function ContextMenu({ items, onClose, x, y }) {
    const onClick = (item) => {
        onClose()

        if (!item instanceof Item) {
            return
        }
        item.action()
    }

    return (
        <Menu
            keepMounted
            open={true}
            onClose={onClose}
            anchorReference="anchorPosition"
            anchorPosition={{ left: x, top: y }}
        >
            {getMenuItems(items, onClick)}
        </Menu>
    )
}

const getMenuItems = (items, onClick) => {
    return items.map((item, i) => {
        //console.log('try item', item)
        if (item instanceof Item) {
            return (
                <MenuItem onClick={() => onClick(item)} key={`item-${i}`}>{item.text}</MenuItem>
            )
        } else if (item instanceof NestedItem) {

            return (
                <NestedMenuItem
                    label={item.text}
                    parentMenuOpen={true}
                    onClick={() => onClick(null)}
                    key={`item-${i}`}
                >
                    {getMenuItems(item.items, onClick)}
                </NestedMenuItem>
            )
        }
        console.warn('Unknown item', item)
        return null

    })
}
