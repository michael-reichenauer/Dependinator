import React, { Component } from "react";
import "import-jquery";
import "jquery-ui-bundle";
import "jquery-ui-bundle/jquery-ui.css";
import draw2d from "draw2d";
import { WheelZoomPolicy } from "./WheelZoomPolicy"
import { PanPolicyReadOnly } from "./PanPolicyReadOnly"
import { PanPolicyEdit } from "./PanPolicyEdit"
import { ConnectionCreatePolicy } from "./ConnectionCreatePolicy"
import { Menu, MenuItem } from "@material-ui/core";
import { random } from '../../common/utils'
import { addNode, addUserNode, addExternalNode } from './standardFigures'

const initialState = {
    contextMenu: null,
};

class Canvas extends Component {
    canvas = null;
    panPolicyCurrent = null
    panPolicyOther = null;
    canvasWidth = 0;
    canvasHeight = 0;
    hasRendered = false;

    constructor(props) {
        super(props);
        this.state = initialState;
        props.commands.addNode = this.addNode
        props.commands.addUserNode = this.addUserNode
        props.commands.addExternalNode = this.addExternalNode
        props.commands.undo = this.undo
        props.commands.redo = this.redo
    }

    undo = () => {
        this.canvas.getCommandStack().undo();
    }

    redo = () => {
        this.canvas.getCommandStack().redo();
    }

    addNode = () => {
        addNode(this.canvas, this.randomCenterPoint());
    }

    addUserNode = () => {
        addUserNode(this.canvas, this.randomCenterPoint());
    }

    addExternalNode = () => {
        addExternalNode(this.canvas, this.randomCenterPoint());
    }

    addDefaultItem = (x, y, shiftKey, ctrlKey) => {
        addNode(this.canvas, { x: x, y: y })
    }

    handleMenuAddNode = () => {
        const { x, y } = this.handleCloseContextMenu()
        addNode(this.canvas, this.toCanvasCoordinate(x, y))
    }

    handleMenuAddUserNode = () => {
        const { x, y } = this.handleCloseContextMenu()
        addUserNode(this.canvas, this.toCanvasCoordinate(x, y))
    }

    handleMenuAddExternalNode = () => {
        const { x, y } = this.handleCloseContextMenu()
        addExternalNode(this.canvas, this.toCanvasCoordinate(x, y))
    }


    componentDidMount = () => {
        this.createCanvas();
        document.addEventListener("contextmenu", this.handleContextMenu);
    }

    componentWillUnmount = () => {
        document.removeEventListener("contextmenu", this.handleContextMenu);
        this.canvas.destroy()
    }

    handleContextMenu = (event) => {
        if (!event.path.some((i) => i.id === "canvas")) {
            // Not a right click within the canvas 
            return
        }
        event.preventDefault();
        event = getEvent(event)

        let figure = this.tryGetFigure(event.clientX, event.clientY)
        this.setState({ contextMenu: { figure: figure, x: event.clientX, y: event.clientY } });
    };

    handleCloseContextMenu = () => {
        const { x, y } = this.state.contextMenu
        this.setState({ contextMenu: null });
        return { x, y }
    };

    togglePanPolicy = (figure) => {
        let current = this.panPolicyCurrent
        this.canvas.uninstallEditPolicy(this.panPolicyCurrent)

        this.panPolicyCurrent = this.panPolicyOther
        this.panPolicyOther = current
        this.canvas.installEditPolicy(this.panPolicyCurrent)

        if (figure != null) {
            this.canvas.setCurrentSelection(figure)
        }
    }

    createCanvas = () => {
        this.canvas = new draw2d.Canvas("canvas")
        let canvas = this.canvas
        canvas.setScrollArea("#canvas")

        // Pan policy readonly/edit
        this.panPolicyCurrent = new PanPolicyReadOnly(this.togglePanPolicy, this.addDefaultItem)
        this.panPolicyOther = new PanPolicyEdit(this.togglePanPolicy, this.addDefaultItem)
        canvas.installEditPolicy(this.panPolicyCurrent)

        canvas.installEditPolicy(new WheelZoomPolicy());
        canvas.installEditPolicy(new ConnectionCreatePolicy())

        canvas.getCommandStack().addEventListener(function (e) {
            if (e.isPostChangeEvent()) {
                console.log('event:', e)
            }
        });

        // let cf = new draw2d.shape.composite.Jailhouse({ width: 200, height: 200, bgColor: 'none' })
        // canvas.add(cf, 400, 400);

        // let f = new draw2d.shape.node.Between({ width: 50, height: 50 });
        // canvas.add(f, 450, 450);
        // f.getPorts().each((i, port) => { port.setVisible(false) })
        // cf.assignFigure(f)

        // let f2 = new draw2d.shape.node.Between({ width: 50, height: 50 });
        // canvas.add(f2, 200, 200);
        // f2.getPorts().each((i, port) => { port.setVisible(false) })

        // let f3 = new draw2d.shape.node.Between({ width: 50, height: 50 });
        // canvas.add(f3, 100, 100);
        // f3.getPorts().each((i, port) => { port.setVisible(false) })

        // var c = new draw2d.Connection({
        //     source: f3.getOutputPort(0),
        //     target: f2.getInputPort(0)
        // });

        // canvas.add(c);
    }

    render = () => {
        console.log('render')
        const { contextMenu } = this.state;

        let w = this.props.width
        let h = this.props.height
        if (this.hasRendered && (this.canvasWidth !== w || this.canvasHeight !== h)) {
            setTimeout(() => {
                this.canvas.setDimension(new draw2d.geo.Rectangle(0, 0, w, h));
            }, 0);
        }
        this.hasRendered = true
        this.canvasWidth = w;
        this.canvasHeight = h;

        return (
            <>
                <div id="canvas"
                    style={{ width: w, height: h, position: 'absolute', overflow: 'scroll', maxWidth: w, maxHeight: h }}></div>
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
                    {contextMenu !== null && contextMenu.figure !== null && <MenuItem onClick={this.handleCloseContextMenu}>Figure</MenuItem>}
                    {contextMenu !== null && contextMenu.figure === null && <MenuItem onClick={this.handleMenuAddNode}>Add Node</MenuItem>}
                    {contextMenu !== null && contextMenu.figure === null && <MenuItem onClick={this.handleMenuAddUserNode}>Add User Node</MenuItem>}
                    {contextMenu !== null && contextMenu.figure === null && <MenuItem onClick={this.handleMenuAddExternalNode}>Add External Node</MenuItem>}

                </Menu>
            </>
        );
    }

    tryGetFigure = (x, y) => {
        let cp = this.toCanvasCoordinate(x, y)
        let figure = this.canvas.getBestFigure(cp.x, cp.y)
        console.log('Figure:', figure)
        return figure
    }

    toCanvasCoordinate = (x, y) => {
        return this.canvas.fromDocumentToCanvasCoordinate(x, y)
    }

    randomCenterPoint = () => {
        let x = this.canvasWidth / 2 + random(-10, 10)
        let y = this.canvasHeight / 2 + random(-10, 10)
        return { x: x, y: y }
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



export default Canvas;