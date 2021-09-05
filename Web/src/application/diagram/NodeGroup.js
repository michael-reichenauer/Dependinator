import draw2d from "draw2d";
import cuid from 'cuid'
import { menuItem } from "../../common/Menus";
import Colors from "./Colors";
import { icons } from "../../common/icons";
import CommandChangeIcon from './CommandChangeIcon';
import { PubSub } from 'pubsub-js';

const defaultOptions = () => {
    return {
        id: cuid(),
        width: NodeGroup.defaultWidth,
        height: NodeGroup.defaultHeight,
        description: 'Description',
        icon: 'Default',
    }
}


//export default class NodeGroup extends draw2d.shape.composite.Raft {
export default class NodeGroup extends draw2d.shape.basic.Rectangle {
    static nodeType = 'nodeGroup'
    static defaultWidth = 500
    static defaultHeight = 340

    type = NodeGroup.nodeType
    nameLabel = null
    descriptionLabel = null

    getName = () => this.nameLabel?.text ?? ''
    getDescription = () => this.descriptionLabel?.text ?? ''


    constructor(options) {
        const o = { ...defaultOptions(), ...options }

        super({
            id: o.id,
            width: o.width, height: o.height, stroke: 0.5,
            bgColor: Colors.canvasBackground, alpha: 0.4, color: Colors.canvasText,
            radius: 5, glow: true, dasharray: '- ',
        });
        if (!o.name) {
            const ic = icons.getIcon(o.icon)
            o.name = ic.name
        }

        this.addIcon(o.icon);
        this.addLabels(o.name, o.description)

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

    setCanvas(canvas) {
        super.setCanvas(canvas)

        if (canvas != null) {
            this.toBack()
        }
    }


    static deserialize(data) {
        return new NodeGroup({ id: data.id, width: data.w, height: data.h, name: data.name, icon: data.icon })
    }

    serialize() {
        try {
            return {
                type: this.type, id: this.id, x: this.x, y: this.y, w: this.width, h: this.height,
                name: this.getName(), hasGroup: this.group != null, icon: this.iconName,
            }
        } catch (error) {
            console.error('error', error)
        }

    }

    changeIcon(iconKey) {
        this.canvas.runCmd(new CommandChangeIcon(this, iconKey))
    }

    getContextMenuItems(x, y) {
        return [
            menuItem('To back', () => this.toBack()),
            menuItem('Change icon ...', () => PubSub.publish('nodes.showDialog', { add: false, group: true, action: (iconKey) => this.changeIcon(iconKey) })),
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

    addLabels = (name, description) => {
        this.nameLabel = new draw2d.shape.basic.Label({
            text: name, stroke: 0, fontSize: 12, fontColor: Colors.canvasText, bold: true,
        })

        this.nameLabel.installEditor(new draw2d.ui.LabelInplaceEditor());
        this.nameLabel.labelLocator = new NodeGroupNameLocator()
        this.add(this.nameLabel, this.nameLabel.labelLocator);


        this.descriptionLabel =
            new draw2d.shape.basic.Label({
                text: description, stroke: 0, fontSize: 9, fontColor: Colors.canvasText, bold: false,
            })

        this.descriptionLabel.installEditor(new draw2d.ui.LabelInplaceEditor());
        this.descriptionLabel.labelLocator = new NodeGroupDescriptionLocator()
        this.add(this.descriptionLabel, this.descriptionLabel.labelLocator);
    }

    setIcon(name) {
        if (this.icon != null) {
            this.remove(this.icon)
            this.icon = null
            this.iconName = null
        }
        this.addIcon(name)
        this.repaint()
    }

    addIcon(iconKey) {
        //console.log('add icon key', iconKey)
        if (iconKey == null) {
            return
        }

        const ic = icons.getIcon(iconKey)
        const icon = new draw2d.shape.basic.Image({ path: ic.src, width: 20, height: 20, bgColor: 'none' })

        this.iconName = iconKey
        this.icon = icon
        this.add(icon, new NodeIconLocator())
    }
}


class NodeGroupNameLocator extends draw2d.layout.locator.Locator {
    relocate(index, label) {
        label.setPosition(22, -3)
    }
}


class NodeGroupDescriptionLocator extends draw2d.layout.locator.Locator {
    relocate(index, label) {
        const node = label.getParent()
        const nameHeight = node.nameLabel.getHeight()
        const x = node.nameLabel.x
        const y = node.nameLabel.y + nameHeight - 8
        label.setPosition(x, y)
    }
}

class NodeIconLocator extends draw2d.layout.locator.Locator {
    relocate(index, icon) {
        icon.setPosition(3, 3)
    }
}
