import draw2d from "draw2d";
import cuid from 'cuid'
import PubSub from 'pubsub-js'
import { menuItem } from "../../common/Menus";
import Colors from "./Colors";
import Label from "./Label";
import { LabelEditor } from './LabelEditor';


const defaultTextWidth = 230

export default class Connection extends draw2d.Connection {
    nameLabel = null
    descriptionLabel = null

    getName = () => this.nameLabel?.text ?? ''
    getDescription = () => this.descriptionLabel?.text ?? ''


    constructor(name, description, src, srcPortName, dst, dstPortName, id) {
        id = id ?? cuid()
        super({ id: id, stroke: 1 })

        name = name ?? ''
        description = description ?? ''

        if (src !== undefined) {
            const srcPort = src.getPort(srcPortName)
            const dstPort = dst.getPort(dstPortName)
            this.setSource(srcPort)
            this.setTarget(dstPort)
        }

        this.on("contextmenu", (s, e) => { })
        this.on('select', () => this.showConfig())
        this.on('unselect', () => this.hideConfig())

        this.setColor(Colors.connectionColor)
        const cr = new draw2d.layout.connection.InteractiveManhattanConnectionRouter()
        this.setRouter(cr);

        // const selectionPolicy = cr.editPolicy.find(p => p instanceof draw2d.policy.figure.RectangleSelectionFeedbackPolicy)
        // if (selectionPolicy != null) {
        //     selectionPolicy.createResizeHandle = (owner, type) => {
        //         return new draw2d.ResizeHandle({ owner: owner, type: type, width: 15, height: 15 });
        //     }
        // }
        this.addArrow()
        this.addLabels(name, description)
        this.addConfigIcon()
        this.hideConfig()
    }

    static deserialize(canvas, c) {
        // console.log('Deserialize', c)
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

        // Serializing the vertices
        let v = this.vertices.asArray().map(v => ({ x: v.x, y: v.y }))
        if (v.length === 2) {
            // For some reason, serializing InteractiveManhattanConnectionRouter with a straight line
            // will cause bug when deserializing. So a middle point is inserted.
            if (v[0].x === v[1].x) {
                const mp = { x: v[0].x, y: (v[0].y + v[1].y) / 2 }
                v.splice(1, 0, mp)
                v.splice(1, 0, mp)
            } else if (v[0].y === v[1].y) {
                const mp = { x: (v[0].x + v[1].x) / 2, y: v[0].y }
                v.splice(1, 0, mp)
                v.splice(1, 0, mp)
            }
        }

        const c = {
            id: this.id,
            src: this.sourcePort.parent.id,
            srcPort: this.sourcePort.name,
            srcGrp: srcGrp,
            trg: this.targetPort.parent.id,
            trgPort: this.targetPort.name,
            trgGrp: trgGrp,
            v: v,
            name: this.getName(),
            description: this.getDescription()
        }
        //console.log('Serialize', c)
        return c
    }

    addLabels(name, description) {
        const nameBackground = !name ? 'none' : Colors.canvasBackground
        const descriptionBackground = !description ? 'none' : Colors.canvasBackground

        this.nameLabel = new Label(defaultTextWidth, {
            text: name, stroke: 0,
            fontSize: 9, bold: true,
            fontColor: Colors.canvasText, bgColor: nameBackground,
        })

        this.nameLabel.installEditor(new LabelEditor(this));
        this.add(this.nameLabel, new ConnectionNameLabelLocator(this));

        this.descriptionLabel = new Label(defaultTextWidth, {
            text: description, stroke: 0,
            fontSize: 9, bold: false,
            fontColor: Colors.canvasText, bgColor: descriptionBackground,
        })

        this.descriptionLabel.installEditor(new LabelEditor(this));
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
            menuItem('Edit label', () => this.nameLabel.editor.start(this)),
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

    addConfigIcon() {
        const iconColor = Colors.getNodeFontColor(this.colorName)
        this.configIcon = new draw2d.shape.icon.Run({
            width: 16, height: 16, color: iconColor, bgColor: 'none',
        })
        //this.configIcon.on("click", () => { console.log('click') })

        this.configBkr = new draw2d.shape.basic.Rectangle({
            bgColor: Colors.buttonBackground, alpha: 0.7, width: 20, height: 20, radius: 3, stroke: 0.1,
        });
        this.configBkr.on("click", this.showConfigMenu)

        this.add(this.configBkr, new ConfigBackgroundLocator())
        this.add(this.configIcon, new ConfigIconLocator())
    }

    showConfigMenu = () => {
        const f = this.configIcon
        const { x, y } = this.canvas.fromCanvasToDocumentCoordinate(f.x + f.getWidth(), f.y)
        PubSub.publish('canvas.TuneSelected', { x: x - 20, y: y + 5 })
    }

    showConfig() {
        this.configBkr?.setVisible(true)
        this.configBkr.toFront()
        this.configIcon?.setVisible(true)
        this.configIcon.toFront()
    }

    hideConfig() {
        this.configBkr?.setVisible(false)
        this.configIcon?.setVisible(false)
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
        const yOffset = conn.getDescription() === '' ? 0 : 6

        target.setPosition(x, y - yOffset)
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

        const yOffset = conn.getName() === '' ? 0 : +7
        target.setPosition(x, y + yOffset)
    }
}

class ConfigIconLocator extends draw2d.layout.locator.ConnectionLocator {
    relocate(index, target) {
        let conn = target.getParent()
        let points = conn.getVertices()

        let segmentIndex = points.getSize() - 2
        if (points.getSize() <= segmentIndex + 1)
            return

        let p1 = points.get(segmentIndex)
        let p2 = points.get(segmentIndex + 1)

        const x = p2.x
        const y = p2.y

        let xOffset = -27
        let yOffset = -25
        if (p1.y !== p2.y) {
            xOffset = -26
            yOffset = -30
        }

        target.setPosition(x + xOffset, y + yOffset)
    }
}

class ConfigBackgroundLocator extends draw2d.layout.locator.ConnectionLocator {
    relocate(index, target) {
        let conn = target.getParent()
        let points = conn.getVertices()

        let segmentIndex = points.getSize() - 2
        if (points.getSize() <= segmentIndex + 1)
            return

        let p1 = points.get(segmentIndex)
        let p2 = points.get(segmentIndex + 1)

        const x = p2.x
        const y = p2.y

        let xOffset = -27
        let yOffset = -25
        if (p1.y !== p2.y) {
            xOffset = -26
            yOffset = -30
        }

        target.setPosition(x + xOffset - 2, y + yOffset - 2)
    }
}


// class ConfigIconLocator extends draw2d.layout.locator.PortLocator {
//     relocate(index, figure) {
//         const parent = figure.getParent()
//         this.applyConsiderRotation(figure, parent.getWidth() - 11, - 28);
//     }
// }


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