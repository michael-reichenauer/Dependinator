import draw2d from "draw2d";

export const CanvasEx = draw2d.Canvas.extend(
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
