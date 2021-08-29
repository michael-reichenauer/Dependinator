import draw2d from "draw2d";
import PubSub from 'pubsub-js'
import cuid from 'cuid'
import { menuItem, menuParentItem } from "../../common/Menus";
import { clickHandler } from "../../common/mouseClicks";
import timing from "../../common/timing";
import Colors from "./Colors";
import CommandChangeColor from "./CommandChangeColor";
import CommandChangeIcon from "./CommandChangeIcon";
import NodeIcons from "./NodeIcons";
import InnerDiagramFigure from "./InnerDiagramFigure";
import Label from "./Label";
import { store } from "./Store";
import { icons } from './../../common/icons';

const defaultIconKey = 'Azure/General/Module'

const defaultOptions = (type) => {
    const dv = {
        id: cuid(),
        width: Node.defaultWidth,
        height: Node.defaultHeight,
        description: 'Description',
    }

    switch (type) {
        case Node.nodeType:
            return { ...dv, icon: defaultIconKey, }
        case Node.systemType:
            return {
                ...dv, name: store.getUniqueSystemName(), colorName: 'DeepPurple', icon: 'Azure/Compute/CloudServices(Classic)',
                width: Node.defaultWidth * 1.2, height: Node.defaultHeight * 1.2,
            }
        case Node.userType:
            return { ...dv, name: 'External Users', icon: 'Azure/Management+Governance/MyCustomers' }
        case Node.externalType:
            return { ...dv, name: 'External Systems', icon: 'Azure/Databases/VirtualClusters' }
        default:
            throw new Error('Unknown type: ' + type);
    }
}



export default class Node extends draw2d.shape.node.Between {
    static nodeType = 'node'
    static systemType = 'system'
    static userType = 'user'
    static externalType = 'external'
    static defaultWidth = 230
    static defaultHeight = 150

    nodeIcons = new NodeIcons()
    figure = null
    type = null
    colorName = null
    nameLabel = null
    descriptionLabel = null
    icon = null
    diagramIcon = null
    canDelete = true


    getName = () => this.nameLabel?.text ?? ''
    getDescription = () => this.descriptionLabel?.text ?? ''

    constructor(type = Node.nodeType, options) {
        const o = { ...defaultOptions(type), ...options }
        if (!o.name) {
            const ic = icons.getIcon(o.icon)
            o.name = ic.name
        }

        super({
            id: o.id,
            width: 60, height: 60, stroke: 0.1,
            bgColor: 'none', color: Colors.nodeBorderColor,
            radius: 5, glow: true
        });

        // const icon = new draw2d.shape.basic.Image({ path: ic.src, width: 22, height: 22, bgColor: 'none' })


        this.type = type
        this.colorName = o.colorName

        this.addLabels(o.name, o.description)
        this.addIcon(o.icon);
        this.addPorts()
        //this.addInnerDiagramIcon()

        // this.on("click", (s, e) => console.log('click node'))
        this.on("dblclick", (s, e) => this.editInnerDiagram())
        this.on('resize', (s, e) => this.handleResize())

        // Adjust selection handle sizes
        const selectionPolicy = this.editPolicy
            .find(p => p instanceof draw2d.policy.figure.RectangleSelectionFeedbackPolicy)
        if (selectionPolicy != null) {
            //this.editPolicy.remove(selectionPolicy)
            selectionPolicy.createResizeHandle = (owner, type) => {
                return new draw2d.ResizeHandle({ owner: owner, type: type, width: 1, height: 1 });
            }
        }
    }

    setCanvas(canvas) {
        super.setCanvas(canvas)

        if (canvas != null) {
            if (canvas.mainNodeId === this.id) {
                // Cannot delete main node of canvas
                this.setDeleteable(false)
            }
            this.diagramIcon?.shape?.attr({ "cursor": "pointer" })
        }
    }


    static deserialize(data) {
        return new Node(data.type,
            {
                id: data.id, width: data.w, height: data.h,
                name: data.name, description: data.description, colorName: data.color, icon: data.icon
            })
    }

    serialize() {
        return {
            type: this.type, id: this.id, x: this.x, y: this.y, w: this.width, h: this.height,
            name: this.getName(), description: this.getDescription(), color: this.colorName, icon: this.iconName,
            hasGroup: this.group != null
        }
    }

    getContextMenuItems(x, y) {
        const hasDiagramIcon = this.diagramIcon != null
        const colorItems = Colors.nodeColorNames().map((name) => {
            return menuItem(name, () => this.canvas.runCmd(new CommandChangeColor(this, name)))
        })

        return [
            menuItem('To front', () => this.toFront()),
            menuItem('To back', () => this.toBack()),
            menuParentItem('Inner diagram', [
                menuItem('Show', () => this.showInnerDiagram(), this.innerDiagram == null, hasDiagramIcon),
                menuItem('Hide (click)', () => this.hideInnerDiagram(), this.innerDiagram != null, hasDiagramIcon),
                menuItem('Edit (dbl-click)', () => this.editInnerDiagram(), true, hasDiagramIcon),
            ], true, hasDiagramIcon),
            menuParentItem('Change color', colorItems),
            menuItem('Change icon ...', () => PubSub.publish('nodes.showDialog', { action: (iconKey) => this.changeIcon(iconKey) })),
            menuItem('Set default size', () => this.setDefaultSize()),
            menuItem('Delete node', () => this.canvas.runCmd(new draw2d.command.CommandDelete(this)), this.canDelete)
        ]
    }

    changeIcon(iconKey) {
        this.canvas.runCmd(new CommandChangeIcon(this, iconKey))
    }

    setName(name) {
        this.nameLabel?.setText(name)
    }

    setDescription(description) {
        this.descriptionLabel?.setText(description)
    }

    getAllConnections() {
        return this.getPorts().asArray().flatMap(p => p.getConnections().asArray())
    }

    setDefaultSize() {
        this.setWidth(Node.defaultWidth)
        this.setHeight(Node.defaultHeight)
    }

    setNodeColor(colorName) {
        this.colorName = colorName
        const color = Colors.getNodeColor(colorName)
        const borderColor = Colors.getNodeBorderColor(colorName)
        const fontColor = Colors.getNodeFontColor(colorName)

        this.setBackgroundColor(color)
        this.setColor(borderColor)

        this.nameLabel?.setFontColor(fontColor)
        this.descriptionLabel?.setFontColor(fontColor)
        // this.icon?.setColor(fontColor)
        this.diagramIcon?.setColor(fontColor)
    }

    setDeleteable(flag) {
        super.setDeleteable(flag)
        this.canDelete = flag
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

    toBack() {
        super.toBack()
        const group = this.getCanvas()?.group
        group?.toBack()

    }

    showInnerDiagram() {
        const t = timing()

        this.setChildrenVisible(false)

        const canvasData = store.getCanvas(this.getCanvas().diagramId, this.getId())
        this.innerDiagram = new InnerDiagramFigure(this, canvasData)
        this.innerDiagram.onClick = clickHandler(
            () => this.hideInnerDiagram(),
            () => this.editInnerDiagram())

        this.add(this.innerDiagram, new InnerDiagramLocator())
        this.repaint()
        t.log()
    }


    hideInnerDiagram() {
        const t = timing()
        if (this.innerDiagram == null) {
            return
        }

        this.setChildrenVisible(true)
        this.remove(this.innerDiagram)
        this.innerDiagram = null
        t.log()
    }

    editInnerDiagram() {
        if (this.diagramIcon == null) {
            return
        }

        if (this.innerDiagram == null) {
            this.showInnerDiagram()
        }

        PubSub.publish('canvas.EditInnerDiagram', this)
    }

    handleResize() {
        this.nameLabel?.setTextWidth(this.width)
        this.nameLabel?.repaint()
        this.descriptionLabel?.setTextWidth(this.width)
        this.descriptionLabel?.repaint()

        if (this.innerDiagram == null) {
            return
        }

        this.hideInnerDiagram()
        this.showInnerDiagram()
    }

    setChildrenVisible(isVisible) {
        this.nameLabel?.setVisible(isVisible)
        this.descriptionLabel?.setVisible(isVisible)
        this.icon?.setVisible(isVisible)
        this.diagramIcon?.setVisible(isVisible)
    }

    addLabels = (name, description) => {
        const fontColor = Colors.labelColor

        this.nameLabel = new Label(this.width + 40, {
            text: name, stroke: 0,
            fontSize: 12, fontColor: fontColor, bold: true,
        })

        this.descriptionLabel = new Label(this.width + 40, {
            text: description, stroke: 0,
            fontSize: 9, fontColor: fontColor, bold: false,
        })

        this.nameLabel.installEditor(new draw2d.ui.LabelInplaceEditor());
        this.nameLabel.labelLocator = new NodeNameLocator()
        this.add(this.nameLabel, this.nameLabel.labelLocator);
        this.descriptionLabel.installEditor(new draw2d.ui.LabelInplaceEditor());
        this.descriptionLabel.labelLocator = new NodeDescriptionLocator()
        this.add(this.descriptionLabel, this.descriptionLabel.labelLocator);
    }

    addIcon(iconKey) {
        //console.log('add icon key', iconKey)
        if (iconKey == null) {
            return
        }

        const ic = icons.getIcon(iconKey)
        const icon = new draw2d.shape.basic.Image({ path: ic.src, width: this.width, height: this.height, bgColor: 'none' })

        this.iconName = iconKey
        this.icon = icon
        this.add(icon, new NodeIconLocator())
    }


    addInnerDiagramIcon() {
        if (this.type !== Node.nodeType && this.type !== Node.systemType) {
            return
        }
        const iconColor = Colors.getNodeFontColor(this.colorName)
        this.diagramIcon = new draw2d.shape.icon.Diagram({
            width: 15, height: 15, color: iconColor, bgColor: 'none',
        })

        this.diagramIcon.on("click", () => this.showInnerDiagram())

        this.add(this.diagramIcon, new InnerDiagramIconLocator())
    }

    addPorts() {
        this.createPort("input", new draw2d.layout.locator.XYRelPortLocator(50, 0))
        this.createPort("output", new draw2d.layout.locator.XYRelPortLocator(50, 100))

        // this.getPorts().each(function (i, port) {
        //     port.setConnectionAnchor(new draw2d.layout.anchor.FanConnectionAnchor(port));
        // });

        // Make ports larger to support touch
        this.getPorts().each((i, p) => {
            p.setCoronaWidth(5)
            p.setDimension(10)
        })
    }
}


class NodeNameLocator extends draw2d.layout.locator.Locator {
    relocate(index, label) {
        const node = label.getParent()
        const x = node.getWidth() / 2 - label.getWidth() / 2
        const y = node.getHeight() + 0
        label.setPosition(x, y)
    }
}

class NodeDescriptionLocator extends draw2d.layout.locator.Locator {
    relocate(index, label) {
        const node = label.getParent()
        const nameHeight = node.nameLabel.getHeight()
        const x = node.getWidth() / 2 - label.getWidth() / 2
        const y = node.getHeight() + nameHeight - 8
        label.setPosition(x, y)
    }
}


class NodeIconLocator extends draw2d.layout.locator.Locator {
    relocate(index, icon) {
        icon.setPosition(0, 0)
    }
}


class InnerDiagramIconLocator extends draw2d.layout.locator.PortLocator {
    relocate(index, figure) {
        const parent = figure.getParent()
        this.applyConsiderRotation(figure, 3, parent.getHeight() - 18);
    }
}

class InnerDiagramLocator extends draw2d.layout.locator.Locator {
    relocate(index, target) {
        target.setPosition(2, 2)
    }
}