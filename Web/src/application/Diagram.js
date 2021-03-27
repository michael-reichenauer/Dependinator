import React, { useEffect, useRef, useState } from "react";
import PubSub from 'pubsub-js'
import DiagramCanvas from "./diagram/DiagramCanvas"
import { getCommonEvent } from "../common/events";
import { atom, useAtom } from 'jotai'
import { Backdrop, makeStyles } from "@material-ui/core";
import { ContextMenu } from "../common/Menus";
//import { useLongPress } from "use-long-press";
//import useLongPress from "../common/useLongPress";

export const titleAtom = atom('System')
export const canUndoAtom = atom(false)
export const canRedoAtom = atom(false)
export const canPopDiagramAtom = atom(false)
export const progressAtom = atom(false)
export const editModeAtom = atom(false)



export default function Diagram({ width, height }) {
    // The ref to the canvas handler for all canvas operations
    const canvasRef = useRef(null)

    const [contextMenu, setContextMenu] = useState()
    const [, setTitle] = useAtom(titleAtom)
    const [, setCanUndo] = useAtom(canUndoAtom)
    const [, setCanRedo] = useAtom(canRedoAtom)
    const [, setCanPopDiagram] = useAtom(canPopDiagramAtom)
    const [, setEditMode] = useAtom(editModeAtom)
    const [isProgress, setProgress] = useAtom(progressAtom)
    const classes = useStyles();


    // const bind = useLongPress(event => {
    //     const { x, y } = { x: event.clientX, y: event.clientY }

    //     // Get target figure or use canvas as target
    //     let figure = getFigure(canvasRef.current, event)
    //     const target = figure ?? canvasRef.current

    //     if (typeof target.getContextMenuItems !== "function") {
    //         // No context menu on target
    //         return
    //     }

    //     const menuItems = target.getContextMenuItems(x, y)
    //     setTimeout(() => {
    //         setContextMenu({ items: menuItems, x: x, y: y });
    //     }, 100);

    // });

    useEffect(() => {
        // Initialize canvas
        const callbacks = {
            setTitle: setTitle,
            setCanUndo: setCanUndo,
            setCanRedo: setCanRedo,
            setProgress: setProgress,
            setCanPopDiagram: setCanPopDiagram,
            setEditMode: setEditMode
        }
        const canvas = new DiagramCanvas('canvas', callbacks);
        canvas.init()
        canvasRef.current = canvas

        const contextMenuHandler = enableContextMenu(setContextMenu, canvas)
        setTimeout(() => canvas.showTotalDiagram(), 0);

        return () => {
            // Clean initialization 
            PubSub.unsubscribe('diagram');
            document.removeEventListener("contextmenu", contextMenuHandler);
            canvasRef.current.delete()
        }
    }, [setCanUndo, setCanRedo, setProgress, setCanPopDiagram, setEditMode, setTitle])

    // const onLongPress = (event) => {
    //     console.log('d', event)
    //     const { x, y } = { x: event.clientX, y: event.clientY }

    //     // Get target figure or use canvas as target
    //     let figure = getFigure(canvasRef.current, event)
    //     const target = figure ?? canvasRef.current

    //     if (typeof target.getContextMenuItems !== "function") {
    //         // No context menu on target
    //         return
    //     }

    //     const menuItems = target.getContextMenuItems(x, y)
    //     setContextMenu({ items: menuItems, x: x, y: y });
    // };


    // const defaultOptions = {
    //     shouldPreventDefault: true,
    //     delay: 500,
    // };
    // const longPressEvent = useLongPress(onLongPress, () => { }, defaultOptions);


    return (
        <>
            <Backdrop className={classes.backdrop} open={isProgress} />

            <div id="diagram">
                <div id="canvas" style={{
                    width: width, height: height, maxWidth: width, maxHeight: height,
                    position: 'absolute', overflow: 'scroll'
                }} />
                <div id="canvasPrint" style={{
                    width: 0, height: 0, maxWidth: 0, maxHeight: 0, position: 'absolute', overflow: 'hidden'
                }} />
            </div>

            <ContextMenu menu={contextMenu} onClose={setContextMenu} />
        </>
    )
}

const useStyles = makeStyles((theme) => ({
    backdrop: {
        zIndex: theme.zIndex.drawer + 1,
        color: '#fff',
    },
}));

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
    if (figure == null) {
        return null
    }
    if (typeof figure.getContextMenuItems !== "function" && figure.getParent() != null) {
        // Figure did not have context menu, but has a parent (e.g. a label) lets try parent
        figure = figure.getParent()
    }
    return figure
}

