import React from "react";
import PubSub from 'pubsub-js'
import { Typography, fade, AppBar, Toolbar, IconButton, Tooltip, FormControlLabel, Switch, } from "@material-ui/core";
import makeStyles from "@material-ui/core/styles/makeStyles";
import { ApplicationMenu } from "./ApplicationMenu"
import PersonAddIcon from '@material-ui/icons/PersonAdd';
import LibraryAddOutlinedIcon from '@material-ui/icons/LibraryAddOutlined';
import AddBoxOutlinedIcon from '@material-ui/icons/AddBoxOutlined';
import UndoIcon from '@material-ui/icons/Undo';
import RedoIcon from '@material-ui/icons/Redo';
import FilterCenterFocusIcon from '@material-ui/icons/FilterCenterFocus';
import ZoomOutMapIcon from '@material-ui/icons/ZoomOutMap';
import { canPopDiagramAtom, canRedoAtom, canUndoAtom, editModeAtom } from "../diagram/Diagram";
import { useAtom } from "jotai";
import { withStyles } from "@material-ui/styles";
import { grey } from "@material-ui/core/colors";


export default function ApplicationBar({ height }) {
    const classes = useAppBarStyles();
    const [editMode, setEditMode] = useAtom(editModeAtom);
    const [canUndo] = useAtom(canUndoAtom)
    const [canRedo] = useAtom(canRedoAtom)
    const [canPopDiagram] = useAtom(canPopDiagramAtom)

    const handleEditModeChange = (event) => {
        setEditMode(event.target.checked);
        PubSub.publish('canvas.SetEditMode', event.target.checked)
    };

    const style = (disabled) => {
        return !disabled ? classes.icons : classes.iconsDisabled
    }

    return (
        <AppBar position="static" style={{ height: height }}>
            <Toolbar>
                <Typography className={classes.title} variant="h6" noWrap>Dependinator</Typography>

                <Button tooltip="Undo" disabled={!canUndo} icon={<UndoIcon className={style(!canUndo)} />}
                    onClick={() => PubSub.publish('canvas.Undo')} />
                <Button tooltip="Redo" disabled={!canRedo} icon={<RedoIcon className={style(!canRedo)} />}
                    onClick={() => PubSub.publish('canvas.Redo')} />

                <Typography className={classes.title} variant="h5" noWrap>|</Typography>

                <Button tooltip="Add node" icon={<AddBoxOutlinedIcon className={style()} />}
                    onClick={() => PubSub.publish('canvas.AddNode')} />
                <Button tooltip="Add user node" icon={<PersonAddIcon className={style()} />}
                    onClick={() => PubSub.publish('canvas.AddUserNode')} />
                <Button tooltip="Add external node" icon={<LibraryAddOutlinedIcon className={style()} />}
                    onClick={() => PubSub.publish('canvas.AddExternalNode')} />

                <Typography className={classes.title} variant="h5" noWrap>|</Typography>

                <Button tooltip="Scroll and zoom to show all of the diagram" icon={<FilterCenterFocusIcon className={style()} />}
                    onClick={() => PubSub.publish('canvas.ShowTotalDiagram')} />
                <Button tooltip="Pop to surrounding diagram" disabled={!canPopDiagram} icon={<ZoomOutMapIcon className={style(!canPopDiagram)} />}
                    onClick={() => PubSub.publish('canvas.CloseInnerDiagram')} />

                <Typography className={classes.space} variant="h6" noWrap> </Typography>

                <Tooltip title="Toggle edit mode" >
                    <FormControlLabel
                        control={
                            <GreySwitch
                                checked={editMode}
                                onChange={handleEditModeChange}
                                name="Edit"
                            />
                        }
                        label="Edit"
                    />
                </Tooltip>

                <ApplicationMenu />

            </Toolbar>
        </AppBar >
    )
}


const Button = ({ icon, tooltip, disabled, onClick }) => {
    return (
        <Tooltip title={!disabled ? tooltip : ''} >
            <IconButton disabled={disabled} onClick={onClick}>
                {icon}</IconButton></Tooltip>
    )
}


const GreySwitch = withStyles({
    switchBase: {
        color: grey[400],
        '&$checked': {
            color: grey[50],
        },
        '&$checked + $track': {
            backgroundColor: grey[50],
        },
    },
    checked: {},
    track: {},
})(Switch);


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