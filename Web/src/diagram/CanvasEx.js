import draw2d from "draw2d";
import { WheelZoomPolicy } from "./WheelZoomPolicy"
import { ConnectionCreatePolicy } from "./ConnectionCreatePolicy"
import { PanPolicy } from "./PanPolicy";
import Colors from "./colors";

const diagramSize = 100000

export const createCanvas = (canvasId, onEditMode) => {
    const canvas = new CanvasEx(canvasId)
    canvas.setScrollArea("#" + canvasId)
    canvas.setDimension(new draw2d.geo.Rectangle(0, 0, diagramSize, diagramSize))

    // A likely bug in draw2d can be fixed with this hack
    canvas.regionDragDropConstraint.constRect = new draw2d.geo.Rectangle(0, 0, diagramSize, diagramSize)

    // Center the canvas
    const area = canvas.getScrollArea()
    area.scrollLeft(diagramSize / 2)
    area.scrollTop(diagramSize / 2)

    canvas.panPolicy = new PanPolicy(onEditMode)
    canvas.installEditPolicy(canvas.panPolicy)

    canvas.zoomPolicy = new WheelZoomPolicy()
    canvas.installEditPolicy(canvas.zoomPolicy);

    canvas.installEditPolicy(new ConnectionCreatePolicy())
    canvas.installEditPolicy(new draw2d.policy.canvas.CoronaDecorationPolicy());

    canvas.html.find("svg").css('background-color', Colors.canvasDivBackground)

    canvas.installEditPolicy(new draw2d.policy.canvas.SnapToGeometryEditPolicy())
    canvas.installEditPolicy(new draw2d.policy.canvas.SnapToInBetweenEditPolicy())
    canvas.installEditPolicy(new draw2d.policy.canvas.SnapToCenterEditPolicy())
    //canvas.installEditPolicy(new draw2d.policy.canvas.SnapToGridEditPolicy(10, false))

    return canvas
}

export const setBackground = (canvas) => {
    canvas.html.find("svg").css({
        'background-color': Colors.canvasDivBackground,
        "background": Colors.canvasDivBackground,
        "background-size": 0
    })
}

export const setGridBackground = (canvas) => {
    // In edit mode, add a grid background.
    const bgColor = Colors.canvasDivBackground
    const gridColor = Colors.canvasGridRgb
    const interval = 10
    const gridStroke = 1

    let background =
        ` linear-gradient(to right,  ${gridColor} ${gridStroke}px, transparent ${gridStroke}px),
          linear-gradient(to bottom, ${gridColor} ${gridStroke}px, ${bgColor}  ${gridStroke}px)`
    let backgroundSize = `${interval}px ${interval}px`

    canvas.html.find("svg").css({
        "background": background,
        "background-size": backgroundSize
    })
}

const CanvasEx = draw2d.Canvas.extend(
    {
        init: function (canvasId, width, height) {
            this._super(canvasId, width, height);
        },

        addAll: function (figures) {
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
    }
)
