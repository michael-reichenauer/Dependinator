import draw2d from "draw2d";
import Canvas from "./Canvas";
import Colors from "./colors";
import Group from "./Group";
import Node from "./Node";


const groupColor = '#' + Colors.canvasText.hex()

const defaultDiagramData = (name) => {
    // Group node with one node in the center of that group
    const gx = Canvas.defaultWidth / 2
    const gy = Canvas.defaultHeight / 2
    const nx = gx + Group.defaultWidth / 2 - Node.defaultWidth / 2
    const ny = gy + Group.defaultHeight / 2 - Node.defaultHeight / 2

    return {
        zoom: 1,
        figures: [
            { type: Group.groupType, x: gx, y: gy, w: Group.defaultWidth, h: Group.defaultHeight, name: name },
            { type: Node.nodeType, x: nx, y: ny, w: Node.defaultWidth, h: Node.defaultHeight, name: 'Node', color: 'DeepPurple', hasGroup: true }
        ],
        connections: [],
    }
}

export class InnerDiagram extends draw2d.SetFigure {
    NAME = "InnerDiagram"
    static innerPadding = 2

    parent = null

    constructor(parent, canvasData) {
        super({
            width: parent.width - InnerDiagram.innerPadding * 2,
            height: parent.height - InnerDiagram.innerPadding * 2,
            keepAspectRatio: true,
            color: 'none',
            bgColor: Colors.canvasBackground,
            radius: 5,
        });

        this.parent = parent
        this.canvasData = canvasData ?? defaultDiagramData(parent.getName())
    }

    setCanvas(canvas) {
        super.setCanvas(canvas)
        this.shape?.attr({ "cursor": "pointer" })
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
        const diagramBox = this.getGroup(this.canvasData.figures)

        // Calculate diagram size with some margin
        const margin = 80
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
        this.diagramWidth = diagramWidth
        this.diagramHeight = diagramHeight

        // Center diagram within the figure inner diagram rect
        let dx = (diagramWidth - diagramBox.w) / 2 - diagramBox.x
        let dy = (diagramHeight - diagramBox.h) / 2 - diagramBox.y

        // Add the inner diagram figures and connections (centered within figure)
        this.addFigures(set, dx, dy)
        this.addConnections(set, dx, dy)
        //this.addExternalGroupConnections(set)

        // Set the inner diagram zoom factor, used when zooming outer diagram before showing inner
        this.innerZoom = this.width / diagramWidth
        this.marginX = (diagramWidth - diagramBox.w) / 2
        this.marginY = (diagramHeight - diagramBox.h) / 2
        return set;
    }

    getGroup(figures) {
        return figures.find(f => f.type === Group.groupType)
    }

    addFigures(set, dx, dy) {
        this.canvasData.figures.forEach(f => this.addFigure(set, f, dx, dy))
    }

    addConnections(set, dx, dy) {
        this.canvasData.connections.forEach(c => this.addConnection(set, c, dx, dy))
    }

    // addExternalGroupConnections(set) {
    //     // Check which external connections that are active
    //     const dashPattern = '--'
    //     const hasLeftInput = this.parent.getInputPort(0).getConnections().asArray().length > 0
    //     const hasTopInput = this.parent.getInputPort(1).getConnections().asArray().length > 0
    //     const hasRightOutput = this.parent.getOutputPort(0).getConnections().asArray().length > 0
    //     const hasBottomOutput = this.parent.getOutputPort(1).getConnections().asArray().length > 0

    //     // Draw lines for each of the active connections
    //     const { x, y, w, h } = this.groupRect
    //     if (hasLeftInput) {
    //         set.push(this.externalLine(0, y + h / 2, x, y + h / 2), dashPattern)
    //     }
    //     if (hasTopInput) {
    //         set.push(this.externalLine(x + w / 2, 0, x + w / 2, y), dashPattern)
    //     }
    //     if (hasRightOutput) {
    //         set.push(this.externalLine(x + w, y + h / 2, this.diagramWidth, y + h / 2), dashPattern)
    //     }
    //     if (hasBottomOutput) {
    //         set.push(this.externalLine(x + w / 2, y + h, x + w / 2, this.diagramHeight), dashPattern)
    //     }
    // }

    addFigure(set, figure, offsetX, offsetY) {
        switch (figure.type) {
            case Node.nodeType:
            case Node.userType:
            case Node.externalType:
                if (figure.hasGroup) {
                    set.push(this.createNode(figure.x + offsetX, figure.y + offsetY, figure.w, figure.h, figure.color))
                    set.push(this.createNodeName(figure.x + offsetX, figure.y + offsetY, figure.w, figure.name, figure.color))
                }
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
        const { x, y, w, h } = this.groupRect

        if (this.isInternalConnection(connection)) {
            // Connection between 2 internal nodes
            set.push(this.internalLine(connection.v, offsetX, offsetY))
            return
        }

        if (!connection.srcGrp && connection.trgGrp) {
            // External node connected to internal node
            const sp = connection.v[1]
            if (connection.trgPort === 'input0') {
                // External node left of group
                set.push(this.externalLine(0, y + h / 2, sp.x + offsetX, sp.y + offsetY))
            } else {
                // External node above group
                set.push(this.externalLine(x + w / 2, 0, sp.x + offsetX, sp.y + offsetY))
            }
            return
        }

        if (connection.srcGrp && !connection.trgGrp) {
            // Internal node connected to external node
            const tp = connection.v[0]
            if (connection.srcPort === 'output0') {
                // External node right of group
                set.push(this.externalLine(tp.x + offsetX, tp.y + offsetY, this.diagramWidth, y + h / 2, y + h / 2))
            } else {
                // External node below group
                set.push(this.externalLine(tp.x + offsetX, tp.y + offsetY, x + w / 2, this.diagramHeight))
            }
        }

        // Ignoring external node connections
    }

    isInternalConnection(connection) {
        return connection.srcGrp && connection.trgGrp
    }


    isExternalConnection(connection) {
        return (!connection.srcGrp && connection.trgGrp)
            || (connection.srcGrp && !connection.trgGrp)
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
            x: x + 5, y: y - 18, text: name, fill: groupColor,
            'font-size': 40, 'font-weight': 'bold'
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
        this.groupRect = { x: x, y: y, w: w, h: h }
        const f = this.canvas.paper.rect()
        f.attr({
            x: x, y: y, width: w, height: h,
            r: 5, "stroke-width": "1", 'stroke-dasharray': '- ', fill: 'none',
            stroke: groupColor
        })
        return f
    }

    internalLine(vertexes, offsetX, offsetY) {
        let pathText = null

        vertexes.forEach(v => {
            if (pathText === null) {
                pathText = `M${v.x + offsetX},${v.y + offsetY}`
            } else {
                pathText = pathText + `L${v.x + offsetX},${v.y + offsetY}`
            }
        })

        const path = this.canvas.paper.path(pathText);
        path.attr({ "stroke-width": 2, "stroke": Colors.connectionColor, 'arrow-end': 'block-wide-long' })
        return path
    }


    externalLine(sx, sy, tx, ty) {
        const path = this.canvas.paper.path(`M${sx},${sy}L${tx},${ty}`)
        path.attr({ "stroke-width": 4, "stroke": Colors.connectionColor, 'arrow-end': 'block-wide-long' })
        return path
    }

    rect(attr) {
        const f = this.canvas.paper.rect()
        f.attr(attr)
        return f
    }
}
