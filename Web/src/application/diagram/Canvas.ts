import draw2d from "draw2d";
import PanPolicy from "./PanPolicy";
import ZoomPolicy from "./ZoomPolicy";
import KeyboardPolicy from "./KeyboardPolicy";
import ConnectionCreatePolicy from "./ConnectionCreatePolicy";
import Colors from "./Colors";
import { random } from "../../common/utils";
import CanvasSerializer from "./CanvasSerializer";
import DiagramCanvas from "./DiagramCanvas";
import { Canvas2d, Command2d, Figure2d, Line2d } from "./draw2dTypes";
import { CanvasDto } from "./StoreDtos";

const randomDist = 30;

export default class Canvas extends draw2d.Canvas {
  diagramCanvas: DiagramCanvas;
  serializer: CanvasSerializer;

  diagramId: string = "";
  diagramName: string = "";
  canvasId: string = "";
  mainNodeId: string = "";

  touchStartTime: number = 0;
  touchEndTime: number = 0;
  previousPinchDiff: number = -1;
  coronaDecorationPolicy: any = null;

  constructor(
    diagramCanvas: DiagramCanvas,
    htmlElementId: string,
    onEditMode: (isEdit: boolean) => void,
    width: number,
    height: number
  ) {
    super(htmlElementId, width, height);

    this.diagramCanvas = diagramCanvas;
    this.serializer = new CanvasSerializer(this);

    this.setScrollArea("#" + htmlElementId);
    this.setDimension(new draw2d.geo.Rectangle(0, 0, width, height));

    // A likely bug in draw2d can be fixed with this hack
    this.regionDragDropConstraint.constRect = new draw2d.geo.Rectangle(
      0,
      0,
      width,
      height
    );

    // Center the canvas
    const area = this.getScrollArea();
    area.scrollLeft(width / 2 - this.getWidth() / 2);
    area.scrollTop(height / 2 - this.getHeight() / 2);

    this.panPolicy = new PanPolicy(onEditMode);
    this.installEditPolicy(this.panPolicy);

    this.zoomPolicy = new ZoomPolicy();
    this.installEditPolicy(this.zoomPolicy);

    this.installEditPolicy(new ConnectionCreatePolicy());
    const cdp = new draw2d.policy.canvas.CoronaDecorationPolicy();
    cdp.onMouseDown = (
      canvas: Canvas2d,
      x: number,
      y: number,
      _shiftKey: boolean,
      _ctrlKey: boolean
    ) => {
      cdp.startDragX = x;
      cdp.startDragY = y;
      cdp.updatePorts(canvas, x, y);
    };
    this.coronaDecorationPolicy = cdp;

    this.installEditPolicy(cdp);

    this.setNormalBackground();

    this.installEditPolicy(new draw2d.policy.canvas.SnapToGeometryEditPolicy());
    this.installEditPolicy(
      new draw2d.policy.canvas.SnapToInBetweenEditPolicy()
    );
    this.installEditPolicy(new draw2d.policy.canvas.SnapToCenterEditPolicy());
    this.installEditPolicy(new KeyboardPolicy());

    this.enableTouchSupport();

    //canvas.installEditPolicy(new draw2d.policy.canvas.SnapToGridEditPolicy(10, false))
  }

  serialize(): CanvasDto {
    return this.serializer.serialize();
  }

  deserialize(canvasDto: CanvasDto): void {
    this.serializer.deserialize(canvasDto);
  }

  save(): void {
    this.diagramCanvas.save();
  }

  exportAsSvg(
    canvasData: CanvasDto,
    width: number,
    height: number,
    margin: number
  ): string {
    const canvasWidth = this.getDimension().getWidth();
    const canvasHeight = this.getDimension().getHeight();
    let svgResult: string = "";

    const canvas = new Canvas(
      this.diagramCanvas,
      "canvasPrint",
      () => {},
      canvasWidth,
      canvasHeight
    );
    canvas.deserialize(canvasData);
    canvas.export(width, height, margin, (svg: string) => (svgResult = svg));
    canvas.destroy();
    return svgResult;
  }

  export(
    width: number,
    height: number,
    margin: number,
    resultHandler: (svgText: string) => void
  ): void {
    const rect = this.getFiguresRect();
    this.serializer.export(rect, width, height, margin, resultHandler);
  }

  clearDiagram = (): void => {
    const canvas = this;
    canvas.lines.clone().each((_: number, e: Line2d) => canvas.remove(e));
    canvas.figures.clone().each((_: number, e: Figure2d) => canvas.remove(e));
    canvas.selection.clear();
    canvas.currentDropTarget = null;
    canvas.figures = new draw2d.util.ArrayList();
    canvas.lines = new draw2d.util.ArrayList();
    canvas.commonPorts = new draw2d.util.ArrayList();
    canvas.commandStack.markSaveLocation();
    canvas.linesToRepaintAfterDragDrop = new draw2d.util.ArrayList();
    canvas.lineIntersections = new draw2d.util.ArrayList();
    canvas.diagramId = "";
    canvas.canvasId = "";
  };

  runCmd(command: Command2d): void {
    this.getCommandStack().execute(command);
  }

  setNormalBackground() {
    this.html.find("svg").css({
      "background-color": Colors.canvasDivBackground,
      background: Colors.canvasDivBackground,
      "background-size": 0,
    });
  }

  setGridBackground(): void {
    // // In edit mode, add a grid background.
    // const bgColor = Colors.canvasDivBackground
    // const gridColor = Colors.canvasGridRgb
    // const interval = 10
    // const gridStroke = 1
    // let background =
    //     ` linear-gradient(to right,  ${gridColor} ${gridStroke}px, transparent ${gridStroke}px),
    //       linear-gradient(to bottom, ${gridColor} ${gridStroke}px, ${bgColor}  ${gridStroke}px)`
    // let backgroundSize = `${interval}px ${interval}px`
    // this.html.find("svg").css({
    //     "background": background,
    //     "background-size": backgroundSize
    // })
  }

  addAtApproximately(figure: Figure2d, x: number, y: number) {
    if (
      null !=
      this.getFigures()
        .asArray()
        .find((f: Figure2d) => f.x === x && f.y === y)
    ) {
      // Figure exists at that place, lets retry with other coordinate
      x = x + random(-randomDist, randomDist);
      y = y + random(-randomDist, randomDist);
      this.addAtApproximately(figure, x, y);
      return;
    }

    // No other figure at this place, lets add
    this.add(figure, x, y);
  }

  addAll(figures: Figure2d[]) {
    //const t = timing()
    for (let i = 0; i < figures.length; i++) {
      let figure = figures[i];
      let x = figures[i].x;
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

    this.figures.each(function (_i: number, fig: Figure2d) {
      fig.repaint();
    });
    //t.log("Repainted figures");

    this.lines.each(function (_i: number, line: Line2d) {
      line.svgPathString = null;
      line.repaint();
    });

    // Fix so that all ports are not shown at first start, only when mouse is moved close to node
    this.coronaDecorationPolicy.updatePorts(this, 0, 0);
    return this;
  }

  getFiguresRect() {
    const d = this.getDimension();
    let minX = d.getWidth();
    let minY = d.getHeight();
    let maxX = 0;
    let maxY = 0;

    this.getFigures().each((i: number, f: Figure2d) => {
      let fx = f.getAbsoluteX();
      let fy = f.getAbsoluteY();
      let fx2 = fx + f.getWidth();
      let fy2 = fy + f.getHeight();

      if (i === 0) {
        // First figure sets min/max which other figures can adjust
        minX = fx;
        minY = fy;
        maxX = fx2;
        maxY = fy2;
      }

      minX = fx < minX ? fx : minX;
      minY = fy < minY ? fy : minY;
      maxX = fx2 > maxX ? fx2 : maxX;
      maxY = fy2 > maxY ? fy2 : maxY;

      // A figure can have children like labels that extend beyond the figure...
      f.getChildren().each((_: number, c: Figure2d) => {
        let fx = c.getAbsoluteX();
        let fy = c.getAbsoluteY();
        let fx2 = fx + c.getWidth();
        let fy2 = fy + c.getHeight();

        minX = fx < minX ? fx : minX;
        minY = fy < minY ? fy : minY;
        maxX = fx2 > maxX ? fx2 : maxX;
        maxY = fy2 > maxY ? fy2 : maxY;
      });
    });

    this.getLines().each((_: number, l: Line2d) => {
      l.vertices.each((_: number, v: any) => {
        minX = v.x < minX ? v.x : minX;
        minY = v.y < minY ? v.y : minY;
        maxX = v.x > maxX ? v.x : maxX;
        maxY = v.y > maxY ? v.y : maxY;
      });
    });

    const w = Math.max(maxX - minX, 0);
    const h = Math.max(maxY - minY, 0);
    const x2 = minX + w;
    const y2 = minY + h;

    return {
      x: minX,
      y: minY,
      w: w,
      h: h,
      x2: x2,
      y2: y2,
    };
  }

  enableTouchSupport() {
    // Seems that the parent does nog handle touchstart same as a mouse down event
    this.html.bind("touchstart", (event: any) => this.handleTouchStart(event));

    //Seems that the parent does nog handle click and double-click for touchend
    this.html.bind("touchend", (event: any) => this.handleTouchEnd(event));

    // Need to replace the parent handler of mouse and touch move to enable multi touch as well
    this.html.unbind("mousemove touchmove");
    this.html.bind("mousemove touchmove", (event: any) => {
      if (event.type === "touchmove" && event.touches.length === 2) {
        this.handlePinchTouchMove(event);
        return;
      }

      this.handleMouseTouchMove(event);
    });
  }

  // Handle touch start same as if mouse down in parent canvas
  // @ts-ignore
  handleTouchStart = (event: any) => {
    try {
      let pos: any = null;
      switch (event.which) {
        case 1: //touch pressed
        case 0: //Left mouse button pressed or touch
          try {
            event.preventDefault();
            event = this._getEvent(event);
            this.mouseDownX = event.clientX;
            this.mouseDownY = event.clientY;
            this.mouseDragDiffX = 0;
            this.mouseDragDiffY = 0;
            this.previousPinchDiff = -1;
            this.touchStartTime = performance.now();
            this.startLongTouchDetection(event);
            pos = this.fromDocumentToCanvasCoordinate(
              event.clientX,
              event.clientY
            );
            this.mouseDown = true;
            this.editPolicy.each((_: number, policy: any) => {
              policy.onMouseDown(
                this,
                pos.x,
                pos.y,
                event.shiftKey,
                event.ctrlKey
              );
            });
          } catch (exc) {
            console.log(exc);
          }
          break;
        case 3: //Right mouse button pressed
          event.preventDefault();
          if (typeof event.stopPropagation !== "undefined")
            event.stopPropagation();
          event = this._getEvent(event);
          pos = this.fromDocumentToCanvasCoordinate(
            event.clientX,
            event.clientY
          );
          this.onRightMouseDown(pos.x, pos.y, event.shiftKey, event.ctrlKey);
          return false;
        case 2:
          //Middle mouse button pressed
          break;
        default:
        //You have a strange mouse
      }
    } catch (exc) {
      console.log(exc);
    }
  };

  // Handle touch end to support touch click, double-click and long click to simulate right click
  // for context menu
  handleTouchEnd = (event: any) => {
    this.cancelLongTouch();
    this.pinchDiff = -1;

    // Calculate click length and double click interval
    const clickInterval = performance.now() - this.touchEndTime;
    this.touchEndTime = performance.now();

    if (this.longTouchHandled) {
      // No click detection when long touch press
      return;
    }

    if (event.touches?.length > 0) {
      // Multi touch ends for one touch, skip this event since neither click nor double click
      this.touchEndTime = 0;
      return;
    }

    event = this._getEvent(event);

    if (
      this.mouseDownX === event.clientX ||
      this.mouseDownY === event.clientY
    ) {
      // Handle click for touch events
      let pos = this.fromDocumentToCanvasCoordinate(
        event.clientX,
        event.clientY
      );
      this.onClick(pos.x, pos.y, event.shiftKey, event.ctrlKey);
      // console.log('click')
    }

    if (clickInterval < 500) {
      // Handle double-click event for touch
      this.touchDownX = event.clientX;
      this.touchDownY = event.clientY;
      let pos = this.fromDocumentToCanvasCoordinate(
        event.clientX,
        event.clientY
      );
      this.onDoubleClick(pos.x, pos.y, event.shiftKey, event.ctrlKey);
    }
  };

  // Handle touch move when two touch (pinch zoom)
  handlePinchTouchMove = (event: any) => {
    this.cancelLongTouch();

    let pinchDelta = 200 * this.zoomFactor;
    const t1 = event.touches[0];
    const t2 = event.touches[1];
    const currentDiff = this.distance(
      t1.clientX,
      t1.clientY,
      t2.clientX,
      t2.clientY
    );

    if (this.previousPinchDiff === -1) {
      // First event for pinch (lets just save diff and to be used in next event)
      this.previousPinchDiff = currentDiff;
      return;
    }

    if (currentDiff > this.previousPinchDiff) {
      // The distance between the two pointers has increased (zoom in), reverse pinch delta
      pinchDelta = -pinchDelta;
    }

    // Store diff to be used in next event
    this.previousPinchDiff = currentDiff;

    // Calculate the zoom center (middle of the two touch points)
    const x = (t2.clientX - t1.clientX) / 2 + t1.clientX;
    const y = (t2.clientY - t1.clientY) / 2 + t1.clientY;
    const center = this.fromDocumentToCanvasCoordinate(x, y);

    // Pinch uses same as mouse wheel to zoom
    this.onMouseWheel(pinchDelta, center.x, center.y, false, false);
    return;
  };

  // The parent handler of muse move and touch move did not handle multi touch move (pinch)
  handleMouseTouchMove = (event: any) => {
    this.cancelLongTouchIfMove(event);

    event = this._getEvent(event);
    //console.log('event', event)
    let pos = this.fromDocumentToCanvasCoordinate(event.clientX, event.clientY);
    if (this.mouseDown === false) {
      // mouseEnter/mouseLeave events for Figures. Don't use the Raphael or DOM native functions.
      // Raphael didn't work for Rectangle with transparent fill (events only fired for the border line)
      // DOM didn't work well for lines. No eclipse area - you must hit the line exact to retrieve the event.
      // In this case I implement my own stuff...again and again.
      //
      // don't break the main event loop if one element fires an error during enter/leave event.
      try {
        let hover = this.getBestFigure(pos.x, pos.y);
        if (
          hover !== this.currentHoverFigure &&
          this.currentHoverFigure !== null
        ) {
          this.currentHoverFigure.onMouseLeave(); // deprecated
          this.currentHoverFigure.fireEvent("mouseleave");
          this.fireEvent("mouseleave", { figure: this.currentHoverFigure });
        }
        if (hover !== this.currentHoverFigure && hover !== null) {
          hover.onMouseEnter();
          hover.fireEvent("mouseenter");
          this.fireEvent("mouseenter", { figure: hover });
        }
        this.currentHoverFigure = hover;
      } catch (exc) {
        // just write it to the console
        console.log(exc);
      }

      this.editPolicy.each((_: number, policy: any) => {
        policy.onMouseMove(this, pos.x, pos.y, event.shiftKey, event.ctrlKey);
      });
      this.fireEvent("mousemove", {
        x: pos.x,
        y: pos.y,
        shiftKey: event.shiftKey,
        ctrlKey: event.ctrlKey,
        hoverFigure: this.currentHoverFigure,
      });
    } else {
      let diffXAbs = (event.clientX - this.mouseDownX) * this.zoomFactor;
      let diffYAbs = (event.clientY - this.mouseDownY) * this.zoomFactor;
      this.editPolicy.each((_: number, policy: any) => {
        policy.onMouseDrag(
          this,
          diffXAbs,
          diffYAbs,
          diffXAbs - this.mouseDragDiffX,
          diffYAbs - this.mouseDragDiffY,
          event.shiftKey,
          event.ctrlKey
        );
      });
      this.mouseDragDiffX = diffXAbs;
      this.mouseDragDiffY = diffYAbs;
      this.fireEvent("mousemove", {
        x: pos.x,
        y: pos.y,
        shiftKey: event.shiftKey,
        ctrlKey: event.ctrlKey,
        hoverFigure: this.currentHoverFigure,
      });
    }
  };

  startLongTouchDetection = (event: any): void => {
    // console.log('Start detection')
    const longPressTimeout = 500;

    this.cancelLongTouch();
    this.longTouchStartEvent = event;
    this.longTouchHandled = false;
    this.longTouchTimer = setTimeout(
      () => this.handleLongTouchTimeout(),
      longPressTimeout
    );
  };

  handleLongTouchTimeout = (): void => {
    this.cancelLongTouch();
    this.longTouchHandled = true;
    document.dispatchEvent(
      new CustomEvent("longclick", { detail: this.longTouchStartEvent })
    );
  };

  cancelLongTouch = (): void => {
    //console.log('cancel')
    clearTimeout(this.longTouchTimer);
  };

  cancelLongTouchIfMove = (event: any): void => {
    if (event.type === "touchmove" && !this.isClose(event)) {
      this.cancelLongTouch();
    }
  };

  isClose = (event: any): boolean => {
    const maxDist = 5;
    return (
      Math.abs(this.mouseDownX - event.clientX) < maxDist &&
      Math.abs(this.mouseDownY - event.clientY) < maxDist
    );
  };

  distance = (x1: number, y1: number, x2: number, y2: number): number => {
    return Math.sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));
  };
}
