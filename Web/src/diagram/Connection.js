import draw2d from "draw2d";
import Colors from "./colors";


export default class Connection extends draw2d.Connection {
    constructor(description, src, srcPortName, dst, dstPortName) {
        super()

        description = description ?? 'Description'
        if (src !== undefined) {
            const srcPort = src.getPort(srcPortName)
            const dstPort = dst.getPort(dstPortName)
            this.setSource(srcPort)
            this.setTarget(dstPort)
        }

        this.setColor(Colors.connectionColor)
        this.setRouter(new draw2d.layout.connection.VertexRouter());

        const arrow = new draw2d.decoration.connection.ArrowDecorator()
        arrow.setBackgroundColor(this.getColor())
        arrow.setDimension(12, 12)
        this.targetDecorator = arrow

        this.descriptionLabel = new draw2d.shape.basic.Text({
            text: description, stroke: 0,
            fontSize: 14, bold: false,
            fontColor: Colors.canvasText, bgColor: Colors.canvasBackground,
        })

        this.descriptionLabel.installEditor(new draw2d.ui.LabelInplaceEditor());
        this.add(this.descriptionLabel, new draw2d.layout.locator.ManhattanMidpointLocator(this));

    }

    static deserialize(canvas, c) {
        console.log('c', c)
        const src = canvas.getFigure(c.src)
        const trg = canvas.getFigure(c.trg)
        return new Connection(c.description, src, c.srcPort, trg, c.trgPort)
    }

    serialize() {
        const l = this.getPersistentAttributes();
        return {
            src: l.source.node,
            srcPort: l.source.port,
            trg: l.target.node,
            trgPort: l.target.port,
            v: l.vertex,
            description: this.descriptionLabel?.text ?? ''
        }
    }
}
