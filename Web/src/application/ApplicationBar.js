import React from "react";
import PubSub from 'pubsub-js'
import { Typography, fade, AppBar, Toolbar, IconButton, Tooltip, FormControlLabel, Switch, Box, } from "@material-ui/core";
import makeStyles from "@material-ui/core/styles/makeStyles";
import { ApplicationMenu } from "./ApplicationMenu"
import PersonAddIcon from '@material-ui/icons/PersonAdd';
import LibraryAddOutlinedIcon from '@material-ui/icons/LibraryAddOutlined';
import AddBoxOutlinedIcon from '@material-ui/icons/AddBoxOutlined';
import UndoIcon from '@material-ui/icons/Undo';
import RedoIcon from '@material-ui/icons/Redo';
import FilterCenterFocusIcon from '@material-ui/icons/FilterCenterFocus';
import SaveAltIcon from '@material-ui/icons/SaveAlt';
import { canPopDiagramAtom, canRedoAtom, canUndoAtom, editModeAtom, syncModeAtom, titleAtom } from "./Diagram";
import { useAtom } from "jotai";
import { withStyles } from "@material-ui/styles";
import { grey } from "@material-ui/core/colors";
import { store } from "./diagram/Store";
import { useLogin } from "./Login";


export default function ApplicationBar({ height }) {
    const classes = useAppBarStyles();
    const [titleText] = useAtom(titleAtom)
    const [editMode, setEditMode] = useAtom(editModeAtom);
    const [syncMode] = useAtom(syncModeAtom)
    const [canUndo] = useAtom(canUndoAtom)
    const [canRedo] = useAtom(canRedoAtom)
    const [canPopDiagram] = useAtom(canPopDiagramAtom)
    const [, setShowLogin] = useLogin()

    const handleEditModeChange = (event) => {
        setEditMode(event.target.checked);
        PubSub.publish('canvas.SetEditMode', event.target.checked)
    };
    const handleSyncModeChange = (event) => {
        const isChecked = event.target.checked
        if (!isChecked) {
            store.disableCloudSync()
            return
        }
        setShowLogin(true)
    };

    const style = (disabled) => {
        return !disabled ? classes.icons : classes.iconsDisabled
    }

    const styleAlways = (disabled) => {
        return !disabled ? classes.iconsAlways : classes.iconsAlwaysDisabled
    }

    return (
        <AppBar position="static" style={{ height: height }}>
            <Toolbar>


                <Button tooltip="Undo" disabled={!canUndo} icon={<UndoIcon className={styleAlways(!canUndo)} />}
                    onClick={() => PubSub.publish('canvas.Undo')} />
                <Button tooltip="Redo" disabled={!canRedo} icon={<RedoIcon className={styleAlways(!canRedo)} />}
                    onClick={() => PubSub.publish('canvas.Redo')} />

                <Typography className={classes.title} variant="h5" noWrap>|</Typography>

                <Button tooltip="Add node" icon={<AddBoxOutlinedIcon className={style()} />} className={style()}
                    onClick={() => PubSub.publish('canvas.AddNode')} />
                <Button tooltip="Add external user" icon={<PersonAddIcon className={style()} />} className={style()}
                    onClick={() => PubSub.publish('canvas.AddUserNode')} />
                <Button tooltip="Add external system" icon={<LibraryAddOutlinedIcon className={style()} />} className={style()}
                    onClick={() => PubSub.publish('canvas.AddExternalNode')} />

                <Typography className={classes.title} variant="h5" noWrap>|</Typography>

                <Button tooltip="Scroll and zoom to show all of the diagram" icon={<FilterCenterFocusIcon className={styleAlways()} />}
                    onClick={() => PubSub.publish('canvas.ShowTotalDiagram')} />
                <Button tooltip="Pop to surrounding diagram" disabled={!canPopDiagram} icon={<SaveAltIcon className={styleAlways(!canPopDiagram)} style={{ transform: 'rotate(180deg)' }} />}
                    onClick={() => PubSub.publish('canvas.PopInnerDiagram')} />

                <Box m={2} />
                <Typography className={classes.title} variant="h6" noWrap>{titleText}</Typography>

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

                <Tooltip title="Toggle cloud sync on/off" >
                    <FormControlLabel className={style()}
                        control={
                            <GreySwitch
                                checked={syncMode}
                                onChange={handleSyncModeChange}
                                name="Sync"
                            />
                        }
                        label="Sync"
                    />
                </Tooltip>

                <ApplicationMenu />

            </Toolbar>
        </AppBar >
    )
}


const Button = ({ icon, tooltip, disabled, onClick, className }) => {
    return (
        <Tooltip title={!disabled ? tooltip : ''} className={className}>
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
        display: 'none',
        [theme.breakpoints.up('md')]: {
            display: 'block',
        },
    },
    iconsDisabled: {
        color: 'grey',
        display: 'none',
        [theme.breakpoints.up('md')]: {
            display: 'block',
        },
    },
    iconsAlways: {
        color: 'white',
    },
    iconsAlwaysDisabled: {
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
        [theme.breakpoints.up('md')]: {
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