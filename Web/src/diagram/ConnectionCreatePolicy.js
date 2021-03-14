import draw2d from "draw2d";
import { configureDefaultConnection } from './connections'
import Node from "./figures";

export const ConnectionCreatePolicy = draw2d.policy.connection.ConnectionCreatePolicy.extend(
    {
        NAME: "ConnectionCreatePolicy",

        init: function (attr, setter, getter) {
            this._super(attr, setter, getter);

            this.mouseDraggingElement = null;
            this.currentDropTarget = null;
            this.currentTarget = null;
        },

        onMouseDown: function (canvas, x, y, shiftKey, ctrlKey) {
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
                var canDragStart = port.onDragStart(x - port.getAbsoluteX(), y - port.getAbsoluteY(), shiftKey, ctrlKey);
                if (canDragStart) {
                    port.fireEvent("dragstart", { x: x - port.getAbsoluteX(), y: y - port.getAbsoluteY(), shiftKey: shiftKey, ctrlKey: ctrlKey });
                }

                // Element send a veto about the drag&drop operation
                this.mouseDraggingElement = canDragStart === false ? null : port;
                this.mouseDownElement = port;
            }
        },

        onMouseDrag: function (canvas, dx, dy, dx2, dy2, shiftKey, ctrlKey) {
            try {
                if (this.mouseDraggingElement !== null) {
                    var de = this.mouseDraggingElement;
                    var ct = this.currentTarget;

                    de.isInDragDrop = true;
                    de.onDrag(dx, dy, dx2, dy2, shiftKey, ctrlKey);

                    var target = canvas.getBestFigure(de.getAbsoluteX(), de.getAbsoluteY(), de);

                    // the hovering element has been changed
                    if (target !== ct) {
                        if (ct !== null) {
                            ct.onDragLeave(de);
                            ct.fireEvent("dragLeave", { draggingElement: de });
                            de.editPolicy.each(function (i, e) {
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
                                de.editPolicy.each(function (i, e) {
                                    if (e instanceof draw2d.policy.port.PortFeedbackPolicy) {
                                        e.onHoverEnter(canvas, de, ct);
                                    }
                                });
                            }
                        }
                        else {
                            this.currentTarget = null;
                        }
                    }


                    var p = canvas.fromDocumentToCanvasCoordinate(canvas.mouseDownX + (dx / canvas.zoomFactor), canvas.mouseDownY + (dy / canvas.zoomFactor));
                    target = canvas.getBestFigure(p.x, p.y, this.mouseDraggingElement);

                    if (target !== this.currentDropTarget) {
                        if (this.currentDropTarget !== null) {
                            this.currentDropTarget.onDragLeave(this.mouseDraggingElement);
                            this.currentDropTarget.fireEvent("dragLeave", { draggingElement: this.mouseDraggingElement });
                            this.currentDropTarget = null;
                        }
                        if (target !== null) {
                            this.currentDropTarget = target.delegateTarget(this.mouseDraggingElement);
                            // inform all listener that the element has accept the dragEnter event
                            //
                            if (this.currentDropTarget !== null) {
                                this.currentDropTarget.onDragEnter(this.mouseDraggingElement); // legacy
                                this.currentDropTarget.fireEvent("dragEnter", { draggingElement: this.mouseDraggingElement });
                            }
                        }
                    }
                }
            }
            catch (exc) {
                console.log(exc);
                debugger;
            }
        },

        onMouseUp: function (canvas, x, y, shiftKey, ctrlKey) {
            let isDropOnPort = false

            if (this.mouseDraggingElement !== null) {
                var de = this.mouseDraggingElement;
                var ct = this.currentTarget;

                canvas.getCommandStack().startTransaction();

                de.onDragEnd(x, y, shiftKey, ctrlKey);

                // notify all installed policies
                if (ct) {
                    de.editPolicy.each(function (i, e) {
                        if (e instanceof draw2d.policy.port.PortFeedbackPolicy) {
                            e.onHoverLeave(canvas, de, ct);
                        }
                    });
                }

                de.editPolicy.each(function (i, e) {
                    if (e instanceof draw2d.policy.figure.DragDropEditPolicy) {
                        e.onDragEnd(canvas, de, x, y, shiftKey, ctrlKey);
                    }
                });

                // Reset the drag&drop flyover information
                this.currentTarget = null;
                de.isInDragDrop = false;

                // fire an event
                de.fireEvent("dragend", { x: x, y: y, shiftKey: shiftKey, ctrlKey: ctrlKey });


                if (this.currentDropTarget === null) {
                    // No drop target, lets create a node first to connect to
                    const figure = Node.createDefault(Node.nodeType).figure

                    // Determine figure point and port name
                    let p = null
                    let targetPortName = null
                    switch (this.mouseDraggingElement.name) {
                        case "output0":
                            p = { x: x, y: y - figure.height / 2 }
                            targetPortName = 'input0'
                            break
                        case "output1":
                            p = { x: x - figure.width / 2, y: y }
                            targetPortName = 'input1'
                            break
                        case "input0":
                            p = { x: x - figure.width, y: y - figure.height / 2 }
                            targetPortName = 'output0'
                            break
                        case "input1":
                            p = { x: x - figure.width / 2, y: y - figure.height }
                            targetPortName = 'output1'
                            break
                        default:
                            throw new Error('Unknown port name' + this.mouseDraggingElement.name)
                    }


                    // Add node first so connection can be added in next command
                    const command = new draw2d.command.CommandAdd(canvas, figure, p.x, p.y);
                    canvas.getCommandStack().execute(command);
                    canvas.getCommandStack().commitTransaction();

                    // Prepare to add connection in next command
                    this.currentDropTarget = figure.getPort(targetPortName);
                    canvas.getCommandStack().startTransaction();
                }

                // check if we drop the port onto a valid
                // drop target and create a connection if possible
                if (this.currentDropTarget !== null) {
                    this.mouseDraggingElement.onDrop(this.currentDropTarget, x, y, shiftKey, ctrlKey);

                    this.currentDropTarget.onDragLeave(this.mouseDraggingElement);
                    this.currentDropTarget.fireEvent("dragLeave", { draggingElement: this.mouseDraggingElement });

                    // Ports accepts only Ports as DropTarget
                    if (this.currentDropTarget instanceof draw2d.Port) {
                        var request = new draw2d.command.CommandType(draw2d.command.CommandType.CONNECT);
                        request.source = this.currentDropTarget;
                        request.target = this.mouseDraggingElement;
                        var command = this.mouseDraggingElement.createCommand(request);

                        if (command !== null) {
                            isDropOnPort = true
                            command.setConnection(this.createConnection());
                            canvas.getCommandStack().execute(command);
                            this.currentDropTarget.onCatch(this.mouseDraggingElement, x, y, shiftKey, ctrlKey);
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
        },


        createConnection: function () {
            const connection = this._super();
            return configureDefaultConnection(connection)
        }
    });

