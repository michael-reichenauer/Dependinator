import draw2d from "draw2d";
import { Item } from "../common/ContextMenu";
import Colors from "./colors";

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
        this.descriptionLabel = new DescriptionText({
            text: description, stroke: 0,
            fontSize: 14, bold: false,
            fontColor: Colors.canvasText, bgColor: Colors.canvasBackground,
        })
        // this.descriptionLabel.setResizeable(true)
        // this.descriptionLabel.setSelectable(true)
        this.descriptionLabel.installEditor(new draw2d.ui.LabelInplaceEditor());
        this.add(this.descriptionLabel, new draw2d.layout.locator.ManhattanMidpointLocator(this));
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


class DescriptionText extends draw2d.shape.basic.Text {
    wrappedTextAttr(text, width) {
        let words = text.split(" ")
        if (this.canvas === null || words.length === 0) {
            return { text: text, width: width, height: 20 }
        }

        if (this.cachedWrappedAttr === null) {
            let abc = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"
            let svgText = this.canvas.paper.text(0, 0, "").attr({ ...this.calculateTextAttr(), ...{ text: abc } })

            // get a good estimation of a letter width...not correct but this is working for the very first draft implementation
            let letterWidth = svgText.getBBox(true).width / abc.length

            let s = [words[0]], x = s[0].length * letterWidth
            let w = null
            for (let i = 1; i < words.length; i++) {
                w = words[i]
                let l = w.length * letterWidth
                if ((x + l) > defaultTextWidth) {
                    s.push("\n")
                    x = l
                } else {
                    s.push(" ")
                    x += l
                }
                s.push(w)
            }
            // set the wrapped text and get the resulted bounding box
            //
            svgText.attr({ text: s.join("") })
            let bbox = svgText.getBBox(true)
            svgText.remove()
            this.cachedWrappedAttr = {
                text: s.join(""),
                width: (Math.max(width, bbox.width) + this.padding.left + this.padding.right),
                height: (bbox.height + this.padding.top + this.padding.bottom)
            }
        }
        return this.cachedWrappedAttr
    }
}