import draw2d from "draw2d";
import { Tweenable } from "shifty"
import { getNodeBorderColor, getNodeColor, getNodeFontColor } from "./colors";
import { connectionColor } from "./connections";
import { defaultGroupNodeWidth, nodeType, userType, externalType } from "./figures";


export const InnerDiagram = draw2d.SetFigure.extend({
    NAME: "InnerDiagram",

    init: function (attr, canvasData) {
        this._super(attr);
        this.canvasData = canvasData
    },

    createSet: function () {
        var set = this.canvas.paper.set()

        // // Set the group node


        console.log('this.figures', this.canvasData.figures)
        const dx = - this.canvasData.box.x
        const dy = - this.canvasData.box.y
        this.canvasData.figures.forEach(f => this.deserializeFigure(set, f, dx, dy))
        this.canvasData.connections.forEach(c => this.deserializeConnection(set, c, dx, dy))

        const b = set.getBBox()

        console.log('fig', this.width, this.height)

        const wh = this.width / this.height

        let width = 0
        let height = 0


        console.log('wh', wh)

        if (wh < 1) {
            if (b.x2 < b.y2) {
                // OK
                width = b.y2 * wh
                height = b.y2
            } else {
                // OK
                width = b.x2
                height = b.x2 / wh
            }
        } else {
            if (b.x2 < b.y2) {
                width = b.y2 * wh
                height = b.y2
            } else {
                width = b.x2
                height = b.x2 / wh
            }
        }


        // const w = 
        // if 
        // if (b.width/wh <

        // const m = 100
        set.push(this.rect({
            x: 0, y: 0, width: width, height: height,
            "stroke-width": "1", 'stroke-dasharray': '- ', r: 5, fill: 'none'
        }))

        return set;
    },


    deserializeFigure: function (set, f, x, y) {
        // console.log('figure', f)
        switch (f.type) {
            case nodeType:
            case userType:
            case externalType:
                set.push(this.node(f.x + x, f.y + y, f.w, f.h, f.color))
                set.push(this.nodeName(f.x + x, f.y + y, f.w, f.name, f.color))
                break;
            default:
                return null
            //throw new Error('Unexpected node typw!');
        }
    },

    deserializeConnection: function (set, c, x, y) {
        console.log('connection', c)
        let pathText = null
        c.v.forEach(v => {
            if (pathText === null) {
                pathText = `M${v.x + x},${v.y + y}`
            } else {
                pathText = pathText + `L${v.x + x},${v.y + y}`
            }
        })

        const path = this.canvas.paper.path(pathText);
        path.attr({ "stroke-width": 2, "stroke": connectionColor })

        set.push(path)
    },



    text: function (attr) {
        const f = this.canvas.paper.text()
        f.attr(attr)
        return f
    },

    nodeName: function (x, y, w, name, colorName) {
        const fontColor = '#' + getNodeFontColor(colorName).hex()
        const f = this.canvas.paper.text()
        f.attr({
            x: w / 2 + x, y: y + 25, text: name, fill: fontColor,
            'font-size': 20, 'font-weight': 'bold'
        })
        return f
    },



    node: function (x, y, w, h, colorName) {
        const color = '#' + getNodeColor(colorName).hex()
        const borderColor = '#' + getNodeBorderColor(colorName).hex()
        const f = this.canvas.paper.rect()
        f.attr({
            x: x, y: y, width: w, height: h,
            "stroke-width": 1, r: 5,
            fill: color, stroke: borderColor
        })
        return f
    },


    rect: function (attr) {
        const f = this.canvas.paper.rect()
        f.attr(attr)
        return f
    }

});



export const moveAndZoomToShowInnerDiagram = (figure, done) => {
    moveToShowInnerDiagram(figure, () => zoomToShowInnerDiagram(figure, done))
}

const moveToShowInnerDiagram = (figure, done) => {
    const canvas = figure.getCanvas()
    const area = canvas.getScrollArea()

    // Calculate how to scroll from current point to get figure to the center near the top
    const zoom = canvas.zoomFactor
    const sourcePoint = { x: area.scrollLeft() * zoom, y: area.scrollTop() * zoom }
    const targetPoint = getTargetScrollPoint(canvas, figure, zoom)

    // Scroll step by step from source point to target point
    const tweenable = new Tweenable()
    tweenable.tween({
        from: sourcePoint,
        to: targetPoint,
        duration: 500,
        easing: "easeOutSine",
        step: state => {
            area.scrollLeft(state.x / zoom)
            area.scrollTop(state.y / zoom)
        },
        finish: data => {
            done()
        }
    })
}



const zoomToShowInnerDiagram = (figure, done) => {
    const canvas = figure.getCanvas()
    const area = canvas.getScrollArea()

    const zoom = canvas.zoomFactor

    const targetZoom = figure.width / defaultGroupNodeWidth

    const tweenable = new Tweenable()
    tweenable.tween({
        from: { zoom: zoom },
        to: { zoom: targetZoom },
        duration: 500,
        easing: "easeOutSine",
        step: state => {
            canvas.setZoom(state.zoom, false)

            const tp = getTargetScrollPoint(canvas, figure, state.zoom)
            area.scrollLeft(tp.x / state.zoom)
            area.scrollTop(tp.y / state.zoom)
        },
        finish: data => {
            done()
        }
    })
}

const getTargetScrollPoint = (canvas, figure, zoom) => {
    const figurePoint = { x: figure.x + figure.width / 2, y: figure.y }
    const canvasPoint = { x: zoom * canvas.getWidth() / 2, y: zoom * 250 }

    return { x: figurePoint.x - canvasPoint.x, y: figurePoint.y - canvasPoint.y }
}