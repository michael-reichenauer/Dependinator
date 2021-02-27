import React from "react";
import PubSub from 'pubsub-js'
import { Typography, fade, AppBar, Toolbar, IconButton, Tooltip, } from "@material-ui/core";
import makeStyles from "@material-ui/core/styles/makeStyles";
import { ApplicationMenu } from "./ApplicationMenu"
import PersonAddIcon from '@material-ui/icons/PersonAdd';
import LibraryAddOutlinedIcon from '@material-ui/icons/LibraryAddOutlined';
import AddBoxOutlinedIcon from '@material-ui/icons/AddBoxOutlined';
import UndoIcon from '@material-ui/icons/Undo';
import RedoIcon from '@material-ui/icons/Redo';
import FilterCenterFocusIcon from '@material-ui/icons/FilterCenterFocus';
import { canRedoAtom, canUndoAtom } from "../diagram/Diagram";
import { useAtom } from "jotai";


export default function ApplicationBar({ height }) {

    const [canUndo] = useAtom(canUndoAtom)
    const [canRedo] = useAtom(canRedoAtom)
    const classes = useAppBarStyles();

    const undoStyle = canUndo ? classes.icons : classes.iconsDisabled
    const redoStyle = canRedo ? classes.icons : classes.iconsDisabled


    return (
        <AppBar position="static" style={{ height: height }}>
            <Toolbar>
                <Typography className={classes.title} variant="h6" noWrap>Dependinator</Typography>

                <Tooltip title={canUndo ? 'Undo' : ''} >
                    <IconButton disabled={!canUndo} onClick={() => PubSub.publish('diagram.Undo')}>
                        <UndoIcon className={undoStyle} /></IconButton></Tooltip>

                <Tooltip title={canRedo ? 'Redo' : ''} >
                    <IconButton disabled={!canRedo} onClick={() => PubSub.publish('diagram.Redo')}>
                        <RedoIcon className={redoStyle} /></IconButton></Tooltip>

                <Tooltip title="Add node" ><IconButton onClick={() => PubSub.publish('diagram.AddNode')}><AddBoxOutlinedIcon className={classes.icons} /></IconButton></Tooltip>
                <Tooltip title="Add user node" ><IconButton onClick={() => PubSub.publish('diagram.AddUserNode')}><PersonAddIcon className={classes.icons} /></IconButton></Tooltip>
                <Tooltip title="Add external system node" ><IconButton onClick={() => PubSub.publish('diagram.AddExternalNode')}><LibraryAddOutlinedIcon className={classes.icons} /></IconButton></Tooltip>
                <Tooltip title="Scroll and zoom to show full diagram" ><IconButton onClick={() => PubSub.publish('diagram.ShowTotalDiagram')}><FilterCenterFocusIcon className={classes.icons} /></IconButton></Tooltip>

                <Typography className={classes.space} variant="h6" noWrap> </Typography>
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
        //flexGrow: 1,
        display: 'none',
        [theme.breakpoints.up('sm')]: {
            display: 'block',
        },
    },
    space: {
        flexGrow: 1,
    },
    icons: {
        color: 'white',
    },
    iconsDisabled: {
        color: 'grey',
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