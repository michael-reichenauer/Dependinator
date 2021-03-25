import draw2d from "draw2d";
import { Item } from "../common/ContextMenu";
import Colors from "./Colors";
import { Label } from "./Label";


const defaultTextWidth = 230

export default class Connection extends draw2d.Connection {
    descriptionLabel = null

    constructor(description, src, srcPortName, dst, dstPortName, id) {
        id = id ?? draw2d.util.UUID.create()
        super({ id: id })

        description = description ?? 'Description'
        if (src !== undefined) {
            const srcPort = src.getPort(srcPortName)
            const dstPort = dst.getPort(dstPortName)
            this.setSource(srcPort)
            this.setTarget(dstPort)
        }

        this.setColor(Colors.connectionColor)
        // this.setRouter(new draw2d.layout.connection.VertexRouter());
        this.setRouter(new draw2d.layout.connection.DirectRouter());

        this.addArrow()
        this.addLabels(description)
    }

    static deserialize(canvas, c) {
        const src = canvas.getFigure(c.src)
        const trg = canvas.getFigure(c.trg)
        return new Connection(c.description, src, c.srcPort, trg, c.trgPort, c.id)
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
            description: this.descriptionLabel?.text ?? ''
        }
    }

    addLabels(description) {
        this.descriptionLabel = new Label(defaultTextWidth, {
            text: description, stroke: 0,
            fontSize: 14, bold: false,
            fontColor: Colors.canvasText, bgColor: Colors.canvasBackground,
        })
        // this.descriptionLabel.setResizeable(true)
        // this.descriptionLabel.setSelectable(true)
        this.descriptionLabel.installEditor(new draw2d.ui.LabelInplaceEditor());
        this.add(this.descriptionLabel, new ConnectionLabelLocator(this));
    }

    addArrow() {
        const arrow = new draw2d.decoration.connection.ArrowDecorator()
        arrow.setBackgroundColor(this.getColor())
        arrow.setDimension(12, 12)
        this.targetDecorator = arrow
    }

    getContextMenuItems(x, y) {
        const menuItems = [
            new Item('To front', () => this.toFront()),
            new Item('To back', () => this.toBack()),
        ]

        return menuItems
    }

    setDescription(description) {
        this.descriptionLabel?.setText(description)
    }
}


class ConnectionLabelLocator extends draw2d.layout.locator.ConnectionLocator {
    relocate(index, target) {
        let conn = target.getParent()
        let points = conn.getVertices()

        let segmentIndex = Math.floor((points.getSize() - 2) / 2)
        if (points.getSize() <= segmentIndex + 1)
            return

        let p1 = points.get(segmentIndex)
        let p2 = points.get(segmentIndex + 1)

        target.setPosition(
            ((p2.x - p1.x) / 2 + p1.x - target.getWidth() / 2) | 0,
            ((p2.y - p1.y) / 2 + p1.y - target.getHeight() / 2) | 0)
    }
}
