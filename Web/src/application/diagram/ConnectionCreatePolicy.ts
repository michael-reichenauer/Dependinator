import draw2d from "draw2d";
import Connection from "./Connection";
import Canvas from "./Canvas";

export default class ConnectionCreatePolicy extends draw2d.policy.connection
  .ConnectionCreatePolicy {
  NAME = "ConnectionCreatePolicy";

  constructor(attr?: any, setter?: any, getter?: any) {
    super(attr, setter, getter);

    this.mouseDraggingElement = null;
    this.currentDropTarget = null;
    this.currentTarget = null;
  }

  onMouseDown(
    canvas: Canvas,
    x: number,
    y: number,
    shiftKey: boolean,
    ctrlKey: boolean
  ) {
    var port = canvas.getBestFigure(x, y);

    if (port === null || !(port instanceof draw2d.Port)) {
      // Not a port
      return;
    }

    // this can happen if the user release the mouse button outside the window during a drag&drop
    // operation. In this case we must fire the "onDragEnd" event postpone.
    if (port.isInDragDrop === true) {
      port.onDragEnd(x, y, shiftKey, ctrlKey);
      port.isInDragDrop = false;
    }

    // introspect the port only if it is draggable at all
    if (port.isDraggable()) {
      var canDragStart = port.onDragStart(
        x - port.getAbsoluteX(),
        y - port.getAbsoluteY(),
        shiftKey,
        ctrlKey
      );
      if (canDragStart) {
        port.fireEvent("dragstart", {
          x: x - port.getAbsoluteX(),
          y: y - port.getAbsoluteY(),
          shiftKey: shiftKey,
          ctrlKey: ctrlKey,
        });
      }

      // Element send a veto about the drag&drop operation
      this.mouseDraggingElement = canDragStart === false ? null : port;
      this.mouseDownElement = port;
    }
  }

  onMouseDrag(
    canvas: Canvas,
    dx: number,
    dy: number,
    dx2: number,
    dy2: number,
    shiftKey: boolean,
    ctrlKey: boolean
  ): void {
    try {
      if (this.mouseDraggingElement !== null) {
        var de = this.mouseDraggingElement;
        var ct = this.currentTarget;

        de.isInDragDrop = true;
        de.onDrag(dx, dy, dx2, dy2, shiftKey, ctrlKey);

        var target = canvas.getBestFigure(
          de.getAbsoluteX(),
          de.getAbsoluteY(),
          de
        );

        // the hovering element has been changed
        if (target !== ct) {
          if (ct !== null) {
            ct.onDragLeave(de);
            ct.fireEvent("dragLeave", { draggingElement: de });
            de.editPolicy.each(function (_: number, e: any) {
              if (e instanceof draw2d.policy.port.PortFeedbackPolicy) {
                e.onHoverLeave(canvas, de, ct);
              }
            });
          }

          // possible hoverEnter event
          //
          if (target !== null) {
            this.currentTarget = ct = target.delegateTarget(de);
            if (ct !== null) {
              ct.onDragEnter(de); // legacy
              ct.fireEvent("dragEnter", { draggingElement: de });
              de.editPolicy.each(function (_: number, e: any) {
                if (e instanceof draw2d.policy.port.PortFeedbackPolicy) {
                  e.onHoverEnter(canvas, de, ct);
                }
              });
            }
          } else {
            this.currentTarget = null;
          }
        }

        var p = canvas.fromDocumentToCanvasCoordinate(
          canvas.mouseDownX + dx / canvas.zoomFactor,
          canvas.mouseDownY + dy / canvas.zoomFactor
        );
        target = canvas.getBestFigure(p.x, p.y, this.mouseDraggingElement);

        if (target !== this.currentDropTarget) {
          if (this.currentDropTarget !== null) {
            this.currentDropTarget.onDragLeave(this.mouseDraggingElement);
            this.currentDropTarget.fireEvent("dragLeave", {
              draggingElement: this.mouseDraggingElement,
            });
            this.currentDropTarget = null;
          }
          if (target !== null) {
            this.currentDropTarget = target.delegateTarget(
              this.mouseDraggingElement
            );
            // inform all listener that the element has accept the dragEnter event
            //
            if (this.currentDropTarget !== null) {
              this.currentDropTarget.onDragEnter(this.mouseDraggingElement); // legacy
              this.currentDropTarget.fireEvent("dragEnter", {
                draggingElement: this.mouseDraggingElement,
              });
            }
          }
        }
      }
    } catch (exc) {
      console.log(exc);
      debugger;
    }
  }

  onMouseUp(
    canvas: Canvas,
    x: number,
    y: number,
    shiftKey: boolean,
    ctrlKey: boolean
  ): void {
    let isDropOnPort = false;

    if (this.mouseDraggingElement !== null) {
      var de = this.mouseDraggingElement;
      var ct = this.currentTarget;

      canvas.getCommandStack().startTransaction();

      de.onDragEnd(x, y, shiftKey, ctrlKey);

      // notify all installed policies
      if (ct) {
        de.editPolicy.each(function (_: number, e: any) {
          if (e instanceof draw2d.policy.port.PortFeedbackPolicy) {
            e.onHoverLeave(canvas, de, ct);
          }
        });
      }

      de.editPolicy.each(function (_: number, e: any) {
        if (e instanceof draw2d.policy.figure.DragDropEditPolicy) {
          e.onDragEnd(canvas, de, x, y, shiftKey, ctrlKey);
        }
      });

      // Reset the drag&drop flyover information
      this.currentTarget = null;
      de.isInDragDrop = false;

      // fire an event
      de.fireEvent("dragend", {
        x: x,
        y: y,
        shiftKey: shiftKey,
        ctrlKey: ctrlKey,
      });

      // check if we drop the port onto a valid
      // drop target and create a connection if possible
      if (this.currentDropTarget !== null) {
        this.mouseDraggingElement.onDrop(
          this.currentDropTarget,
          x,
          y,
          shiftKey,
          ctrlKey
        );

        this.currentDropTarget.onDragLeave(this.mouseDraggingElement);
        this.currentDropTarget.fireEvent("dragLeave", {
          draggingElement: this.mouseDraggingElement,
        });

        // Ports accepts only Ports as DropTarget
        if (this.currentDropTarget instanceof draw2d.Port) {
          var request = new draw2d.command.CommandType(
            draw2d.command.CommandType.CONNECT
          );
          request.source = this.currentDropTarget;
          request.target = this.mouseDraggingElement;
          var command = this.mouseDraggingElement.createCommand(request);

          if (command !== null) {
            isDropOnPort = true;
            command.setConnection(this.createConnection());
            canvas.getCommandStack().execute(command);
            this.currentDropTarget.onCatch(
              this.mouseDraggingElement,
              x,
              y,
              shiftKey,
              ctrlKey
            );
          }
        }
      }

      // end command stack trans
      canvas.getCommandStack().commitTransaction();
      this.currentDropTarget = null;
      this.mouseDraggingElement = null;
      if (!isDropOnPort) {
        //this.onNoPortDrop(x, y, shiftKey, ctrlKey)
      }
    }
  }

  createConnection(): Connection {
    return new Connection();
  }
}
