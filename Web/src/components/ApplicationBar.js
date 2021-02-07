import React from "react";
import { Typography, fade, AppBar, Toolbar, IconButton, Tooltip, } from "@material-ui/core";
import makeStyles from "@material-ui/core/styles/makeStyles";
import { ApplicationMenu } from "./ApplicationMenu"
import PersonAddIcon from '@material-ui/icons/PersonAdd';
import LibraryAddOutlinedIcon from '@material-ui/icons/LibraryAddOutlined';
import AddBoxOutlinedIcon from '@material-ui/icons/AddBoxOutlined';

export default function ApplicationBar({ height, commands }) {
    const classes = useAppBarStyles();

    return (
        <AppBar position="static" style={{ height: height }}>
            <Toolbar>
                <Typography className={classes.title} variant="h6" noWrap>Dependinator</Typography>

                <Tooltip title="Add node" ><IconButton onClick={() => commands.addNode()}><AddBoxOutlinedIcon style={{ color: 'white' }} /></IconButton></Tooltip>
                <Tooltip title="Add user node" ><IconButton onClick={() => commands.addUserNode()}><PersonAddIcon style={{ color: 'white' }} /></IconButton></Tooltip>
                <Tooltip title="Add external system node" ><IconButton onClick={() => commands.addExternalNode()}><LibraryAddOutlinedIcon style={{ color: 'white' }} /></IconButton></Tooltip>
                <ApplicationMenu />

            </Toolbar>
        </AppBar >
    )
}


const useAppBarStyles = makeStyles((theme) => ({
    root: {
        flexGrow: 1,
    },
    title: {
        flexGrow: 1,
        display: 'none',
        [theme.breakpoints.up('sm')]: {
            display: 'block',
        },
    },
    search: {
        position: 'relative',
        borderRadius: theme.shape.borderRadius,
        backgroundColor: fade(theme.palette.common.white, 0.15),
        '&:hover': {
            backgroundColor: fade(theme.palette.common.white, 0.25),
        },
        marginLeft: 0,
        width: '100%',
        [theme.breakpoints.up('sm')]: {
            marginLeft: theme.spacing(1),
            width: 'auto',
        },
    },
    searchIcon: {
        padding: theme.spacing(0, 2),
        height: '100%',
        position: 'absolute',
        pointerEvents: 'none',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
    },
    inputRoot: {
        color: 'inherit',
    },
    inputInput: {
        padding: theme.spacing(1, 1, 1, 0),
        // vertical padding + font size from searchIcon
        paddingLeft: `calc(1em + ${theme.spacing(4)}px)`,
        transition: theme.transitions.create('width'),
        width: '100%',
        [theme.breakpoints.up('sm')]: {
            width: '8ch',
            '&:focus': {
                width: '20ch',
            },
        },
    },
}));