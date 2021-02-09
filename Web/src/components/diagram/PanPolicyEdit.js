import draw2d from "draw2d";


export let PanPolicyEdit = draw2d.policy.canvas.SingleSelectionPolicy.extend(
    {
        NAME: "PanPolicyEdit",

        togglePanPolicy: null,
        createItem: null,
        canvas: null,

        init: function (togglePanPolicy, createItem) {
            this._super()
            this.togglePanPolicy = togglePanPolicy
            this.createItem = createItem
        },

        onInstall: function (canvas) {
            this._super(canvas)
            this.canvas = canvas
            canvas.isReadOnlyMode = false
            canvas.getAllPorts().each(function (i, port) {
                port.setVisible(true)
            })
        },

        onClick: function (figure, mouseX, mouseY, shiftKey, ctrlKey) {
            if (figure !== null) {
                if (figure instanceof draw2d.shape.basic.GhostVertexResizeHandle) {
                    // Clicked on connection vertex handle, add a new vertex
                    figure.onClick()
                }
                return
            }

            this.togglePanPolicy()
        },

        // onDoubleClick: function (figure, mouseX, mouseY, shiftKey, ctrlKey) {
        //     if (figure !== null) {
        //         return
        //     }

        //     this.createItem(mouseX, mouseY, shiftKey, ctrlKey)
        //     this.togglePanPolicy()
        // },

        /**
         * 
         *
         * @param {draw2d.Canvas} canvas
         * @param {Number} dx The x diff between start of dragging and this event
         * @param {Number} dy The y diff between start of dragging and this event
         * @param {Number} dx2 The x diff since the last call of this dragging operation
         * @param {Number} dy2 The y diff since the last call of this dragging operation
         * @param {Boolean} shiftKey true if the shift key has been pressed during this event
         * @param {Boolean} ctrlKey true if the ctrl key has been pressed during the event
         * @template
         */
        onMouseDrag: function (canvas, dx, dy, dx2, dy2, shiftKey, ctrlKey) {
            this._super(canvas, dx, dy, dx2, dy2, shiftKey, ctrlKey)

            if (this.mouseDraggingElement === null && this.mouseDownElement === null) {

                // check if we are dragging a port. This isn't reported by the selection handler anymore
                //
                let p = canvas.fromDocumentToCanvasCoordinate(canvas.mouseDownX + (dx / canvas.zoomFactor), canvas.mouseDownY + (dy / canvas.zoomFactor))
                let figure = canvas.getBestFigure(p.x, p.y)

                if (figure === null) {
                    let area = canvas.getScrollArea()
                    let zoom = canvas.getZoom()
                    area.scrollTop(area.scrollTop() - dy2 / zoom)
                    area.scrollLeft(area.scrollLeft() - dx2 / zoom)
                }
            }
        }
    })
