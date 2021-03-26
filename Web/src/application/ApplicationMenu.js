import React, { useState } from "react";
import PubSub from 'pubsub-js'
import IconButton from "@material-ui/core/IconButton";
import MenuIcon from "@material-ui/icons/Menu";
import Tooltip from '@material-ui/core/Tooltip';
import makeStyles from "@material-ui/core/styles/makeStyles";
import { Box, Link, Popover, Typography } from "@material-ui/core";
import { AppMenu, Item, NestedItem } from "../common/ContextMenu";
import { store } from "./diagram/Store";


const useMenuStyles = makeStyles((theme) => ({
    menuButton: {
        marginLeft: theme.spacing(2),
    },
}));

const asItems = (diagrams) => {
    return diagrams.map(d => {
        return new Item(d.name, () => PubSub.publish('canvas.OpenDiagram', d.id))
    })
}

export function ApplicationMenu() {
    const classes = useMenuStyles();
    const [menu, setMenu] = useState(null);
    const [anchorEl, setAnchorEl] = useState(null);

    const deleteDiagram = () => {
        var shouldDelete = confirm('Do you really want to delete the current diagram?') //eslint-disable-line
        if (shouldDelete) {
            PubSub.publish('canvas.DeleteDiagram')
        }
    };


    const diagrams = menu == null ? [] : asItems(store.getDiagrams())
    const menuItems = [
        new Item('New Diagram', () => PubSub.publish('canvas.NewDiagram')),
        new NestedItem('Open Diagram', diagrams),
        new Item('Export Diagram as Page (A4)', () => PubSub.publish('diagram.Export')),
        new Item('Delete current diagram', deleteDiagram),
        new Item('Export Diagram as Page (A4)', () => PubSub.publish('diagram.Export')),
        new Item('About', () => setAnchorEl(true)),
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


