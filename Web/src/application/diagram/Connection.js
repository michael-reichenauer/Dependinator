import draw2d from "draw2d";
import cuid from 'cuid'
import { menuItem } from "../../common/Menus";
import Colors from "./Colors";
import Label from "./Label";


const defaultTextWidth = 230

export default class Connection extends draw2d.Connection {
    nameLabel = null
    descriptionLabel = null

    getName = () => this.nameLabel?.text ?? ''
    getDescription = () => this.descriptionLabel?.text ?? ''


    constructor(name, description, src, srcPortName, dst, dstPortName, id) {
        id = id ?? cuid()
        super({ id: id, stroke: 1 })

        name = name ?? 'Name'
        description = description ?? 'description'

        if (src !== undefined) {
            const srcPort = src.getPort(srcPortName)
            const dstPort = dst.getPort(dstPortName)
            this.setSource(srcPort)
            this.setTarget(dstPort)
        }

        this.setColor(Colors.connectionColor)
        this.setRouter(new draw2d.layout.connection.InteractiveManhattanConnectionRouter());

        this.addArrow()
        this.addLabels(name, description)
    }

    static deserialize(canvas, c) {
        const src = canvas.getFigure(c.src)
        const trg = canvas.getFigure(c.trg)
        const connection = new Connection(c.name, c.description, src, c.srcPort, trg, c.trgPort, c.id)

        // Restore vertices
        for (let i = 1; i < c.v.length - 1; i++) {
            const v = c.v[i];
            connection.insertVertexAt(i, v.x, v.y)
        }
        connection.getRouter().verticesSet(connection)

        return connection
    }

    serialize() {
        const srcGrp = this.sourcePort.parent.group != null
        const trgGrp = this.targetPort.parent.group != null

        return {
            id: this.id,
            src: this.sourcePort.parent.id,
            srcPort: this.sourcePort.name,
            srcGrp: srcGrp,
            trg: this.targetPort.parent.id,
            trgPort: this.targetPort.name,
            trgGrp: trgGrp,
            v: this.vertices.asArray().map(v => ({ x: v.x, y: v.y })),
            name: this.getName(),
            description: this.getDescription()
        }
    }

    addLabels(name, description) {
        this.nameLabel = new Label(defaultTextWidth, {
            text: 'name', stroke: 0,
            fontSize: 9, bold: true,
            fontColor: Colors.canvasText, bgColor: Colors.canvasBackground,
        })

        this.nameLabel.installEditor(new draw2d.ui.LabelInplaceEditor());
        this.add(this.nameLabel, new ConnectionNameLabelLocator(this));

        this.descriptionLabel = new Label(defaultTextWidth, {
            text: description, stroke: 0,
            fontSize: 9, bold: false,
            fontColor: Colors.canvasText, bgColor: Colors.canvasBackground,
        })

        this.descriptionLabel.installEditor(new draw2d.ui.LabelInplaceEditor());
        this.add(this.descriptionLabel, new ConnectionDescriptionLabelLocator(this));
    }

    addArrow() {
        const arrow = new draw2d.decoration.connection.ArrowDecorator()
        arrow.setBackgroundColor(this.getColor())
        arrow.setDimension(8, 8)
        this.targetDecorator = arrow
    }

    getContextMenuItems(x, y) {
        return [
            menuItem('To front', () => this.toFront()),
            menuItem('To back', () => this.toBack()),
            menuItem('Delete connection', () => this.deleteConnection())
        ]
    }

    deleteConnection() {
        let cmd = new draw2d.command.CommandDelete(this)
        this.getCanvas().getCommandStack().execute(cmd)
    }

    setDescription(description) {
        this.descriptionLabel?.setText(description)
    }

    addSegmentAt(x, y) {
        const cp = this.getCanvas().fromDocumentToCanvasCoordinate(x, y)
        const closestIndex = this.getClosestVertexIndex(cp.x, cp.y)

        let cmd = new draw2d.command.CommandAddVertex(this, closestIndex + 1, cp.x, cp.y)
        this.getCanvas().getCommandStack().execute(cmd)

        // Make sure line is selected so the move handle is ready to be used
        if (this.getCanvas().getSelection().contains(this)) {
            return // nothing to to
        }
        this.select(true) // primary selection
        this.getCanvas().getSelection().setPrimary(this)
    }

    removeSegmentAt(x, y) {
        const cp = this.getCanvas().fromDocumentToCanvasCoordinate(x, y)
        if (this.getVertices().asArray().length < 2) {
            return
        }
        const closestIndex = this.getClosestVertexIndex(cp.x, cp.y)
        let cmd = new draw2d.command.CommandRemoveVertex(this, closestIndex + 1)
        this.getCanvas().getCommandStack().execute(cmd)
    }


    getClosestVertexIndex(x, y) {
        const vertices = this.getVertices().asArray()

        const lineDistances = []
        for (let i = 0; i < vertices.length - 1; i++) {
            const p1 = vertices[i];
            const p2 = vertices[i + 1];
            const dl = this.distToSegment({ x: x, y: y }, { sx: p1.x, sy: p1.y, ex: p2.x, ey: p2.y })
            lineDistances.push(dl)
        }

        let closestIndex = 0
        let closestDistance = lineDistances[0]
        lineDistances.forEach((distance, i) => {
            if (distance < closestDistance) {
                closestIndex = i
                closestDistance = distance
            }
        })

        return closestIndex
    }


    dist(point, x, y) {
        var dx = x - point.x;
        var dy = y - point.y;
        return Math.sqrt(dx * dx + dy * dy);
    }

    distToSegment(point, line) {
        var dx = line.ex - line.sx;
        var dy = line.ey - line.sy;
        var l2 = dx * dx + dy * dy;

        if (l2 === 0) {
            return this.dist(point, line.sx, line.sy);
        }

        let t = ((point.x - line.sx) * dx + (point.y - line.sy) * dy) / l2;
        t = Math.max(0, Math.min(1, t));

        return this.dist(point, line.sx + t * dx, line.sy + t * dy);
    }

}


class ConnectionNameLabelLocator extends draw2d.layout.locator.ConnectionLocator {
    relocate(index, target) {
        let conn = target.getParent()
        let points = conn.getVertices()

        let segmentIndex = Math.floor((points.getSize() - 2) / 2)
        if (points.getSize() <= segmentIndex + 1)
            return

        let p1 = points.get(segmentIndex)
        let p2 = points.get(segmentIndex + 1)

        const x = ((p2.x - p1.x) / 2 + p1.x - target.getWidth() / 2) | 0
        const y = ((p2.y - p1.y) / 2 + p1.y - target.getHeight() / 2) | 0

        target.setPosition(x, y - 6)
    }
}

class ConnectionDescriptionLabelLocator extends draw2d.layout.locator.ConnectionLocator {
    relocate(index, target) {
        let conn = target.getParent()
        let points = conn.getVertices()

        let segmentIndex = Math.floor((points.getSize() - 2) / 2)
        if (points.getSize() <= segmentIndex + 1)
            return

        let p1 = points.get(segmentIndex)
        let p2 = points.get(segmentIndex + 1)

        const x = ((p2.x - p1.x) / 2 + p1.x - target.getWidth() / 2) | 0
        const y = ((p2.y - p1.y) / 2 + p1.y - target.getHeight() / 2) | 0

        target.setPosition(x, y + 6)
    }
}


// class VertexSelectionFeedbackPolicy extends draw2d.policy.line.LineSelectionFeedbackPolicy {
//     NAME = "VertexSelectionFeedbackPolicy"

//     onSelect(canvas, figure, isPrimarySelection) {
//         let startHandle = new draw2d.shape.basic.LineStartResizeHandle(figure)
//         startHandle.setMinWidth(15)

//         let endHandle = new draw2d.shape.basic.LineEndResizeHandle(figure)
//         endHandle.setMinWidth(15)

//         figure.selectionHandles.add(startHandle)
//         figure.selectionHandles.add(endHandle)

//         let points = figure.getVertices()
//         let count = points.getSize() - 1
//         let i = 1
//         for (; i < count; i++) {
//             const handle = new draw2d.shape.basic.VertexResizeHandle(figure, i)
//             handle.setMinWidth(15)
//             handle.setMinHeight(15)
//             figure.selectionHandles.add(handle)
//             //figure.selectionHandles.add(new draw2d.shape.basic.GhostVertexResizeHandle(figure, i - 1))
//         }

//         // figure.selectionHandles.add(new draw2d.shape.basic.GhostVertexResizeHandle(figure, i - 1))
//         figure.selectionHandles.each((i, e) => {
//             e.setDraggable(figure.isResizeable())
//             e.show(canvas)
//         })

//         this.moved(canvas, figure)
//     }
// }