import React, { useState, useEffect } from "react";
import PubSub from 'pubsub-js'
import IconButton from "@material-ui/core/IconButton";
import MenuIcon from "@material-ui/icons/Menu";
import Tooltip from '@material-ui/core/Tooltip';
import { AppMenu, menuItem, menuParentItem } from "../common/Menus";
import { store } from "./diagram/Store";
import Printer from "../common/Printer";
import { useAbout } from "./About";
// import { useLogin } from "./Login";
import { showConfirmAlert } from "../common/AlertDialog";
import { showPrompt } from './../common/PromptDialog';
import { useAtom } from 'jotai';
import { titleAtom } from './Diagram';


const getDiagramsMenuItems = () => {
    const diagrams = store.getRecentDiagramInfos().slice(1)
    return diagrams.map(d => menuItem(d.name, () => PubSub.publish('canvas.OpenDiagram', d.diagramId)))
}

export function ApplicationMenu() {
    const [menu, setMenu] = useState(null);
    const [, setShowAbout] = useAbout()
    //const [, setShowLogin] = useLogin()
    const [titleText] = useAtom(titleAtom)

    useEffect(() => {
        const handler = Printer.registerPrintKey(() => PubSub.publish('canvas.Print'))
        return () => Printer.deregisterPrintKey(handler)

    })

    const deleteDiagram = () => {
        showConfirmAlert('Delete', 'Do you really want to delete the current diagram?',
            () => PubSub.publish('canvas.DeleteDiagram'))
    };

    const renameDiagram = () => {
        var name = titleText
        const index = titleText.lastIndexOf(' - ')
        if (index > -1) {
            name = name.substring(0, index)
        }

        showPrompt('Rename Diagram', null, name,
            (name) => PubSub.publish('canvas.RenameDiagram', name))
    };


    // const clearLocalData = () => {
    //     if (!confirm('Do you really want to clear all local data?')) {//eslint-disable-line
    //         return
    //     }

    //     store.clearLocalData()
    //     window.location.reload()
    // };

    // const clearAllData = async () => {
    //     if (!confirm('Do you really want to clear all local and remote data?')) {//eslint-disable-line
    //         return
    //     }

    //     if (await store.clearRemoteData()) {
    //         store.clearLocalData()
    //         window.location.reload()
    //     }
    // };

    const diagrams = menu == null ? [] : getDiagramsMenuItems()
    const isInStandaloneMode = () =>
        (window.matchMedia('(display-mode: standalone)').matches)
        || (window.navigator.standalone)
        || document.referrer.includes('android-app://');

    const menuItems = [
        menuItem('New Diagram', () => PubSub.publish('canvas.NewDiagram')),
        menuParentItem('Open Recent', diagrams, diagrams.length > 0),
        menuItem('Rename', renameDiagram),
        menuItem('Print', () => PubSub.publish('canvas.Print'), true),
        menuParentItem('Export', [
            menuItem('As png file', () => PubSub.publish('canvas.Export', { type: 'png', target: 'file' })),
            menuItem('As png in new tab', () => PubSub.publish('canvas.Export', { type: 'png', target: 'tab' })),

            menuItem('As svg file', () => PubSub.publish('canvas.Export', { type: 'svg', target: 'file' })),
            menuItem('As svg in new tab', () => PubSub.publish('canvas.Export', { type: 'svg', target: 'tab' })),

        ]),
        menuItem('Delete', deleteDiagram),
        // menuItem('Enable cloud sync', () => setShowLogin(true), false, !store.isCloudSyncEnabled()),
        // menuItem('Disable cloud sync', () => store.disableCloudSync(), false, store.isCloudSyncEnabled()),
        menuParentItem('Files', [
            menuItem('Open file ...', () => PubSub.publish('canvas.OpenFile')),
            menuItem('Save diagram to file', () => PubSub.publish('canvas.SaveDiagramToFile')),
            menuItem('Save/Archive all to file', () => PubSub.publish('canvas.ArchiveToFile')),
        ]),
        menuItem('Reload web page', () => window.location.reload(), true, isInStandaloneMode()),
        menuItem('About', () => setShowAbout(true)),
        // menuParentItem('Advanced', [
        //     menuItem('Clear all local data', () => clearLocalData()),
        //     menuItem('Clear all local and remote user data', () => clearAllData()),
        // ]),
    ]

    return (
        <>
            <Tooltip title="Customize and control">
                <IconButton
                    edge="start"
                    color="inherit"
                    onClick={e => setMenu(e.currentTarget)}
                >
                    <MenuIcon />
                </IconButton>
            </Tooltip>

            <AppMenu anchorEl={menu} items={menuItems} onClose={setMenu} />
        </>
    )
}


