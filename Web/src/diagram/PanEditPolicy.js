import draw2d from "draw2d";


export let PanEditPolicy = draw2d.policy.canvas.BoundingboxSelectionPolicy.extend(
    {
        NAME: "PanEditPolicy",

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
                if (figure instanceof draw2d.shape.icon.Diagram || figure instanceof draw2d.shape.icon.Contract) {
                    // Clicked on connection vertex handle, add a new vertex
                    figure.onClickDiagram()
                }
                return
            }

            this.togglePanPolicy()
        },

        onMouseDrag: function (canvas, dx, dy, dx2, dy2, shiftKey, ctrlKey) {
            this._super(canvas, dx, dy, dx2, dy2, shiftKey, ctrlKey)

            // if (this.mouseDraggingElement === null && this.mouseDownElement === null) {

            //     // check if we are dragging a port. This isn't reported by the selection handler anymore
            //     //
            //     let p = canvas.fromDocumentToCanvasCoordinate(canvas.mouseDownX + (dx / canvas.zoomFactor), canvas.mouseDownY + (dy / canvas.zoomFactor))
            //     let figure = canvas.getBestFigure(p.x, p.y)

            //     if (figure === null) {
            //         let area = canvas.getScrollArea()
            //         let zoom = canvas.getZoom()
            //         area.scrollTop(area.scrollTop() - dy2 / zoom)
            //         area.scrollLeft(area.scrollLeft() - dx2 / zoom)
            //     }
            // }
        }
    })
