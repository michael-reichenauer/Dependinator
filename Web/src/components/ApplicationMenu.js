import React, { useState } from "react";
import Menu from "@material-ui/core/Menu";
import MenuItem from "@material-ui/core/MenuItem";
import IconButton from "@material-ui/core/IconButton";
import MenuIcon from "@material-ui/icons/Menu";
import Tooltip from '@material-ui/core/Tooltip';
import makeStyles from "@material-ui/core/styles/makeStyles";
import { Popover, Typography } from "@material-ui/core";


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

    const handleCloseAbout = () => {
        console.info(`Hide About`)
        setAnchorEl(null);
    };

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
                <Typography>About</Typography>
            </Popover>
        </>
    )
}


