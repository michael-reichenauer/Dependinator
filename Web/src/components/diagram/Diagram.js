import React, { Component, useEffect, useRef } from "react";
import "import-jquery";
import "jquery-ui-bundle";
import "jquery-ui-bundle/jquery-ui.css";
import PubSub from 'pubsub-js'
import Canvas from "./Canvas"
import CanvasMenu from "./CanvasMenu";
import FigureMenu from "./FigureMenu";

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


    handleContextMenu = (event) => {
        if (!event.path.some((i) => i.id === "canvas")) {
            // Not a right click within the canvas 
            return
        }
        event.preventDefault();
        event = getEvent(event)

        let figure = this.canvasw.tryGetFigure(event.clientX, event.clientY)

        this.setState({
            contextMenu: {
                isCanvas: figure === null,
                figure: figure,
                x: event.clientX,
                y: event.clientY
            }
        });
    };


    handleCloseContextMenu = () => {
        this.setState({ contextMenu: null });
    };


    render = () => {
        console.log('Render')
        const { isCanvas, x, y, figure } = this.state.contextMenu ?? {};

        let width = this.props.width
        let height = this.props.height

        if (this.canvasw != null) {
            this.canvasw.canvas.canvasWidth = width;
            this.canvasw.canvas.canvasHeight = height
        }

        return (
            <>
                <div id="canvas" style={{
                    width: width, height: height, maxWidth: width, maxHeight: height, position: 'absolute',
                    overflow: 'scroll', background: '#D5DBDB'
                }}>
                </div>

                <CanvasMenu canvas={this.canvasw} isCanvas={isCanvas} onClose={this.handleCloseContextMenu} x={x} y={y} />
                <FigureMenu figure={figure} onClose={this.handleCloseContextMenu} x={x} y={y} />
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
