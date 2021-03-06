import React from "react";
import PubSub from 'pubsub-js'
import { Typography, AppBar, Toolbar, IconButton, Tooltip, Box, } from "@material-ui/core";
import { ToggleButton, ToggleButtonGroup } from "@material-ui/lab";
import makeStyles from "@material-ui/core/styles/makeStyles";
import { ApplicationMenu } from "./ApplicationMenu"
import PersonAddIcon from '@material-ui/icons/PersonAdd';
import LibraryAddOutlinedIcon from '@material-ui/icons/LibraryAddOutlined';
import AddBoxOutlinedIcon from '@material-ui/icons/AddBoxOutlined';
import SyncIcon from '@material-ui/icons/Sync';
import SyncProblemIcon from '@material-ui/icons/SyncProblem';
import SyncDisabledIcon from '@material-ui/icons/SyncDisabled';
import UndoIcon from '@material-ui/icons/Undo';
import RedoIcon from '@material-ui/icons/Redo';
import ControlCameraIcon from '@material-ui/icons/ControlCamera';
import EditIcon from '@material-ui/icons/Edit';
import FilterCenterFocusIcon from '@material-ui/icons/FilterCenterFocus';
import SaveAltIcon from '@material-ui/icons/SaveAlt';
import { canPopDiagramAtom, canRedoAtom, canUndoAtom, editModeAtom, titleAtom } from "./Diagram";
import { useAtom } from "jotai";
import { store } from "./diagram/Store";
import { useLogin } from "./Login";
import { useSyncMode } from './Online'
import { useConnection } from "./diagram/Api";



export default function ApplicationBar({ height }) {
    const classes = useAppBarStyles();
    const [titleText] = useAtom(titleAtom)
    const [editMode, setEditMode] = useAtom(editModeAtom);
    const [syncMode] = useSyncMode()
    const [canUndo] = useAtom(canUndoAtom)
    const [canRedo] = useAtom(canRedoAtom)
    const [canPopDiagram] = useAtom(canPopDiagramAtom)
    const [, setShowLogin] = useLogin()
    const [connection] = useConnection()

    const syncState = syncMode && connection ? true : syncMode && !connection ? false : null

    const style = (disabled) => {
        return !disabled ? classes.icons : classes.iconsDisabled
    }

    const styleAlways = (disabled) => {
        return !disabled ? classes.iconsAlways : classes.iconsAlwaysDisabled
    }
    const editStyleAlways = (disabled) => {
        return !disabled ? classes.iconsAlways : classes.iconsAlwaysDarker
    }

    const editToggle = editMode ? ['edit'] : ['pan']

    const handleEditToggleChange = (_, newMode) => {
        if (!editMode && newMode.includes('edit')) {
            setEditMode(true)
            PubSub.publish('canvas.SetEditMode', true)
            return
        }
        if (editMode && newMode.includes('pan')) {
            setEditMode(false)
            PubSub.publish('canvas.SetEditMode', false)
            return
        }
    }

    const { details, provider } = store.getSync()

    return (
        <AppBar position="static" style={{ height: height }}>
            <Toolbar>
                <ApplicationMenu />
                {syncState === true && <Button tooltip={`Cloud sync enabled and OK for ${details}, ${provider}, click to check cloud connection`} icon={<SyncIcon style={{ color: 'Lime' }} />}
                    onClick={() => store.checkCloudConnection()} />}
                {syncState === false && <Button tooltip="Cloud connection error, sync disabled, click to retry" icon={<SyncProblemIcon style={{ color: '#FF3366' }} />}
                    onClick={() => store.checkCloudConnection()} />}
                {syncState === null && <Button tooltip="Cloud sync disabled, click to enable" icon={<SyncDisabledIcon style={{ color: '#FFFF66' }} />}
                    onClick={() => setShowLogin(true)} />}

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

                <ToggleButtonGroup
                    size="small"
                    value={editToggle}
                    onChange={handleEditToggleChange}
                >
                    <ToggleButton value="pan" ><Tooltip title="Enable pan mode"><ControlCameraIcon className={editStyleAlways(editMode)} /></Tooltip></ToggleButton>
                    <ToggleButton value="edit" ><Tooltip title="Enable edit mode"><EditIcon className={editStyleAlways(!editMode)} /></Tooltip></ToggleButton>
                </ToggleButtonGroup>

                <Box m={2} className={style()} />
                <Typography className={classes.title} variant="h6" noWrap>{titleText}</Typography>
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

    iconsAlwaysDarker: {
        color: 'Silver',
    },
    connectionIcons: {
        color: 'green',
    },
}));