import React, { useState, useEffect, useRef } from "react";
import PubSub from "pubsub-js";
import IconButton from "@material-ui/core/IconButton";
import MenuIcon from "@material-ui/icons/Menu";
import Tooltip from "@material-ui/core/Tooltip";
import { AppMenu, menuItem, menuParentItem } from "../common/Menus";
import { IStoreKey } from "./diagram/Store";
import Printer from "../common/Printer";
import { useAbout } from "./About";
import { showConfirmAlert } from "../common/AlertDialog";
import { showPrompt } from "../common/PromptDialog";
import { di } from "../common/di";
import { useTitle } from "./Diagram";
import { IOnlineKey, SyncState, useSyncMode } from "./Online";
import { DiagramInfoDto } from "./diagram/StoreDtos";

const getDiagramsMenuItems = (recentDiagrams: DiagramInfoDto[]) => {
  const diagrams = recentDiagrams.slice(1);
  return diagrams.map((d) =>
    menuItem(d.name, () => PubSub.publish("canvas.OpenDiagram", d.id))
  );
};

export function ApplicationMenu() {
  const onlineRef = useRef(di(IOnlineKey));
  const storeRef = useRef(di(IStoreKey));
  const syncMode = useSyncMode();
  const [menu, setMenu] = useState(null);
  const [, setShowAbout] = useAbout();

  const [titleText] = useTitle();

  useEffect(() => {
    const handler = Printer.registerPrintKey(() =>
      PubSub.publish("canvas.Print")
    );
    return () => Printer.deregisterPrintKey(handler);
  });

  const deleteDiagram = () => {
    showConfirmAlert(
      "Delete",
      "Do you really want to delete the current diagram?",
      () => PubSub.publish("canvas.DeleteDiagram")
    );
  };

  const renameDiagram = () => {
    var name = titleText;
    const index = titleText.lastIndexOf(" - ");
    if (index > -1) {
      name = name.substring(0, index);
    }

    showPrompt("Rename Diagram", "", name, (name: string) =>
      PubSub.publish("canvas.RenameDiagram", name)
    );
  };

  const diagrams =
    menu == null
      ? []
      : getDiagramsMenuItems(storeRef.current.getRecentDiagrams());
  const isInStandaloneMode = () =>
    window.matchMedia("(display-mode: standalone)").matches ||
    // @ts-ignore
    window.navigator.standalone ||
    document.referrer.includes("android-app://");

  const menuItems = [
    menuItem("New Diagram", () => PubSub.publish("canvas.NewDiagram")),
    menuParentItem("Open Recent", diagrams, diagrams.length > 0),
    menuItem("Rename", renameDiagram),
    menuItem("Print", () => PubSub.publish("canvas.Print"), true),
    menuParentItem("Export", [
      menuItem("As png file", () =>
        PubSub.publish("canvas.Export", { type: "png", target: "file" })
      ),
      menuItem("As svg file", () =>
        PubSub.publish("canvas.Export", { type: "svg", target: "file" })
      ),
    ]),
    menuItem("Delete", deleteDiagram),
    menuItem(
      "Enable device sync",
      () => onlineRef.current.enableSync(),
      syncMode !== SyncState.Progress,
      syncMode === SyncState.Disabled
    ),
    menuItem(
      "Disable device sync",
      () => onlineRef.current.disableSync(),
      syncMode !== SyncState.Progress,
      syncMode !== SyncState.Disabled
    ),
    menuParentItem(
      "Files",
      [
        menuItem("Open file ...", () => PubSub.publish("canvas.OpenFile")),
        menuItem("Save diagram to file", () =>
          PubSub.publish("canvas.SaveDiagramToFile")
        ),
        menuItem("Save/Archive all to file", () =>
          PubSub.publish("canvas.ArchiveToFile")
        ),
      ],
      false
    ),
    menuItem(
      "Reload web page",
      () => window.location.reload(),
      true,
      isInStandaloneMode()
    ),
    menuItem("About", () => setShowAbout(true)),
    // menuParentItem('Advanced', [
    //     menuItem('Clear all local data', () => clearLocalData()),
    //     menuItem('Clear all local and remote user data', () => clearAllData()),
    // ]),
  ];

  return (
    <>
      <Tooltip title="Customize and control">
        <IconButton
          edge="start"
          color="inherit"
          onClick={(e: any) => setMenu(e.currentTarget)}
        >
          <MenuIcon />
        </IconButton>
      </Tooltip>

      <AppMenu anchorEl={menu} items={menuItems} onClose={setMenu} />
    </>
  );
}
