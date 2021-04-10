import React from "react";
import { Menu, MenuItem } from "@material-ui/core";
import NestedMenuItem from "material-ui-nested-menu-item";


export const menuItem = (text, action, isEnabled = true, isShow = true) => {
    return new Item(text, action, isEnabled, isShow)
}

export const menuParentItem = (text, items, isEnabled = true, isShow = true) => {
    return new NestedItem(text, items, isEnabled, isShow)
}

class Item {
    constructor(text, action, isEnabled = true, isShow = true) {
        this.text = text
        this.action = action
        this.isEnabled = isEnabled
        this.isShow = isShow
    }
}

class NestedItem {
    items = []
    constructor(text, items, isEnabled = true, isShow = true) {
        this.text = text
        this.items = items
        this.isEnabled = isEnabled
        this.isShow = isShow
    }
}

export function AppMenu({ anchorEl, items, onClose }) {
    if (anchorEl == null || items == null || items.length === 0) {
        return null
    }

    const onClick = (item) => {
        onClose()

        if (!(item instanceof Item)) {
            return
        }
        item?.action()
    }

    return (
        <Menu
            anchorEl={anchorEl}
            open={true}
            onClose={() => onClose()}
            PaperProps={{}}
        >
            {getMenuItems(items, onClick)}
        </Menu>
    )
}


export function ContextMenu({ menu, onClose }) {
    if (menu?.items == null) {
        return null
    }

    const onClick = (item) => {
        if (!(item instanceof Item)) {
            return
        }
        onClose()
        item?.action()
    }


    return (
        <Menu
            open={true}
            onClose={onClose}
            anchorReference="anchorPosition"
            anchorPosition={{ left: menu.x - 2, top: menu.y - 2 }}
        >
            {getMenuItems(menu.items, onClick)}
        </Menu>
    )
}

const getMenuItems = (items, onClick) => {
    return items.map((item, i) => {
        //console.log('try item', item)
        if (!item.isShow) {
            return null
        }
        if (item instanceof Item) {
            return (
                <MenuItem
                    key={`item-${i}`}
                    onClick={() => onClick(item)}
                    disabled={!item.isEnabled}
                    dense>
                    {item.text}
                </MenuItem>
            )
        } else if (item instanceof NestedItem) {
            return (
                <NestedMenuItem
                    key={`item-${i}`}
                    label={item.text}
                    parentMenuOpen={true}
                    disabled={!item.isEnabled}
                    dense
                >
                    {getMenuItems(item.items, onClick)}
                </NestedMenuItem>
            )
        }
        console.warn('Unknown item', item)
        return null

    }).filter(i => i != null)
}
