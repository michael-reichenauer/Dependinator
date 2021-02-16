import React, { Component, useEffect, useRef } from "react";
import "import-jquery";
import "jquery-ui-bundle";
import "jquery-ui-bundle/jquery-ui.css";
import PubSub from 'pubsub-js'
import { Menu, MenuItem } from "@material-ui/core";
import { CommandChangeColor } from "./commandChangeColor";
import { nodeColorNames } from "./colors";
import Canvas from "./Canvas"
import { createDefaultExternalNode, createDefaultNode, createDefaultUserNode, zoomAndMoveShowTotalDiagram } from "./figures";

const initialState = {
    contextMenu: null,
};


export default class Diagram extends Component {
    canvasw = null;


    constructor(props) {
        super(props);
        this.state = initialState;
    }

    componentDidMount = () => {
        console.log('componentDidMount')
        this.canvasw = new Canvas('canvas', this.props.width, this.props.height);

        this.canvasw.showTotalDiagram()

        document.addEventListener("contextmenu", this.handleContextMenu);
        PubSub.subscribe('diagram.AddNode', this.canvasw.commandAddNode)
        PubSub.subscribe('diagram.AddUserNode', this.canvasw.commandAddUserNode)
        PubSub.subscribe('diagram.AddExternalNode', this.canvasw.commandAddExternalNode)
        PubSub.subscribe('diagram.Undo', this.canvasw.commandUndo)
        PubSub.subscribe('diagram.Redo', this.canvasw.commandRedo)
        PubSub.subscribe('diagram.ShowTotalDiagram', this.canvasw.showTotalDiagram)
        PubSub.subscribe('diagram.NewDiagram', this.canvasw.commandNewDiagram)
    }

    componentWillUnmount = () => {
        console.log('componentWillUnmount')
        PubSub.unsubscribe('diagram');
        document.removeEventListener("contextmenu", this.handleContextMenu);
        this.canvasw.delete()
    }


    handleMenuAddNode = () => this.handleMenuAdd(createDefaultNode())
    handleMenuAddUserNode = () => this.handleMenuAdd(createDefaultUserNode())
    handleMenuAddExternalNode = () => this.handleMenuAdd(createDefaultExternalNode())
    handleMenuAdd = (figure) => {
        const { x, y } = this.handleCloseContextMenu()
        this.canvasw.addFigure(figure, this.canvasw.toCanvasCoordinate(x, y))
    }

    handleContextMenu = (event) => {
        if (!event.path.some((i) => i.id === "canvas")) {
            // Not a right click within the canvas 
            return
        }
        event.preventDefault();
        event = getEvent(event)

        let figure = this.canvasw.tryGetFigure(event.clientX, event.clientY)

        this.setState({ contextMenu: { figure: figure, x: event.clientX, y: event.clientY } });
    };


    handleCloseContextMenu = () => {
        const { x, y } = this.state.contextMenu
        this.setState({ contextMenu: null });
        return { x, y }
    };


    render = () => {
        console.log('Render')
        const { contextMenu } = this.state;

        let width = this.props.width
        let height = this.props.height
        //console.log('render', w, h, this.canvas?.getZoom(),)

        if (this.canvasw != null) {
            this.canvasw.canvas.canvasWidth = width;
            this.canvasw.canvas.canvasHeight = height
        }

        const isCanvas = contextMenu !== null && contextMenu.figure === null
        const isFigure = contextMenu !== null && contextMenu.figure !== null

        return (
            <>
                <div id="canvas" style={{
                    width: width, height: height, maxWidth: width, maxHeight: height, position: 'absolute',
                    overflow: 'scroll', background: '#D5DBDB'
                }}>
                </div>

                <Menu
                    keepMounted
                    open={contextMenu !== null}
                    onClose={this.handleCloseContextMenu}
                    anchorReference="anchorPosition"
                    anchorPosition={
                        contextMenu !== null
                            ? { left: contextMenu.x - 2, top: contextMenu.y - 4 }
                            : undefined
                    }
                >
                    {isFigure && figureMenu(contextMenu.figure, this.handleCloseContextMenu)}

                    {isCanvas && <MenuItem onClick={this.handleMenuAddNode}>Add Node</MenuItem>}
                    {isCanvas && <MenuItem onClick={this.handleMenuAddUserNode}>Add User Node</MenuItem>}
                    {isCanvas && <MenuItem onClick={this.handleMenuAddExternalNode}>Add External Node</MenuItem>}

                </Menu>
            </>
        );
    }
}

function getEvent(event) {
    // check for iPad, Android touch events
    if (typeof event.originalEvent !== "undefined") {
        if (event.originalEvent.touches && event.originalEvent.touches.length) {
            return event.originalEvent.touches[0]
        } else if (event.originalEvent.changedTouches && event.originalEvent.changedTouches.length) {
            return event.originalEvent.changedTouches[0]
        }
    }
    return event
}

const figureMenu = (figure, closeMenu) => {
    const setColor = (figure, colorName) => {
        closeMenu()
        const command = new CommandChangeColor(figure, colorName);
        figure.getCanvas().getCommandStack().execute(command);
    }

    return nodeColorNames().map((item) => (
        <MenuItem onClick={() => setColor(figure, item)} key={`item-${item}`}>{item}</MenuItem>
    ))
}



// export const Diagram = ({ width, height }) => {

//     const canvasRef = useRef(null)


//     useEffect(() => {
//         const handleContextMenu = (event) => {
//             if (!event.path.some((i) => i.id === "canvas")) {
//                 // Not a right click within the canvas 
//                 return
//             }
//             event.preventDefault();
//             event = getEvent(event)

//             console.log('handleContextMenu', event)
//             console.log('canvasref', canvasRef.current)
//             const canvas = canvasRef.current
//             let figure = canvas.canvasw.tryGetFigure(event.clientX, event.clientY)

//             canvas.setState({ contextMenu: { figure: figure, x: event.clientX, y: event.clientY } });
//         }

//         console.log('Diagram use effect')
//         document.addEventListener("contextmenu", handleContextMenu);

//         return () => {
//             // Clean 
//             console.log('Diagram clean use effect')
//             document.removeEventListener("contextmenu", handleContextMenu);
//         }
//     }, [])

//     return (
//         <>
//             <Canvas canvasRef={canvasRef} width={width} height={height} />
//         </>
//     )
// }
