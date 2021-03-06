import draw2d from "draw2d";
import { Tweenable } from "shifty"
import { canvasBackground, getNodeBorderColor, getNodeColor, getNodeFontColor } from "./colors";
import { connectionColor } from "./connections";
import { nodeType, userType, externalType, groupType } from "./figures";


export const InnerDiagram = draw2d.SetFigure.extend({
    NAME: "InnerDiagram",

    init: function (attr, canvasData, name) {
        this._super(attr);
        this.name = name
        this.canvasData = canvasData

        if (this.canvasData == null) {
            this.canvasData = {
                zoom: 1,
                box: { x: 5090, y: 5250, w: 1000, h: 800 },
                figures: [{ type: groupType, x: 5090, y: 5250, w: 1000, h: 800, name: name }],
                connections: [],
            }
        }
    },

    createSet: function () {
        const margin = 30
        const set = this.canvas.paper.set()
        const diagramBox = this.canvasData.box

        const figureAspectRatio = this.width / this.height
        const diagramAspectRatio = diagramBox.w / diagramBox.h

        // Adjust inner diagram width and height to fit and keep same aspect ratio as figure
        let diagramWidth = 0
        let diagramHeight = 0
        if (figureAspectRatio > diagramAspectRatio) {
            diagramWidth = diagramBox.w * (figureAspectRatio / diagramAspectRatio)
            diagramHeight = diagramBox.h
        } else {
            diagramWidth = diagramBox.w
            diagramHeight = diagramBox.h * (diagramAspectRatio / figureAspectRatio)
        }

        // Add some margin around the inner diagram
        diagramWidth = diagramWidth + margin * 2
        diagramHeight = diagramHeight + margin * 2 - 30

        // Draw an invisible rect to ensure diagram keeps aspect rate within the figure
        set.push(this.rect({
            x: 0, y: 0, width: diagramWidth, height: diagramHeight,
            "stroke-width": "0", fill: 'none'
        }))


        // Center diagram within the figure inner diagram rect
        let dx = - diagramBox.x
        if (diagramBox.w < diagramWidth) {
            dx = dx + (diagramWidth - diagramBox.w) / 2
        }
        let dy = - diagramBox.y + 13   // adjust +10 for group name
        if (diagramBox.h < diagramHeight) {
            dy = dy + (diagramHeight - diagramBox.h) / 2
        }

        // Add the inner diagram figures and connections (centered within figure)
        this.canvasData.figures.forEach(f => this.addFigure(set, f, dx, dy))
        this.canvasData.connections.forEach(c => this.addConnection(set, c, dx, dy))

        // Set the inner diagram zoom factor, used when zooming outer diagram before showing inner
        console.log('dx dy', dx, dy, diagramBox)
        this.dx = dx
        this.dy = dy
        this.innerZoom = this.width / diagramWidth
        return set;
    },


    addFigure: function (set, f, dx, dy) {
        // console.log('figure', f)
        switch (f.type) {
            case nodeType:
            case userType:
            case externalType:
                set.push(this.createNode(f.x + dx, f.y + dy, f.w, f.h, f.color))
                set.push(this.createNodeName(f.x + dx, f.y + dy, f.w, f.name, f.color))
                break;
            case groupType:
                set.push(this.createGroupNode(f.x + dx, f.y + dy, f.w, f.h))
                set.push(this.createGroupName(f.x + dx, f.y + dy, f.w, f.name, f.color))
                break;
            default:
                // Ignore other types
                break
        }
    },

    addConnection: function (set, c, dx, dy) {
        let pathText = null
        c.v.forEach(v => {
            if (pathText === null) {
                pathText = `M${v.x + dx},${v.y + dy}`
            } else {
                pathText = pathText + `L${v.x + dx},${v.y + dy}`
            }
        })

        const path = this.canvas.paper.path(pathText);
        path.attr({ "stroke-width": 2, "stroke": connectionColor })

        set.push(path)
    },

    createNodeName: function (x, y, w, name, colorName) {
        const fontColor = '#' + getNodeFontColor(colorName).hex()
        const f = this.canvas.paper.text()
        f.attr({
            x: w / 2 + x, y: y + 25, text: name, fill: fontColor,
            'font-size': 20, 'font-weight': 'bold'
        })
        return f
    },

    createGroupName: function (x, y, w, name, colorName) {
        const fontColor = '#' + canvasBackground.getIdealTextColor().hex()
        const f = this.canvas.paper.text()
        f.attr({
            x: x + 42, y: y - 14, text: name, fill: fontColor,
            'font-size': 30, 'font-weight': 'bold'
        })
        return f
    },

    createNode: function (x, y, w, h, colorName) {
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


    createGroupNode: function (x, y, w, h) {
        const borderColor = '#' + canvasBackground.getIdealTextColor().hex()
        const f = this.canvas.paper.rect()
        f.attr({
            x: x, y: y, width: w, height: h,
            r: 5, "stroke-width": "1", 'stroke-dasharray': '- ', fill: 'none',
            stroke: borderColor
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
    const targetZoom = figure.innerZoom

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
    const canvasPoint = { x: (canvas.getWidth() / 2) * zoom, y: 250 * zoom }

    return { x: figurePoint.x - canvasPoint.x, y: figurePoint.y - canvasPoint.y }
}