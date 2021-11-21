// @ts-nocheck

import React from "react";
import PubSub from "pubsub-js";
import {
  Typography,
  AppBar,
  Toolbar,
  IconButton,
  Tooltip,
  Box,
} from "@material-ui/core";

import makeStyles from "@material-ui/core/styles/makeStyles";
import { ApplicationMenu } from "./ApplicationMenu";
import AddBoxOutlinedIcon from "@material-ui/icons/AddBoxOutlined";

// import SyncIcon from '@material-ui/icons/Sync';
// import SyncProblemIcon from '@material-ui/icons/SyncProblem';
// import SyncDisabledIcon from '@material-ui/icons/SyncDisabled';
import UndoIcon from "@material-ui/icons/Undo";
import RedoIcon from "@material-ui/icons/Redo";
import TuneIcon from "@material-ui/icons/Tune";
import FilterCenterFocusIcon from "@material-ui/icons/FilterCenterFocus";

import { canRedoAtom, canUndoAtom, selectModeAtom, titleAtom } from "./Diagram";
import { useAtom } from "jotai";
// import { store } from "./diagram/Store";
//import { useLogin } from "./Login";
// import { useSyncMode } from './Online'
// import { useConnection } from "./diagram/Api";
import { showPrompt } from "./../common/PromptDialog";

export default function ApplicationBar({ height }) {
  const classes = useAppBarStyles();
  const [titleText] = useAtom(titleAtom);
  const [selectMode] = useAtom(selectModeAtom);

  // const [syncMode] = useSyncMode()
  const [canUndo] = useAtom(canUndoAtom);
  const [canRedo] = useAtom(canRedoAtom);
  // const [canPopDiagram] = useAtom(canPopDiagramAtom)
  //const [, setShowLogin] = useLogin()
  //const [connection] = useConnection()

  // const syncState = syncMode && connection ? true : syncMode && !connection ? false : null

  const style = (disabled) => {
    return !disabled ? classes.icons : classes.iconsDisabled;
  };

  const styleAlways = (disabled) => {
    return !disabled ? classes.iconsAlways : classes.iconsAlwaysDisabled;
  };

  const renameDiagram = () => {
    var name = titleText;
    const index = titleText.lastIndexOf(" - ");
    if (index > -1) {
      name = name.substring(0, index);
    }

    showPrompt("Rename Diagram", null, name, (name) =>
      PubSub.publish("canvas.RenameDiagram", name)
    );
  };

  return (
    <AppBar position="static" style={{ height: height }}>
      <Toolbar>
        <ApplicationMenu />
        {/* {syncState === true && <Button  tooltip={`Cloud sync enabled and OK for ${details}, ${provider}, click to check cloud connection`} icon={<SyncIcon style={{ color: 'Lime' }} />}
                    onClick={() => store.checkCloudConnection()} />}
                {syncState === false && <Button  tooltip="Cloud connection error, sync disabled, click to retry" icon={<SyncProblemIcon style={{ color: '#FF3366' }} />}
                    onClick={() => store.checkCloudConnection()} />}
                {syncState === null && <Button  tooltip="Cloud sync disabled, click to enable" icon={<SyncDisabledIcon style={{ color: '#FFFF66' }} />}
                    onClick={() => setShowLogin(true)} />} */}

        <Button
          tooltip="Undo"
          disabled={!canUndo}
          icon={<UndoIcon className={styleAlways(!canUndo)} />}
          onClick={() => PubSub.publish("canvas.Undo")}
        />
        <Button
          tooltip="Redo"
          disabled={!canRedo}
          icon={<RedoIcon className={styleAlways(!canRedo)} />}
          onClick={() => PubSub.publish("canvas.Redo")}
        />

        <Button
          tooltip="Add node"
          icon={<AddBoxOutlinedIcon className={styleAlways()} />}
          onClick={(e) => {
            PubSub.publish("nodes.showDialog", { add: true });
          }}
        />
        <Button
          tooltip="Tune selected item"
          disabled={!selectMode}
          icon={<TuneIcon className={styleAlways(!selectMode)} />}
          onClick={(e) => {
            PubSub.publish("canvas.TuneSelected", { x: e.pageX - 20, y: 50 });
          }}
        />

        <Button
          tooltip="Scroll and zoom to show all of the diagram"
          icon={<FilterCenterFocusIcon className={styleAlways()} />}
          onClick={() => PubSub.publish("canvas.ShowTotalDiagram")}
        />
        {/* <Button tooltip="Pop to surrounding diagram" disabled={!canPopDiagram} icon={<SaveAltIcon className={styleAlways(!canPopDiagram)} style={{ transform: 'rotate(180deg)' }} />}
                    onClick={() => PubSub.publish('canvas.PopInnerDiagram')} /> */}

        {/* <ToggleButtonGroup
                    size="small"
                    value={editToggle}
                    onChange={handleEditToggleChange}
                >
                    <ToggleButton value="pan" ><Tooltip title="Enable pan mode"><ControlCameraIcon className={editStyleAlways(editMode)} /></Tooltip></ToggleButton>
                    <ToggleButton value="edit" ><Tooltip title="Enable edit mode"><EditIcon className={editStyleAlways(!editMode)} /></Tooltip></ToggleButton>
                </ToggleButtonGroup> */}

        <Box m={1} className={style()} />
        <Typography
          className={classes.title}
          variant="h6"
          noWrap
          onClick={renameDiagram}
        >
          {titleText}
        </Typography>
      </Toolbar>
    </AppBar>
  );
}

const Button = ({ icon, tooltip, disabled, onClick, className }) => {
  return (
    <Tooltip title={tooltip} className={className}>
      <span>
        <IconButton
          disabled={disabled}
          onClick={onClick}
          style={{ padding: 5 }}
        >
          {icon}
        </IconButton>
      </span>
    </Tooltip>
  );
};

const useAppBarStyles = makeStyles((theme) => ({
  root: {
    flexGrow: 1,
  },
  title: {
    //flexGrow: 1,
    display: "block",
    // [theme.breakpoints.up('sm')]: {
    //     display: 'block',
    // },
  },
  space: {
    flexGrow: 1,
  },
  icons: {
    color: "white",
    display: "none",
    [theme.breakpoints.up("md")]: {
      display: "block",
    },
  },
  iconsDisabled: {
    color: "grey",
    display: "none",
    [theme.breakpoints.up("md")]: {
      display: "block",
    },
  },
  iconsAlways: {
    color: "white",
  },
  iconsAlwaysDisabled: {
    color: "grey",
  },

  iconsAlwaysDarker: {
    color: "Silver",
  },
  connectionIcons: {
    color: "green",
  },
}));
