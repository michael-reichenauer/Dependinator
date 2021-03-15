import React, { useEffect, useRef, useState } from "react";
import PubSub from 'pubsub-js'
import Canvas from "./Canvas"
import { getCommonEvent } from "../common/events";
import { atom, useAtom } from 'jotai'
import { Backdrop, makeStyles } from "@material-ui/core";
import ContextMenu from "../common/ContextMenu";


export const canUndoAtom = atom(false)
export const canRedoAtom = atom(false)
export const canPopDiagramAtom = atom(false)
export const progressAtom = atom(false)
export const editModeAtom = atom(false)

const useStyles = makeStyles((theme) => ({
    backdrop: {
        zIndex: theme.zIndex.drawer + 1,
        color: '#fff',
    },
}));

export default function Diagram({ width, height }) {
    const canvasRef = useRef(null)
    const [contextMenu, setContextMenu] = useState()
    const [, setCanUndo] = useAtom(canUndoAtom)
    const [, setCanRedo] = useAtom(canRedoAtom)
    const [, setCanPopDiagram] = useAtom(canPopDiagramAtom)
    const [, setEditMode] = useAtom(editModeAtom)
    const [isProgress, setProgress] = useAtom(progressAtom)
    const classes = useStyles();

    useEffect(() => {
        // Initialize canvas
        const callbacks = {
            setCanUndo: setCanUndo,
            setCanRedo: setCanRedo,
            setProgress: setProgress,
            setCanPopDiagram: setCanPopDiagram,
            setEditMode: setEditMode
        }
        const canvas = new Canvas('canvas', callbacks);
        canvas.init()
        canvasRef.current = canvas

        const contextMenuHandler = enableContextMenu(setContextMenu, canvas)
        PubSub.subscribe('diagram.Export', () => exportDiagram(canvas))

        setTimeout(() => canvas.showTotalDiagram(), 0);

        return () => {
            // Clean initialization 
            PubSub.unsubscribe('diagram');
            document.removeEventListener("contextmenu", contextMenuHandler);
            canvasRef.current.delete()
        }
    }, [setCanUndo, setCanRedo, setProgress, setCanPopDiagram, setEditMode])


    return (
        <>
            <Backdrop className={classes.backdrop} open={isProgress} />

            <div id="diagram">
                <div id="canvas" style={{
                    width: width, height: height, maxWidth: width, maxHeight: height, position: 'absolute',
                    overflow: 'scroll'
                }}>
                </div>
            </div>

            <ContextMenu menu={contextMenu} onClose={setContextMenu} />
        </>
    )
}



function enableContextMenu(setContextMenu, canvas) {
    const handleContextMenu = (event) => {
        if (!event.path.some((i) => i.id === 'diagram')) {
            // Not a right click within the diagram canvas 
            return
        }
        event.preventDefault();
        event = getCommonEvent(event)

        const { x, y } = { x: event.clientX, y: event.clientY }

        // Get target figure or use canvas as target
        let figure = getFigure(canvas, event)
        const target = figure ?? canvas

        if (typeof target.getContextMenuItems !== "function") {
            // No context menu on target
            return
        }

        const menuItems = target.getContextMenuItems(x, y)
        setContextMenu({ items: menuItems, x: x, y: y });
    }

    document.addEventListener("contextmenu", handleContextMenu);
    return handleContextMenu
}

const getFigure = (canvas, event) => {
    let figure = canvas.tryGetFigure(event.clientX, event.clientY)
    if (typeof figure.getContextMenuItems !== "function" && figure.getParent() != null) {
        // Figure did not have context menu, but has a parent (e.g. a label) lets try parent
        figure = figure.getParent()
    }
    return figure
}

function exportDiagram(canvas) {
    // Open other tab
    const tab = window.open(":", "_blank");
    tab.document.open();

    canvas.export(svg => {
        tab.document.write(`
            <html style="margin: 0; ">
                <head><title>Dependinator Diagram</title></head>
                <body style="margin: 0;">${svg}</body>
            </html>`)
        tab.focus(); //required for IE
    })

    //tab.print();
}
