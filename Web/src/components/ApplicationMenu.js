import React, { useState } from "react";
import PubSub from 'pubsub-js'
import Menu from "@material-ui/core/Menu";
import MenuItem from "@material-ui/core/MenuItem";
import IconButton from "@material-ui/core/IconButton";
import MenuIcon from "@material-ui/icons/Menu";
import Tooltip from '@material-ui/core/Tooltip';
import makeStyles from "@material-ui/core/styles/makeStyles";
import { Box, Link, Popover, Typography } from "@material-ui/core";


const useMenuStyles = makeStyles((theme) => ({
    menuButton: {
        marginLeft: theme.spacing(2),
    },
}));


export function ApplicationMenu() {
    const classes = useMenuStyles();
    const [menu, setMenu] = useState(null);
    const [anchorEl, setAnchorEl] = React.useState(null);

    const handleAbout = (event) => {
        setMenu(null);
        console.info(`Show About`)
        setAnchorEl(event.currentTarget);
    };


    const handleNewDiagram = (event) => {
        setMenu(null);
        var shouldDelete = confirm('Do you really want to clear the current diagram?') //eslint-disable-line
        if (shouldDelete) {
            PubSub.publish('diagram.NewDiagram')
        }
    };

    const handleExport = (event) => {
        setMenu(null);
        PubSub.publish('diagram.Export')
    }

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
            <Menu
                anchorEl={menu}
                keepMounted
                open={Boolean(menu)}
                onClose={() => setMenu(null)}
                PaperProps={{
                    // style: {
                    //     backgroundColor: "#333333"
                    // },
                }}
            >

                <MenuItem onClick={handleNewDiagram}>New Diagram</MenuItem>
                <MenuItem onClick={handleExport}>Export Diagram as Page (A4)</MenuItem>
                <MenuItem onClick={handleAbout}>About</MenuItem>

            </Menu>
            <Popover
                id={id}
                open={open}
                anchorEl={anchorEl}
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


