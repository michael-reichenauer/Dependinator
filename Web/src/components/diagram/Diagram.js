import React, { useEffect, useRef, useState } from "react";
import "import-jquery";
import "jquery-ui-bundle";
import "jquery-ui-bundle/jquery-ui.css";
import PubSub from 'pubsub-js'
import Canvas from "./Canvas"
import CanvasMenu from "./CanvasMenu";
import FigureMenu from "./FigureMenu";
import { getCommonEvent } from "../../common/events";


export default function Diagram({ width, height }) {
    const canvasRef = useRef(null)
    const [contextMenu, setContextMenu] = useState()

    useEffect(() => {
        // Initialize canvas
        const canvas = new Canvas('canvas', width, height);
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
        // width height only used at initialization
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [])


    const onCloseMenu = () => setContextMenu(null);
    const { isCanvas, x, y, figure } = contextMenu ?? {};

    const canvas = canvasRef.current
    if (canvas != null) {
        canvas.canvasWidth = width;
        canvas.canvasHeight = height
    }

    return (
        <>
            <div id="canvas" style={{
                width: width, height: height, maxWidth: width, maxHeight: height, position: 'absolute',
                overflow: 'scroll', background: '#D5DBDB'
            }}>
            </div>

            <CanvasMenu canvas={canvas} isCanvas={isCanvas} onClose={onCloseMenu} x={x} y={y} />
            <FigureMenu figure={figure} onClose={onCloseMenu} x={x} y={y} />
        </>
    )
}


function enableContextMenu(setContextMenu, canvas) {
    const handleContextMenu = (event) => {
        if (!event.path.some((i) => i.id === "canvas")) {
            // Not a right click within the canvas 
            return
        }
        event.preventDefault();
        event = getCommonEvent(event)

        let figure = canvas.tryGetFigure(event.clientX, event.clientY)
        setContextMenu({ isCanvas: figure === null, figure: figure, x: event.clientX, y: event.clientY });
    }

    document.addEventListener("contextmenu", handleContextMenu);
    return handleContextMenu
}


function HandleToolbarCommands(canvas) {
    PubSub.subscribe('diagram.AddNode', canvas.commandAddNode)
    PubSub.subscribe('diagram.AddUserNode', canvas.commandAddUserNode)
    PubSub.subscribe('diagram.AddExternalNode', canvas.commandAddExternalNode)
    PubSub.subscribe('diagram.Undo', canvas.commandUndo)
    PubSub.subscribe('diagram.Redo', canvas.commandRedo)
    PubSub.subscribe('diagram.ShowTotalDiagram', canvas.showTotalDiagram)
    PubSub.subscribe('diagram.NewDiagram', canvas.commandNewDiagram)
}
