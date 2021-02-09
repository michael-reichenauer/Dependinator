import draw2d from "draw2d";

export let ConnectionCreatePolicy = draw2d.policy.connection.ConnectionCreatePolicy.extend(
    {

        NAME: "ConnectionCreatePolicy",

        /**
         *
         * Creates a new connection create policy instance
         */
        init: function (attr, setter, getter) {
            this._super(attr, setter, getter);

            this.mouseDraggingElement = null;
            this.currentDropTarget = null;
            this.currentTarget = null;
        },

        /**
         * 
         *
         * @param {draw2d.Canvas} canvas
         * @param {Number} x the x-coordinate of the mouse down event
         * @param {Number} y the y-coordinate of the mouse down event
         * @param {Boolean} shiftKey true if the shift key has been pressed during this event
         * @param {Boolean} ctrlKey true if the ctrl key has been pressed during the event
         */
        onMouseDown: function (canvas, x, y, shiftKey, ctrlKey) {
            //just consider ports
            //
            var port = canvas.getBestFigure(x, y);


            // nothing found at all
            //
            if (port === null) {
                return;
            }

            // may there is a resize handle below the port or another figure
            // in this case the ResizeHandle has prio. and handled by another
            // Policy
            if (!(port instanceof draw2d.Port)) {
                return;
            }

            // this can happen if the user release the mouse button outside the window during a drag&drop
            // operation. In this case we must fire the "onDragEnd" event postpond.
            //
            if (port.isInDragDrop === true) {
                port.onDragEnd(x, y, shiftKey, ctrlKey);
                port.isInDragDrop = false;
            }

            // introspect the port only if it is draggable at all
            //
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

        /**
         * 
         *
         * @param {draw2d.Canvas} canvas
         * @param {Number} dx The x diff between start of dragging and this event
         * @param {Number} dy The y diff between start of dragging and this event
         * @param {Number} dx2 The x diff since the last call of this dragging operation
         * @param {Number} dy2 The y diff since the last call of this dragging operation
         * @template
         */
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


        /**
         * 
         *
         * @param {draw2d.Figure} figure the shape below the mouse or null
         * @param {Number} x the x-coordinate of the mouse down event
         * @param {Number} y the y-coordinate of the mouse down event
         * @param {Boolean} shiftKey true if the shift key has been pressed during this event
         * @param {Boolean} ctrlKey true if the ctrl key has been pressed during the event
         */
        onMouseUp: function (canvas, x, y, shiftKey, ctrlKey) {
            let isDropOnPort = false

            if (this.mouseDraggingElement !== null) {
                var de = this.mouseDraggingElement;
                var ct = this.currentTarget;
                // start CommandStack transaction
                canvas.getCommandStack().startTransaction();

                de.onDragEnd(x, y, shiftKey, ctrlKey);
                // notify all installed policies
                //
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
                //
                this.currentTarget = null;
                de.isInDragDrop = false;

                // fire an event
                // @since 5.3.3
                de.fireEvent("dragend", { x: x, y: y, shiftKey: shiftKey, ctrlKey: ctrlKey });

                // check if we drop the port onto a valid
                // drop target and create a connection if possible
                //
                if (this.currentDropTarget !== null) {
                    this.mouseDraggingElement.onDrop(this.currentDropTarget, x, y, shiftKey, ctrlKey);

                    this.currentDropTarget.onDragLeave(this.mouseDraggingElement);
                    this.currentDropTarget.fireEvent("dragLeave", { draggingElement: this.mouseDraggingElement });

                    // Ports accepts only Ports as DropTarget
                    //
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
            var connection = this._super();
            connection.setColor('#222222')
            connection.setRouter(new draw2d.layout.connection.DirectRouter());

            const arrow = new draw2d.decoration.connection.ArrowDecorator()
            arrow.setBackgroundColor(connection.getColor())
            arrow.setDimension(12, 12)
            connection.targetDecorator = arrow

            return connection;
        }


    });

