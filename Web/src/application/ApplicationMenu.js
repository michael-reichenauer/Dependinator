import React, { useState, useEffect } from "react";
import PubSub from 'pubsub-js'
import IconButton from "@material-ui/core/IconButton";
import MenuIcon from "@material-ui/icons/Menu";
import Tooltip from '@material-ui/core/Tooltip';
import { AppMenu, menuItem, menuParentItem } from "../common/Menus";
import { store } from "./diagram/Store";
import Printer from "../common/Printer";
import About, { useAbout } from "./About";



const asMenuItems = (diagrams) => {
    return diagrams.map(d => menuItem(d.name, () => PubSub.publish('canvas.OpenDiagram', d.diagramId)))
}

export function ApplicationMenu() {
    const [menu, setMenu] = useState(null);
    const [, setShowAbout] = useAbout()

    useEffect(() => {
        const handler = Printer.registerPrintKey(() => PubSub.publish('canvas.Print'))
        return () => Printer.deregisterPrintKey(handler)

    })

    const deleteDiagram = () => {
        var shouldDelete = confirm('Do you really want to delete the current diagram?') //eslint-disable-line
        if (shouldDelete) {
            PubSub.publish('canvas.DeleteDiagram')
        }
    };

    const diagrams = menu == null ? [] : asMenuItems(store.getRecentDiagramInfos().slice(1))

    const menuItems = [
        menuItem('New Diagram', () => PubSub.publish('canvas.NewDiagram')),
        menuParentItem('Open Recent', diagrams, diagrams.length > 0),
        menuItem('Print', () => PubSub.publish('canvas.Print')),
        menuItem('Delete', deleteDiagram),
        menuParentItem('Enable cloud sync', [
            menuItem('With GitHub id ...', () => store.login('GitHub'), false, !store.isLocal()),
            menuItem('With Local id ...', () => store.login('GitHub'), false, store.isLocal()),
        ], false, !store.isCloudSyncEnabled),
        menuItem('Disable cloud sync', () => store.disableCloudSync(), false, store.isCloudSyncEnabled),
        menuItem('Reload web page', () => window.location.reload()),
        menuParentItem('Files', [
            menuItem('Open file ...', () => PubSub.publish('canvas.OpenFile')),
            menuItem('Save diagram to file', () => PubSub.publish('canvas.SaveDiagramToFile')),
            menuItem('Save/Archive all to file', () => PubSub.publish('canvas.ArchiveToFile')),
        ]),
        menuItem('About', () => setShowAbout(true)),
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

            <About />
        </>
    )
}


