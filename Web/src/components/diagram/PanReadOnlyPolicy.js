import draw2d from "draw2d";



export let PanReadOnlyPolicy = draw2d.policy.canvas.SelectionPolicy.extend(
    {
        NAME: "PanReadOnlyPolicy",

        togglePanPolicy: null,
        createItem: null,
        canvas: null,

        init: function (togglePanPolicy, createItem, attr, setter, getter) {
            this._super(attr, setter, getter)
            this.togglePanPolicy = togglePanPolicy
            this.createItem = createItem
        },

        onInstall: function (canvas) {
            this._super(canvas)
            this.canvas = canvas
            canvas.isReadOnlyMode = true
            canvas.getAllPorts().each(function (i, port) {
                port.setVisible(false)
            })
        },

        onUninstall: function (canvas) {
            this._super(canvas)
        },


        onClick: function (figure, mouseX, mouseY, shiftKey, ctrlKey) {
            if (figure === null) {
                return
            }

            this.togglePanPolicy(figure)
        },

        onDoubleClick: function (figure, mouseX, mouseY, shiftKey, ctrlKey) {
            if (figure !== null) {
                return
            }

            this.createItem(mouseX, mouseY, shiftKey, ctrlKey)
        },

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
            let area = canvas.getScrollArea()
            let zoom = canvas.getZoom()

            //console.log("Pan:", dx, dy, dx2, dy2, shiftKey, ctrlKey, area.scrollLeft(), area.scrollTop(), zoom)
            area.scrollLeft(area.scrollLeft() - dx2 / zoom)
            area.scrollTop(area.scrollTop() - dy2 / zoom)
        }
    })
