import React, { Component } from "react";
import "import-jquery";
import "jquery-ui-bundle";
import "jquery-ui-bundle/jquery-ui.css";
import draw2d from "draw2d";
import { WheelZoomPolicy } from "./WheelZoomPolicy"
import { PanReadOnlyPolicy } from "./PanReadOnlyPolicy"
import { PanEditPolicy } from "./PanEditPolicy"
import { ConnectionCreatePolicy } from "./ConnectionCreatePolicy"
import { Menu, MenuItem } from "@material-ui/core";
import { random } from '../../common/utils'
import { createDefaultNode, createDefaultUserNode, createDefaultExternalNode } from './figures'
import { serializeCanvas, deserializeCanvas } from './serialization'
import { canvasDivBackground } from "./colors";

const diagramName = 'diagram'
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
        props.commands.undo = this.undo
        props.commands.redo = this.redo
        props.commands.addNode = this.commandAddNode
        props.commands.addUserNode = this.commandAddUserNode
        props.commands.addExternalNode = this.commandAddExternalNode
        props.commands.clear = this.commandClearCanvas
    }

    undo = () => this.canvas.getCommandStack().undo();
    redo = () => this.canvas.getCommandStack().redo();

    commandAddNode = () => this.addFigure(createDefaultNode(), this.randomCenterPoint())
    commandAddUserNode = () => this.addFigure(createDefaultUserNode(), this.randomCenterPoint())
    commandAddExternalNode = () => this.addFigure(createDefaultExternalNode(), this.randomCenterPoint())
    commandClearCanvas = () => this.canvas.clear()
    handleMenuAddNode = () => this.handleMenuAdd(createDefaultNode())
    handleMenuAddUserNode = () => this.handleMenuAdd(createDefaultUserNode())
    handleMenuAddExternalNode = () => this.handleMenuAdd(createDefaultExternalNode())
    addDefaultItem = (x, y, shiftKey, ctrlKey) => this.addFigure(createDefaultNode(), { x: x, y: y })

    handleMenuAdd = (figure) => {
        const { x, y } = this.handleCloseContextMenu()
        this.addFigure(figure, this.toCanvasCoordinate(x, y))
    }


    addFigure = (figure, p) => {
        addFigureToCanvas(this.canvas, figure, p)
        this.enableEditMode()
    }

    componentDidMount = () => {
        console.log('componentDidMount')
        this.createCanvas();
        document.addEventListener("contextmenu", this.handleContextMenu);
    }

    componentWillUnmount = () => {
        console.log('componentWillUnmount')
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

        restoreDiagram(canvas)
        updateCanvasMaxFigureSize(canvas)

        // Pan policy readonly/edit
        this.panPolicyCurrent = new PanReadOnlyPolicy(this.togglePanPolicy, this.addDefaultItem)
        this.panPolicyOther = new PanEditPolicy(this.togglePanPolicy, this.addDefaultItem)
        canvas.installEditPolicy(this.panPolicyCurrent)

        canvas.installEditPolicy(new WheelZoomPolicy());
        canvas.installEditPolicy(new ConnectionCreatePolicy())
        canvas.installEditPolicy(new draw2d.policy.canvas.CoronaDecorationPolicy());
        // canvas.installEditPolicy(new draw2d.policy.canvas.ShowGridEditPolicy());

        canvas.canvasWidth = this.props.width
        canvas.canvasHeight = this.props.height

        canvas.getCommandStack().addEventListener(function (e) {
            // console.log('event:', e)
            if (e.isPostChangeEvent()) {
                // console.log('event isPostChangeEvent:', e)
                if (e.action === "POST_EXECUTE") {
                    updateCanvasMaxFigureSize(canvas)
                    saveDiagram(canvas)
                }
            }
        });
    }

    render = () => {
        const { contextMenu } = this.state;

        let w = this.props.width
        let h = this.props.height
        console.log('render', w, h, this.canvas?.getZoom(),)
        if (this.hasRendered && (this.canvasWidth !== w || this.canvasHeight !== h)) {
            setTimeout(() => {
                this.canvas.canvasWidth = w;
                this.canvas.canvasHeight = h;
                let cw = Math.max(w, this.canvas.maxFigureWidth)
                let ch = Math.max(h, this.canvas.maxFigureHeight)
                // let cw = w
                // let ch = h
                let zoom = this.canvas.getZoom()
                //console.log('resize ', cw, ch, zoom)
                this.canvas.setDimension(new draw2d.geo.Rectangle(0, 0, cw, ch));
                // Must adjust region constraint since setDimension does not do that
                this.canvas.regionDragDropConstraint.constRect = new draw2d.geo.Rectangle(0, 0, cw, ch)
                this.canvas.paper.setViewBox(0, 0, cw, ch)

                this.canvas.html
                    .find("svg")
                    .attr({
                        'width': cw / zoom,
                        'height': ch / zoom,
                    })
            }, 0);
        }
        this.hasRendered = true
        this.canvasWidth = w;
        this.canvasHeight = h;

        return (
            <>
                <div id="canvas"
                    style={{
                        width: w, height: h, maxWidth: w, maxHeight: h, position: 'absolute',
                        overflow: 'scroll', background: canvasDivBackground
                    }}></div>
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

    enableEditMode = () => {
        if (!this.canvas.isReadOnlyMode) {
            return
        }
        this.togglePanPolicy()
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

const updateCanvasMaxFigureSize = (canvas) => {
    let w = 0
    let h = 0

    canvas.getFigures().each((i, f) => {
        let fw = f.getAbsoluteX() + f.getWidth()
        let fh = f.getAbsoluteY() + f.getHeight()

        if (i === 0) {
            w = fw
            h = fh
            return
        }

        if (fw > w) {
            w = fw
        }
        if (fh > h) {
            h = fh
        }
    })

    canvas.maxFigureWidth = w
    canvas.maxFigureHeight = h
    // console.log('figure size', w, h)
}

const addFigureToCanvas = (canvas, figure, p) => {
    hidePortsIfReadOnly(canvas, figure)

    const command = new draw2d.command.CommandAdd(canvas, figure, p.x - figure.width / 2, p.y - figure.height / 2);
    canvas.getCommandStack().execute(command);
}


const hidePortsIfReadOnly = (canvas, figure) => {
    if (canvas.isReadOnlyMode) {
        figure.getPorts().each((i, port) => { port.setVisible(false) })
    }
}


const saveDiagram = (canvas) => {
    // Serialize canvas figures and connections into canvas data object
    const canvasData = serializeCanvas(canvas);


    // Store canvas data in local storage
    const canvasText = JSON.stringify(canvasData)
    localStorage.setItem(diagramName, canvasText)
    // console.log('save', canvasText)
}

const restoreDiagram = (canvas) => {
    // Get canvas data from local storage.
    let canvasText = localStorage.getItem(diagramName)
    //console.log('load', canvasText)

    if (canvasText == null) {
        return
    }
    const canvasData = JSON.parse(canvasText)
    if (canvasData == null || canvasData.figures == null) {
        return
    }

    // Deserialize canvas
    deserializeCanvas(canvas, canvasData)
}

export default Canvas;