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

const initialState = {
    showContextMenu: false,
    figure: null,
    contextMenuX: null,
    contextMenuY: null,
};

class Canvas extends Component {
    canvas = null;
    panPolicyCurrent = null
    panPolicyOther = null;
    canvasWidth = 0;
    canvasHeigh = 0;
    hasRendered = false;

    constructor(props) {
        super(props);
        this.state = initialState;
    }

    componentDidMount() {
        this.createCanvas();
        document.addEventListener("contextmenu", this.handleContextMenu);
    }

    componentWillUnmount() {
        document.removeEventListener("contextmenu", this.handleContextMenu);
        this.canvas.destroy()
    }

    handleContextMenu = (event) => {
        if (!event.path.some((i) => i.id === "canvas")) {
            // Not within the canvas 
            return
        }
        event.preventDefault();

        // Try get figure for context menu
        event = getEvent(event)
        let point = this.canvas.fromDocumentToCanvasCoordinate(event.clientX, event.clientY)
        let figure = this.canvas.getBestFigure(point.x, point.y)
        console.log('Figure:', figure)

        // Show menu
        this.setState({
            showContextMenu: true,
            figure: figure,
            contextMenuX: event.clientX,
            contextMenuY: event.clientY,
        });
    };

    handleCloseContextMenu = () => {
        this.setState({ showContextMenu: false });
    };

    togglePanPolicy = () => {
        let current = this.panPolicyCurrent
        this.canvas.uninstallEditPolicy(this.panPolicyCurrent)

        this.panPolicyCurrent = this.panPolicyOther
        this.panPolicyOther = current
        this.canvas.installEditPolicy(this.panPolicyCurrent)
    }

    addDefaultItem = (x, y, shiftKey, ctrlKey) => {
        let figure = new draw2d.shape.node.Between({ width: 50, height: 50 });
        this.canvas.add(figure, x - 25, y - 25);
        return figure
    }

    handleAddNode = (event) => {
        this.handleCloseContextMenu()
        console.log('e', event)
        let point = this.canvas.fromDocumentToCanvasCoordinate(this.state.contextMenuX, this.state.contextMenuY)
        let figure = new draw2d.shape.node.Between({ width: 50, height: 50 });
        this.canvas.add(figure, point.x - 25, point.y - 25);
    }

    createCanvas() {

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

    render() {
        console.log('render')
        const { showContextMenu, figure, contextMenuX, contextMenuY } = this.state;

        let w = this.props.width
        let h = this.props.height
        if (this.hasRendered && (this.canvasWidth !== w || this.canvasHeigh !== h)) {
            setTimeout(() => {
                this.canvas.setDimension(new draw2d.geo.Rectangle(0, 0, w, h));
            }, 0);
        }
        this.hasRendered = true
        this.canvasWidth = w;
        this.canvasHeigh = h;


        return (
            <>
                <div id="canvas"
                    style={{ width: w, height: h, position: 'absolute', overflow: 'scroll', maxWidth: w, maxHeight: h }}></div>
                <Menu
                    keepMounted
                    open={showContextMenu}
                    onClose={this.handleCloseContextMenu}
                    anchorReference="anchorPosition"
                    anchorPosition={
                        contextMenuX !== null && contextMenuY !== null
                            ? { left: contextMenuX - 2, top: contextMenuY - 4 }
                            : undefined
                    }
                >
                    {figure !== null && <MenuItem onClick={this.handleCloseContextMenu}>Figure</MenuItem>}
                    {figure === null && <MenuItem onClick={this.handleAddNode}>Add Node</MenuItem>}

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



export default Canvas;