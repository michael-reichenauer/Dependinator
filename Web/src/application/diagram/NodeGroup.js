import draw2d from "draw2d";
import cuid from 'cuid'
import { menuItem } from "../../common/Menus";
import Colors from "./Colors";

const defaultOptions = () => {
    return {
        id: cuid(),
        width: NodeGroup.defaultWidth,
        height: NodeGroup.defaultHeight,
        name: 'Group',
    }
}


export default class NodeGroup extends draw2d.shape.basic.Rectangle {
    static nodeType = 'nodeGroup'
    static defaultWidth = 500
    static defaultHeight = 340

    type = NodeGroup.nodeType
    nameLabel = null

    getName = () => this.nameLabel?.text ?? ''


    constructor(options) {
        const o = { ...defaultOptions(), ...options }

        super({
            id: o.id,
            width: o.width, height: o.height, stroke: 0.5,
            bgColor: Colors.canvasBackground, alpha: 0.4, color: Colors.canvasText,
            radius: 5, glow: true, dasharray: '- ',
        });

        this.addLabels(o.name)

        // this.on("click", (s, e) => console.log('click node'))
        this.on("dblclick", (s, e) => { })
        this.on('resize', (s, e) => { })

        // Adjust selection handle sizes
        const selectionPolicy = this.editPolicy
            .find(p => p instanceof draw2d.policy.figure.RectangleSelectionFeedbackPolicy)
        if (selectionPolicy != null) {
            selectionPolicy.createResizeHandle = (owner, type) => {
                return new draw2d.ResizeHandle({ owner: owner, type: type, width: 15, height: 15 });
            }
        }
    }

    static deserialize(data) {
        return new NodeGroup({ id: data.id, width: data.w, height: data.h, name: data.name })
    }

    serialize() {
        try {
            return {
                type: this.type, id: this.id, x: this.x, y: this.y, w: this.width, h: this.height,
                name: this.getName(), hasGroup: this.group != null
            }
        } catch (error) {
            console.error('error', error)
        }

    }

    getContextMenuItems(x, y) {
        return [
            menuItem('To back', () => this.toBack()),
            menuItem('Delete node', () => this.canvas.runCmd(new draw2d.command.CommandDelete(this)), this.canDelete)
        ]
    }

    setName(name) {
        this.nameLabel?.setText(name)
    }

    setDefaultSize() {
        this.setWidth(NodeGroup.defaultWidth)
        this.setHeight(NodeGroup.defaultHeight)
    }

    toBack() {
        super.toBack()
        const group = this.getCanvas()?.group
        group?.toBack()

    }

    handleResize() {
        this.nameLabel?.setTextWidth(this.width)
        this.nameLabel?.repaint()
    }

    setChildrenVisible(isVisible) {
        this.nameLabel?.setVisible(isVisible)
    }

    addLabels = (name) => {
        this.nameLabel = new draw2d.shape.basic.Label({
            text: name, stroke: 0, fontSize: 14, fontColor: Colors.canvasText, bold: false,
        })

        this.nameLabel.installEditor(new draw2d.ui.LabelInplaceEditor());
        this.nameLabel.labelLocator = new LabelLocator()
        this.add(this.nameLabel, this.nameLabel.labelLocator);
    }
}

class LabelLocator extends draw2d.layout.locator.Locator {
    relocate(index, figure) {
        // Center in the x middle and then percent of height 
        const parent = figure.getParent()
        const x = 0
        const y = parent.getHeight() - 22
        figure.setPosition(x, y);
    }
}

