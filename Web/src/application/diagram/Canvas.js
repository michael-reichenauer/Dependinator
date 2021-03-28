import draw2d from "draw2d";
import PanPolicy from "./PanPolicy";
import ZoomPolicy from "./ZoomPolicy"
import KeyboardPolicy from "./KeyboardPolicy";
import ConnectionCreatePolicy from "./ConnectionCreatePolicy"
import Colors from "./Colors";
import { random } from "../../common/utils";
import CanvasSerializer from "./CanvasSerializer";


const randomDist = 30

export default class Canvas extends draw2d.Canvas {
    serializer = null

    diagramId = null
    canvasId = null
    mainNodeId = null

    touchStartTime = 0
    touchEndTime = 0
    pinchDiff = -1

    constructor(htmlElementId, onEditMode, width, height) {
        super(htmlElementId, width, height);

        this.serializer = new CanvasSerializer(this)

        this.setScrollArea("#" + htmlElementId)
        this.setDimension(new draw2d.geo.Rectangle(0, 0, width, height))

        // A likely bug in draw2d can be fixed with this hack
        this.regionDragDropConstraint.constRect = new draw2d.geo.Rectangle(0, 0, width, height)

        // Center the canvas
        const area = this.getScrollArea()
        area.scrollLeft(width / 2 - this.getWidth() / 2)
        area.scrollTop(height / 2 - this.getHeight() / 2)

        this.panPolicy = new PanPolicy(onEditMode)
        this.installEditPolicy(this.panPolicy)

        this.zoomPolicy = new ZoomPolicy()
        this.installEditPolicy(this.zoomPolicy);

        this.installEditPolicy(new ConnectionCreatePolicy())
        this.installEditPolicy(new draw2d.policy.canvas.CoronaDecorationPolicy());

        this.setNormalBackground()

        this.installEditPolicy(new draw2d.policy.canvas.SnapToGeometryEditPolicy())
        this.installEditPolicy(new draw2d.policy.canvas.SnapToInBetweenEditPolicy())
        this.installEditPolicy(new draw2d.policy.canvas.SnapToCenterEditPolicy())
        this.installEditPolicy(new KeyboardPolicy())

        this.enableTouchSupport()

        //canvas.installEditPolicy(new draw2d.policy.canvas.SnapToGridEditPolicy(10, false))  

    }

    serialize() {
        return this.serializer.serialize()
    }

    deserialize(canvasData) {
        this.serializer.deserialize(canvasData)
    }

    export(resultHandler) {
        const rect = this.getFiguresRect()
        this.serializer.export(rect, resultHandler)
    }

    clearDiagram = () => {
        const canvas = this
        canvas.lines.clone().each((i, e) => canvas.remove(e))
        canvas.figures.clone().each((i, e) => canvas.remove(e))
        canvas.selection.clear()
        canvas.currentDropTarget = null
        canvas.figures = new draw2d.util.ArrayList()
        canvas.lines = new draw2d.util.ArrayList()
        canvas.commonPorts = new draw2d.util.ArrayList()
        canvas.commandStack.markSaveLocation()
        canvas.linesToRepaintAfterDragDrop = new draw2d.util.ArrayList()
        canvas.lineIntersections = new draw2d.util.ArrayList()
        canvas.diagramId = null
        canvas.canvasId = null
    }


    runCmd(command) {
        this.getCommandStack().execute(command);
    }

    setNormalBackground() {
        this.html.find("svg").css({
            'background-color': Colors.canvasDivBackground,
            "background": Colors.canvasDivBackground,
            "background-size": 0
        })
    }

    setGridBackground() {
        // In edit mode, add a grid background.
        const bgColor = Colors.canvasDivBackground
        const gridColor = Colors.canvasGridRgb
        const interval = 10
        const gridStroke = 1

        let background =
            ` linear-gradient(to right,  ${gridColor} ${gridStroke}px, transparent ${gridStroke}px),
              linear-gradient(to bottom, ${gridColor} ${gridStroke}px, ${bgColor}  ${gridStroke}px)`
        let backgroundSize = `${interval}px ${interval}px`

        this.html.find("svg").css({
            "background": background,
            "background-size": backgroundSize
        })
    }

    addAtApproximately(figure, x, y) {
        if (null != this.getFigures().asArray().find(f => f.x === x && f.y === y)) {
            // Figure exists at that place, lets retry with other coordinate
            x = x + random(-randomDist, randomDist)
            y = y + random(-randomDist, randomDist)
            this.addAtApproximately(figure, x, y)
            return
        }

        // No other figure at this place, lets add
        this.add(figure, x, y)
    }


    addAll(figures) {
        //const t = timing()
        for (let i = 0; i < figures.length; i++) {
            let figure = figures[i]
            let x = figures[i].x
            let y = figures[i].y;

            if (figure.getCanvas() === this) {
                return;
            }

            if (figure instanceof draw2d.shape.basic.Line) {
                this.lines.add(figure);
            } else {
                this.figures.add(figure);
                if (typeof y !== "undefined") {
                    figure.setPosition(x, y);
                } else if (typeof x !== "undefined") {
                    figure.setPosition(x);
                }
            }


            figure.setCanvas(this);

            // to avoid drag&drop outside of this canvas
            figure.installEditPolicy(this.regionDragDropConstraint);

            // important initial call
            figure.getShapeElement();

            // fire the figure:add event before the "move" event and after the figure.repaint() call!
            //   - the move event can only be fired if the figure part of the canvas.
            //     and in this case the notification event should be fired to the listener before
            this.fireEvent("figure:add", { figure: figure, canvas: this });

            // fire the event that the figure is part of the canvas
            figure.fireEvent("added", { figure: figure, canvas: this });

            // ...now we can fire the initial move event
            figure.fireEvent("move", { figure: figure, dx: 0, dy: 0 });

            if (figure instanceof draw2d.shape.basic.PolyLine) {
                this.calculateConnectionIntersection();
            }
        }
        //t.log("Added all figures");

        this.figures.each(function (i, fig) {
            fig.repaint();
        });
        //t.log("Repainted figures");

        this.lines.each(function (i, line) {
            line.svgPathString = null;
            line.repaint();

        });
        //t.log();
        return this;
    }

    getFiguresRect() {
        const d = this.getDimension()
        let minX = d.getWidth()
        let minY = d.getHeight()
        let maxX = 0
        let maxY = 0

        this.getFigures().each((i, f) => {
            let fx = f.getAbsoluteX()
            let fy = f.getAbsoluteY()
            let fx2 = fx + f.getWidth()
            let fy2 = fy + f.getHeight()

            if (i === 0) {
                minX = fx
                minY = fy
                maxX = fx2
                maxY = fy2
                return
            }

            if (fx < minX) {
                minX = fx
            }
            if (fy < minY) {
                minY = fy
            }
            if (fx2 > maxX) {
                maxX = fx2
            }
            if (fy2 > maxY) {
                maxY = fy2
            }
        })

        return { x: minX, y: minY, w: maxX - minX, h: maxY - minY, x2: maxX, y2: maxY }
    }

    enableTouchSupport() {

        this.html.unbind("mousemove touchmove")

        this.html.bind("mousemove touchmove", (event) => {
            // console.log('orgevent', event)
            if (event.type === 'touchmove' && event.touches.length === 2) {
                // Pinch touch with two touches
                //console.log('orgevent', event)
                const t1 = event.touches[0]
                const t2 = event.touches[1]
                const curDiff = this.distance(t1.clientX, t1.clientY, t2.clientX, t2.clientY)

                if (this.pinchDiff === -1) {
                    this.pinchDiff = curDiff
                    return
                }

                let delta = 70
                if (curDiff > this.pinchDiff) {
                    // The distance between the two pointers has increased (zoom in)
                    delta = -delta
                } else if (curDiff < this.pinchDiff) {
                    // The distance between the two pointers has decreased (zoom out)
                }
                this.pinchDiff = curDiff

                const x = (t2.clientX - t1.clientX) / 2 + t1.clientX
                const y = (t2.clientY - t1.clientY) / 2 + t1.clientY

                let pos = this.fromDocumentToCanvasCoordinate(x, y)
                this.onMouseWheel(delta, pos.x, pos.y, false, false)
                return
            }


            event = this._getEvent(event)
            //console.log('event', event)
            let pos = this.fromDocumentToCanvasCoordinate(event.clientX, event.clientY)
            if (this.mouseDown === false) {
                // mouseEnter/mouseLeave events for Figures. Don't use the Raphael or DOM native functions.
                // Raphael didn't work for Rectangle with transparent fill (events only fired for the border line)
                // DOM didn't work well for lines. No eclipse area - you must hit the line exact to retrieve the event.
                // In this case I implement my own stuff...again and again.
                //
                // don't break the main event loop if one element fires an error during enter/leave event.
                try {
                    let hover = this.getBestFigure(pos.x, pos.y)
                    if (hover !== this.currentHoverFigure && this.currentHoverFigure !== null) {
                        this.currentHoverFigure.onMouseLeave() // deprecated
                        this.currentHoverFigure.fireEvent("mouseleave")
                        this.fireEvent("mouseleave", { figure: this.currentHoverFigure })
                    }
                    if (hover !== this.currentHoverFigure && hover !== null) {
                        hover.onMouseEnter()
                        hover.fireEvent("mouseenter")
                        this.fireEvent("mouseenter", { figure: hover })
                    }
                    this.currentHoverFigure = hover
                } catch (exc) {
                    // just write it to the console
                    console.log(exc)
                }

                this.editPolicy.each((i, policy) => {
                    policy.onMouseMove(this, pos.x, pos.y, event.shiftKey, event.ctrlKey)
                })
                this.fireEvent("mousemove", {
                    x: pos.x,
                    y: pos.y,
                    shiftKey: event.shiftKey,
                    ctrlKey: event.ctrlKey,
                    hoverFigure: this.currentHoverFigure
                })
            } else {
                let diffXAbs = (event.clientX - this.mouseDownX) * this.zoomFactor
                let diffYAbs = (event.clientY - this.mouseDownY) * this.zoomFactor
                this.editPolicy.each((i, policy) => {
                    policy.onMouseDrag(this, diffXAbs, diffYAbs, diffXAbs - this.mouseDragDiffX, diffYAbs - this.mouseDragDiffY, event.shiftKey, event.ctrlKey)
                })
                this.mouseDragDiffX = diffXAbs
                this.mouseDragDiffY = diffYAbs
                this.fireEvent("mousemove", {
                    x: pos.x,
                    y: pos.y,
                    shiftKey: event.shiftKey,
                    ctrlKey: event.ctrlKey,
                    hoverFigure: this.currentHoverFigure
                })
            }
        })



        //this.on('click', e => console.log('click', e))

        // Seems that the parent canvas forgot handling touchstart as a mouse down event
        this.html.bind("touchstart", (event) => {
            try {
                let pos = null
                switch (event.which) {
                    case 1: //touch pressed
                    case 0: //Left mouse button pressed
                        try {
                            event.preventDefault()
                            event = this._getEvent(event)
                            this.mouseDownX = event.clientX
                            this.mouseDownY = event.clientY
                            this.mouseDragDiffX = 0
                            this.mouseDragDiffY = 0
                            this.pinchDiff = -1
                            this.touchStartTime = performance.now()
                            pos = this.fromDocumentToCanvasCoordinate(event.clientX, event.clientY)
                            this.mouseDown = true
                            this.editPolicy.each((i, policy) => {
                                policy.onMouseDown(this, pos.x, pos.y, event.shiftKey, event.ctrlKey)
                            })
                        } catch (exc) {
                            console.log(exc)
                        }
                        break
                    case 3: //Right mouse button pressed
                        event.preventDefault()
                        if (typeof event.stopPropagation !== "undefined")
                            event.stopPropagation()
                        event = this._getEvent(event)
                        pos = this.fromDocumentToCanvasCoordinate(event.clientX, event.clientY)
                        this.onRightMouseDown(pos.x, pos.y, event.shiftKey, event.ctrlKey)
                        return false
                    case 2:
                        //Middle mouse button pressed
                        break
                    default:
                    //You have a strange mouse
                }
            } catch (exc) {
                console.log(exc)
            }
        })


        // Parent canvas seems to have forgotten to handle click and double-click for touchend
        this.html.bind("touchend", (event) => {
            const longClickTimeout = 500
            const maxDist = 10

            // Calculate click length and double click interval
            const clickTime = performance.now() - this.touchStartTime
            const clickInterval = performance.now() - this.touchEndTime
            this.touchEndTime = performance.now()

            if (event.touches?.length > 0) {
                // Multi touch ends for one touch, skip this event since neither click nor double click
                this.touchEndTime = 0
                this.pinchDiff = -1
                return
            }

            event = this._getEvent(event)

            if (this.mouseDownX === event.clientX || this.mouseDownY === event.clientY) {
                // Handle click for touch events
                let pos = this.fromDocumentToCanvasCoordinate(event.clientX, event.clientY)
                this.onClick(pos.x, pos.y, event.shiftKey, event.ctrlKey)
                console.log('click')
            }

            if (clickTime > longClickTimeout &&
                (Math.abs(this.mouseDownX - event.clientX) < maxDist &&
                    Math.abs(this.mouseDownY - event.clientY) < maxDist)) {
                // long click to simulate context menu
                document.dispatchEvent(new CustomEvent('longclick', { detail: event }));
            }

            if (clickInterval < 500) {
                // Handle double-click event for touch
                this.touchDownX = event.clientX
                this.touchDownY = event.clientY
                let pos = this.fromDocumentToCanvasCoordinate(event.clientX, event.clientY)
                this.onDoubleClick(pos.x, pos.y, event.shiftKey, event.ctrlKey)
            }
        })
    }

    distance = (x1, y1, x2, y2) => {
        return Math.sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2))
    }
}
