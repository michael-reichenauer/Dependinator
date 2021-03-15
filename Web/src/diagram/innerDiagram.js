import draw2d from "draw2d";
import Colors from "./colors";
import { connectionColor } from "./connections";
import Group from "./Group";
import Node from "./Node";


const groupColor = '#' + Colors.canvasText.hex()

const defaultDiagramData = (name) => {
    return {
        zoom: 1,
        box: { x: 5090, y: 5250, w: Group.defaultWidth, h: Group.defaultHeight },
        figures: [{ type: Group.groupType, x: 5090, y: 5250, w: Group.defaultWidth, h: Group.defaultHeight, name: name }],
        connections: [],
    }
}

export class InnerDiagram extends draw2d.SetFigure {
    NAME = "InnerDiagram"

    constructor(parent, canvasData) {
        super({
            width: parent.width - 4,
            height: parent.height - 4,
            keepAspectRatio: true,
            color: 'none',
            bgColor: Colors.canvasBackground,
            radius: 5,
        });

        this.canvasData = canvasData ?? defaultDiagramData(parent.getName())
    }

    getDiagramViewCoordinate() {
        const canvasZoom = this.canvas.zoomFactor

        // get the diagram margin in canvas coordinates
        const imx = this.marginX * this.innerZoom
        const imy = this.marginY * this.innerZoom

        // get the inner diagram pos in canvas view coordinates
        const outerScrollPos = this.getScrollInCanvasCoordinate()
        const vx = (this.getAbsoluteX() + imx - outerScrollPos.left) / canvasZoom
        const vy = (this.getAbsoluteY() + imy - outerScrollPos.top) / canvasZoom
        return { left: vx, top: vy }
    }


    getScrollInCanvasCoordinate() {
        const area = this.canvas.getScrollArea()
        return { left: area.scrollLeft() * this.canvas.zoomFactor, top: area.scrollTop() * this.canvas.zoomFactor }
    }

    createSet() {
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
    }


    addFigure(set, figure, offsetX, offsetY) {
        switch (figure.type) {
            case Node.nodeType:
            case Node.userType:
            case Node.externalType:
                set.push(this.createNode(figure.x + offsetX, figure.y + offsetY, figure.w, figure.h, figure.color))
                set.push(this.createNodeName(figure.x + offsetX, figure.y + offsetY, figure.w, figure.name, figure.color))
                break;
            case Group.groupType:
                set.push(this.createGroupNode(figure.x + offsetX, figure.y + offsetY, figure.w, figure.h))
                set.push(this.createGroupName(figure.x + offsetX, figure.y + offsetY, figure.w, figure.name))
                break;
            default:
                // Ignore other types
                break
        }
    }

    addConnection(set, connection, offsetX, offsetY) {
        let pathText = null
        connection.v.forEach(v => {
            if (pathText === null) {
                pathText = `M${v.x + offsetX},${v.y + offsetY}`
            } else {
                pathText = pathText + `L${v.x + offsetX},${v.y + offsetY}`
            }
        })

        const path = this.canvas.paper.path(pathText);
        path.attr({ "stroke-width": 2, "stroke": connectionColor })

        set.push(path)
    }

    createNodeName(x, y, w, name, colorName) {
        const fontColor = Colors.getNodeFontHexColor(colorName)
        const f = this.canvas.paper.text()
        f.attr({
            x: w / 2 + x, y: y + 25, text: name, fill: fontColor,
            'font-size': 20, 'font-weight': 'bold'
        })
        return f
    }

    createGroupName(x, y, w, name) {
        const f = this.canvas.paper.text()
        f.attr({
            'text-anchor': 'start',
            x: x + 5, y: y + 16, text: name, fill: groupColor,
            'font-size': 30, 'font-weight': 'bold'
        })
        return f
    }

    createNode(x, y, w, h, colorName) {
        const color = Colors.getNodeHexColor(colorName)
        const borderColor = Colors.getNodeBorderHexColor(colorName)
        const f = this.canvas.paper.rect()
        f.attr({
            x: x, y: y, width: w, height: h,
            "stroke-width": 1, r: 5,
            fill: color, stroke: borderColor
        })
        return f
    }


    createGroupNode(x, y, w, h) {
        const f = this.canvas.paper.rect()
        f.attr({
            x: x, y: y, width: w, height: h,
            r: 5, "stroke-width": "1", 'stroke-dasharray': '- ', fill: 'none',
            stroke: groupColor
        })
        return f
    }


rect(attr) {
    const f = this.canvas.paper.rect()
    f.attr(attr)
    return f
}
}
