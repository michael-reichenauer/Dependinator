import draw2d from "draw2d";
import PanPolicy from "./PanPolicy";
import ZoomPolicy from "./ZoomPolicy"
import KeyboardPolicy from "./KeyboardPolicy";
import ConnectionCreatePolicy from "./ConnectionCreatePolicy"
import Colors from "./Colors";
import { random } from "../common/utils";


const randomDist = 30

export default class Canvas extends draw2d.Canvas {
    name = 'root'

    constructor(canvasId, onEditMode, width, height) {
        super(canvasId, width, height);

        this.setScrollArea("#" + canvasId)
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
        //canvas.installEditPolicy(new draw2d.policy.canvas.SnapToGridEditPolicy(10, false))
    }

    clearDiagram = () => {
        const canvas = this
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
        // t.log("Repainted lines");
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

}

