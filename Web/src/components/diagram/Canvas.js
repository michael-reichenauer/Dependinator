import React, { Component } from "react";
import PubSub from 'pubsub-js'
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
import { createDefaultNode, createDefaultUserNode, createDefaultExternalNode, createDefaultSystemNode, zoomAndMoveShowTotalDiagram, updateCanvasMaxFigureSize } from './figures'
import { serializeCanvas, deserializeCanvas } from './serialization'
import { canvasDivBackground, nodeColorNames } from "./colors";
import { CommandChangeColor } from "./commandChangeColor";
import { createDefaultConnection } from "./connections";

import { atom } from 'jotai'

export const canUndo = atom(false)
export const canRedo = atom(false)

const diagramName = 'diagram'
const initialState = {
    contextMenu: null,
};



class Canvas extends Component {
    canvas = null;


    constructor(props) {
        super(props);
        this.state = initialState;
    }

    showTotalDiagram = () => {
        zoomAndMoveShowTotalDiagram(this.canvas)
    }

    commandUndo = () => {
        this.canvas.getCommandStack().undo();
        saveDiagram(this.canvas)
    }

    commandRedo = () => {
        this.canvas.getCommandStack().redo();
        saveDiagram(this.canvas)
    }

    commandAddNode = () => this.addFigure(createDefaultNode(), this.randomCenterPoint())
    commandAddUserNode = () => this.addFigure(createDefaultUserNode(), this.randomCenterPoint())
    commandAddExternalNode = () => this.addFigure(createDefaultExternalNode(), this.randomCenterPoint())
    handleMenuAddNode = () => this.handleMenuAdd(createDefaultNode())
    handleMenuAddUserNode = () => this.handleMenuAdd(createDefaultUserNode())
    handleMenuAddExternalNode = () => this.handleMenuAdd(createDefaultExternalNode())
    addDefaultItem = (x, y, shiftKey, ctrlKey) => this.addFigure(createDefaultNode(), { x: x, y: y })

    handleMenuAdd = (figure) => {
        const { x, y } = this.handleCloseContextMenu()
        this.addFigure(figure, this.toCanvasCoordinate(x, y))
    }

    commandNewDiagram = () => {
        this.clearDiagram()
        addDefaultNewDiagram(this.canvas)

    }

    addFigure = (figure, p) => {
        addFigureToCanvas(this.canvas, figure, p)
        this.enableEditMode()
    }

    componentDidMount = () => {
        console.log('componentDidMount')
        this.canvas = createCanvas('canvas', this.togglePanPolicy, this.addDefaultItem);
        this.canvas.canvasWidth = this.props.width
        this.canvas.canvasHeight = this.props.height

        zoomAndMoveShowTotalDiagram(this.canvas)

        document.addEventListener("contextmenu", this.handleContextMenu);
        PubSub.subscribe('diagram.AddNode', this.commandAddNode)
        PubSub.subscribe('diagram.AddUserNode', this.commandAddUserNode)
        PubSub.subscribe('diagram.AddExternalNode', this.commandAddExternalNode)
        PubSub.subscribe('diagram.Undo', this.commandUndo)
        PubSub.subscribe('diagram.Redo', this.commandRedo)
        PubSub.subscribe('diagram.ShowTotalDiagram', this.showTotalDiagram)
        PubSub.subscribe('diagram.NewDiagram', this.commandNewDiagram)
    }

    componentWillUnmount = () => {
        console.log('componentWillUnmount')
        PubSub.unsubscribe('diagram');
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
        let current = this.canvas.panPolicyCurrent
        this.canvas.uninstallEditPolicy(this.canvas.panPolicyCurrent)

        this.canvas.panPolicyCurrent = this.canvas.panPolicyOther
        this.canvas.panPolicyOther = current
        this.canvas.installEditPolicy(this.canvas.panPolicyCurrent)

        if (figure != null) {
            this.canvas.setCurrentSelection(figure)
        }
    }



    render = () => {
        const { contextMenu } = this.state;

        let width = this.props.width
        let height = this.props.height
        //console.log('render', w, h, this.canvas?.getZoom(),)

        if (this.canvas != null) {
            this.canvas.canvasWidth = width;
            this.canvas.canvasHeight = height
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


    tryGetFigure = (x, y) => {
        let cp = this.toCanvasCoordinate(x, y)
        let figure = this.canvas.getBestFigure(cp.x, cp.y)
        return figure
    }

    toCanvasCoordinate = (x, y) => {
        return this.canvas.fromDocumentToCanvasCoordinate(x, y)
    }


    randomCenterPoint = () => {
        let x = (this.canvas.canvasWidth / 2 + random(-10, 10) + this.canvas.getScrollLeft()) * this.canvas.getZoom()
        let y = (this.canvas.canvasHeight / 2 + random(-10, 10) + this.canvas.getScrollTop()) * this.canvas.getZoom()

        return { x: x, y: y }
    }


    enableEditMode = () => {
        if (!this.canvas.isReadOnlyMode) {
            return
        }
        this.togglePanPolicy()
    }


    clearDiagram = () => {
        const canvas = this.canvas
        canvas.lines.clone().each(function (i, e) {
            canvas.remove(e)
        })

        canvas.figures.clone().each(function (i, e) {
            canvas.remove(e)
        })


        canvas.selection.clear()
        canvas.currentDropTarget = null
        canvas.figures = new draw2d.util.ArrayList()
        canvas.lines = new draw2d.util.ArrayList()
        canvas.commonPorts = new draw2d.util.ArrayList()
        canvas.commandStack.markSaveLocation()
        canvas.linesToRepaintAfterDragDrop = new draw2d.util.ArrayList()
        canvas.lineIntersections = new draw2d.util.ArrayList()
    }
}

const createCanvas = (canvasId, togglePanPolicy, addDefaultItem) => {
    const canvas = new draw2d.Canvas(canvasId)
    canvas.setScrollArea("#" + canvasId)
    canvas.setDimension(new draw2d.geo.Rectangle(0, 0, 10000, 10000))
    canvas.regionDragDropConstraint.constRect = new draw2d.geo.Rectangle(0, 0, 10000, 10000)

    restoreDiagram(canvas)
    updateCanvasMaxFigureSize(canvas)

    // Pan policy readonly/edit
    canvas.panPolicyCurrent = new PanReadOnlyPolicy(togglePanPolicy, addDefaultItem)
    canvas.panPolicyOther = new PanEditPolicy(togglePanPolicy, addDefaultItem)
    canvas.installEditPolicy(canvas.panPolicyCurrent)

    canvas.installEditPolicy(new WheelZoomPolicy());
    canvas.installEditPolicy(new ConnectionCreatePolicy())
    canvas.installEditPolicy(new draw2d.policy.canvas.CoronaDecorationPolicy());
    const sg = new draw2d.policy.canvas.ShowGridEditPolicy(1, 1, canvasDivBackground)
    sg.onZoomCallback = () => { }
    canvas.installEditPolicy(sg);
    canvas.installEditPolicy(new draw2d.policy.canvas.SnapToGeometryEditPolicy())
    canvas.installEditPolicy(new draw2d.policy.canvas.SnapToInBetweenEditPolicy())
    canvas.installEditPolicy(new draw2d.policy.canvas.SnapToCenterEditPolicy())
    canvas.installEditPolicy(new draw2d.policy.canvas.SnapToGridEditPolicy(10, false))


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
    return canvas
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


const addFigureToCanvas = (canvas, figure, p) => {
    hidePortsIfReadOnly(canvas, figure)

    const command = new draw2d.command.CommandAdd(canvas, figure, p.x - figure.width / 2, p.y - figure.height / 2);
    canvas.getCommandStack().execute(command);
}

const addConnectionToCanvas = (canvas, connection) => {
    const command = new draw2d.command.CommandAdd(canvas, connection, 0, 0);
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
        console.log('no diagram')
        addDefaultNewDiagram(canvas)
        return
    }
    //console.log('saved', canvasText)
    const canvasData = JSON.parse(canvasText)
    if (canvasData == null || canvasData.figures == null || canvasData.figures.lengths === 0) {
        return
    }

    // Deserialize canvas
    deserializeCanvas(canvas, canvasData)
}

const addDefaultNewDiagram = (canvas) => {
    const user = createDefaultUserNode()
    const system = createDefaultSystemNode()
    const external = createDefaultExternalNode()
    addFigureToCanvas(canvas, user, { x: 5200, y: 5400 })
    addFigureToCanvas(canvas, system, { x: 5600, y: 5400 })
    addConnectionToCanvas(canvas, createDefaultConnection(user, 'output0', system, 'input0'))
    addFigureToCanvas(canvas, external, { x: 6000, y: 5400 })
    addConnectionToCanvas(canvas, createDefaultConnection(system, 'output0', external, 'input0'))

    zoomAndMoveShowTotalDiagram(canvas)
}

export default Canvas;