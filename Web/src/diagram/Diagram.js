import React, { useEffect, useRef, useState } from "react";
import "import-jquery";
import "jquery-ui-bundle";
import "jquery-ui-bundle/jquery-ui.css";
import PubSub from 'pubsub-js'
import Canvas from "./Canvas"
import CanvasMenu from "./CanvasMenu";
import FigureMenu from "./FigureMenu";
import { getCommonEvent } from "../common/events";
import { atom, useAtom } from 'jotai'
import { groupType } from "./figures";

export const canUndoAtom = atom(false)
export const canRedoAtom = atom(false)


export default function Diagram({ width, height }) {
    const canvasRef = useRef(null)
    const [contextMenu, setContextMenu] = useState()
    const [, setCanUndo] = useAtom(canUndoAtom)
    const [, setCanRedo] = useAtom(canRedoAtom)

    useEffect(() => {
        // Initialize canvas
        const canvas = new Canvas('canvas', setCanUndo, setCanRedo);
        canvasRef.current = canvas

        const contextMenuHandler = enableContextMenu(setContextMenu, canvas)
        HandleToolbarCommands(canvas)

        setTimeout(() => canvas.showTotalDiagram(), 0);

        return () => {
            // Clean initialization 
            PubSub.unsubscribe('diagram');
            document.removeEventListener("contextmenu", contextMenuHandler);
            canvasRef.current.delete()
        }
    }, [setCanUndo, setCanRedo])


    const { canvas, figure, x, y } = contextMenu ?? {};

    return (
        <>
            <div id="diagram">
                <div id="canvas" style={{
                    width: width, height: height, maxWidth: width, maxHeight: height, position: 'absolute',
                    overflow: 'scroll'
                }}>
                </div>
            </div>

            <CanvasMenu canvas={canvas} onClose={setContextMenu} x={x} y={y} />
            <FigureMenu figure={figure} onClose={setContextMenu} x={x} y={y} />
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

        let figure = canvas.tryGetFigure(event.clientX, event.clientY)

        const userData = figure?.getUserData()
        if (userData?.type === groupType) {
            // Context menu in group node, treat as canvas 
            figure = null
        }

        setContextMenu({ canvas: figure == null ? canvas : null, figure: figure, x: event.clientX, y: event.clientY });
    }

    document.addEventListener("contextmenu", handleContextMenu);
    return handleContextMenu
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


function HandleToolbarCommands(canvas) {
    PubSub.subscribe('diagram.AddNode', canvas.commandAddNode)
    PubSub.subscribe('diagram.AddUserNode', canvas.commandAddUserNode)
    PubSub.subscribe('diagram.AddExternalNode', canvas.commandAddExternalNode)
    PubSub.subscribe('diagram.Undo', canvas.commandUndo)
    PubSub.subscribe('diagram.Redo', canvas.commandRedo)
    PubSub.subscribe('diagram.ShowTotalDiagram', canvas.showTotalDiagram)
    PubSub.subscribe('diagram.NewDiagram', canvas.commandNewDiagram)
    PubSub.subscribe('diagram.Export', () => exportDiagram(canvas))
    PubSub.subscribe('diagram.ShowInnerDiagram', canvas.commandShowInnerDiagram)
    PubSub.subscribe('diagram.CloseInnerDiagram', canvas.commandCloseInnerDiagram)
    PubSub.subscribe('diagram.EditInnerDiagram', canvas.commandEditInnerDiagram)

}
