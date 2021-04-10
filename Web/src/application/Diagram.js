import React, { useEffect, useRef, useState } from "react";
import PubSub from 'pubsub-js'
import DiagramCanvas from "./diagram/DiagramCanvas"
import { getCommonEvent } from "../common/events";
import { atom, useAtom } from 'jotai'
import { ContextMenu } from "../common/Menus";
import Progress from '../common/Progress'
import { activityEventName } from '../common/activity'


export const titleAtom = atom('System')
export const canUndoAtom = atom(false)
export const canRedoAtom = atom(false)
export const canPopDiagramAtom = atom(false)
export const editModeAtom = atom(false)


export default function Diagram({ width, height }) {
    // The ref to the canvas handler for all canvas operations
    const canvasRef = useRef(null)
    const activeRef = useRef(true)

    const [contextMenu, setContextMenu] = useState()
    const [, setTitle] = useAtom(titleAtom)
    const [, setCanUndo] = useAtom(canUndoAtom)
    const [, setCanRedo] = useAtom(canRedoAtom)
    const [, setCanPopDiagram] = useAtom(canPopDiagramAtom)
    const [, setEditMode] = useAtom(editModeAtom)

    useEffect(() => {
        const onActivityEvent = (activity) => {
            if (!activeRef.current && activity.detail) {
                canvasRef.current.activated()
            }
            activeRef.current = activity.detail
        }

        const callbacks = {
            setTitle: setTitle,
            setCanUndo: setCanUndo,
            setCanRedo: setCanRedo,
            setCanPopDiagram: setCanPopDiagram,
            setEditMode: setEditMode,
        }

        const canvas = new DiagramCanvas('canvas', callbacks);
        canvasRef.current = canvas
        canvas.init()

        const contextMenuHandler = enableContextMenu('canvas', setContextMenu, canvas)

        document.addEventListener(activityEventName, onActivityEvent)

        return () => {
            // Clean initialization 
            document.removeEventListener(activityEventName, onActivityEvent)
            PubSub.unsubscribe('diagram');
            var el = document.getElementById('canvas');
            el.removeEventListener("contextmenu", contextMenuHandler);
            document.removeEventListener("longclick", contextMenuHandler);
            canvasRef.current.delete()
        }
    }, [setCanUndo, setCanRedo, setCanPopDiagram, setEditMode, setTitle])

    return (
        <>
            <Progress />

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

function enableContextMenu(elementId, setContextMenu, canvas) {
    const handleContextMenu = (event) => {
        //console.log('right', event)
        if (event.type === 'longclick') {
            // long click event from Canvas to simulate context menu on touch device
            event = event.detail
        } else {
            // Normal contextmenu event
            event.preventDefault();
            event = getCommonEvent(event)
        }

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

    var el = document.getElementById(elementId);
    el.addEventListener("contextmenu", handleContextMenu);
    document.addEventListener("longclick", handleContextMenu);
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

