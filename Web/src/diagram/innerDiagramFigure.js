import draw2d from "draw2d";
import { canvasBackground, getNodeBorderColor, getNodeColor, getNodeFontColor } from "./colors";
import { connectionColor } from "./connections";
import { externalType, groupType, nodeType, userType } from "./figures";



export const InnerDiagram = draw2d.SetFigure.extend({
    NAME: "InnerDiagram",

    init: function (attr, canvasData, name) {
        this._super(attr);
        this.name = name
        this.canvasData = canvasData
        this.clicks = 0

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
        const set = this.canvas.paper.set()
        const diagramBox = this.canvasData.box

        // Calculate diagram size with some margin
        const margin = 20
        let diagramWidth = diagramBox.w + margin
        let diagramHeight = diagramBox.h + margin

        // Calculate aspect ratios for containing figure and diagram
        const figureAspectRatio = this.width / this.height
        const diagramAspectRatio = diagramWidth / diagramHeight

        // Adjust inner diagram width and height to fit diagram and still keep same aspect ratio as figure
        if (figureAspectRatio > diagramAspectRatio) {
            diagramWidth = diagramWidth * (figureAspectRatio / diagramAspectRatio)
        } else {
            diagramHeight = diagramHeight * (diagramAspectRatio / figureAspectRatio)
        }

        // Draw an invisible rect to ensure diagram keeps aspect rate within the figure
        set.push(this.rect({
            x: 0, y: 0, width: diagramWidth, height: diagramHeight,
            "stroke-width": "0", fill: 'none'
        }))

        // Center diagram within the figure inner diagram rect
        let dx = (diagramWidth - diagramBox.w) / 2 - diagramBox.x
        let dy = (diagramHeight - diagramBox.h) / 2 - diagramBox.y



        // Add the inner diagram figures and connections (centered within figure)
        this.canvasData.figures.forEach(f => this.addFigure(set, f, dx, dy))
        this.canvasData.connections.forEach(c => this.addConnection(set, c, dx, dy))

        // Set the inner diagram zoom factor, used when zooming outer diagram before showing inner
        this.innerZoom = this.width / diagramWidth
        this.marginX = (diagramWidth - diagramBox.w) / 2
        this.marginY = (diagramHeight - diagramBox.h) / 2
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
            x: x + 42, y: y + 16, text: name, fill: fontColor,
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

