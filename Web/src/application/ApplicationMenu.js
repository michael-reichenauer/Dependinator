import React, { useState, useEffect } from "react";
import PubSub from 'pubsub-js'
import IconButton from "@material-ui/core/IconButton";
import MenuIcon from "@material-ui/icons/Menu";
import Tooltip from '@material-ui/core/Tooltip';
import makeStyles from "@material-ui/core/styles/makeStyles";
import { Box, Link, Popover, Typography } from "@material-ui/core";
import { AppMenu, menuItem, menuParentItem } from "../common/Menus";
import { store } from "./diagram/Store";
import Printer from "../common/Printer";


const useMenuStyles = makeStyles((theme) => ({
    menuButton: {

    },
}));

const asMenuItems = (diagrams, lastUsedDiagramId) => {
    return diagrams.filter(d => d.id != lastUsedDiagramId)
        .map(d => menuItem(d.name, () => PubSub.publish('canvas.OpenDiagram', d.id)))
}

export function ApplicationMenu() {
    const classes = useMenuStyles();
    const [menu, setMenu] = useState(null);
    const [anchorEl, setAnchorEl] = useState(null);

    useEffect(() => {
        const handler = Printer.registerPrintKey(() => PubSub.publish('canvas.Print'))
        return () => Printer.deregisterPrintKey(handler)

    })

    const deleteDiagram = () => {
        var shouldDelete = confirm('Do you really want to delete the current diagram?') //eslint-disable-line
        if (shouldDelete) {
            PubSub.publish('canvas.DeleteDiagram')
        }
    };

    const diagrams = menu == null ? [] : asMenuItems(store.getDiagrams(), store.getLastUsedDiagramId())

    const menuItems = [
        menuItem('New Diagram', () => PubSub.publish('canvas.NewDiagram')),
        menuParentItem('Open Recent', diagrams, diagrams.length > 0),
        menuItem('Open file ...', () => PubSub.publish('canvas.OpenFile')),
        menuItem('Save to file', () => PubSub.publish('canvas.SaveDiagramToFile')),
        menuItem('Save/Archive all to file', () => PubSub.publish('canvas.ArchiveToFile')),
        menuItem('Print', () => PubSub.publish('canvas.Print')),
        menuItem('Delete', deleteDiagram),
        menuItem('Reload web page', () => window.location.reload(true)),
        menuItem('About', () => setAnchorEl(true)),
    ]

    const handleCloseAbout = () => { setAnchorEl(null); };

    const open = Boolean(anchorEl);
    const id = open ? 'simple-popover' : undefined;

    return (
        <>
            <Tooltip title="Customize and control">
                <IconButton
                    edge="start"
                    className={classes.menuButton}
                    color="inherit"
                    onClick={e => setMenu(e.currentTarget)}
                >
                    <MenuIcon />
                </IconButton>
            </Tooltip>

            <AppMenu anchorEl={menu} items={menuItems} onClose={setMenu} />

            <Popover
                id={id}
                open={open}
                onClose={handleCloseAbout}
                anchorReference="anchorPosition"
                anchorPosition={{ top: 200, left: 400 }}
                anchorOrigin={{
                    vertical: 'center',
                    horizontal: 'center',
                }}
                transformOrigin={{
                    vertical: 'center',
                    horizontal: 'center',
                }}
            >
                <Box style={{ width: 400, height: 200, padding: 20 }}>
                    <Typography variant="h5">Dependinator</Typography>
                    <Typography >
                        Early preview of a tool for visualizing software architecture inspired by map tools for
                        navigation and the "<Link href="https://c4model.com" target="_blank">C4 Model</Link>"
                        by Simon Brown.
                    </Typography>
                </Box>
            </Popover>
        </>
    )
}


