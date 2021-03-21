import draw2d from "draw2d";
import PubSub from 'pubsub-js'
import { Item, NestedItem } from "../common/ContextMenu";
import { clickHandler } from "../common/mouseClicks";
import { timing } from "../common/timing";
import Colors from "./colors";
import { CommandChangeColor } from "./commandChangeColor";
import { CommandChangeIcon } from "./commandChangeIcon";
import { createNodeIcon, getNodeIconNames } from "./icons";
import { InnerDiagram } from "./innerDiagram";
import { store } from "./store";
import { InnerDiagramIconLocator, LabelLocator, InnerDiagramLocator } from './nodeLocators'


const defaultOptions = (type) => {
    const dv = {
        id: draw2d.util.UUID.create(),
        width: Node.defaultWidth,
        height: Node.defaultHeight,
        description: 'Description',
    }

    switch (type) {
        case Node.nodeType:
            return { ...dv, name: 'Node', colorName: 'DeepPurple', icon: 'Node', }
        case Node.userType:
            return { ...dv, name: 'External User', colorName: 'BlueGrey', icon: 'User' }
        case Node.externalType:
            return { ...dv, name: 'External System', colorName: 'BlueGrey', icon: 'External' }
        default:
            throw new Error('Unknown type: ' + type);
    }
}



export default class Node extends draw2d.shape.node.Between {
    static nodeType = 'node'
    static userType = 'user'
    static externalType = 'external'
    static defaultWidth = 230
    static defaultHeight = 150


    figure = null
    type = null
    colorName = null
    nameLabel = null
    descriptionLabel = null
    icon = null
    diagramIcon = null


    getName = () => this.nameLabel?.text ?? ''
    getDescription = () => this.descriptionLabel?.text ?? ''

    constructor(type = Node.nodeType, options) {
        const o = { ...defaultOptions(type), ...options }

        super({
            id: o.id,
            width: o.width, height: o.height,
            bgColor: Colors.getNodeColor(o.colorName), color: Colors.getNodeBorderColor(o.colorName),
            radius: 5,
        });

        this.type = type
        this.colorName = o.colorName

        this.addLabels(o.name, o.description)
        this.addIcon(o.icon);
        this.addInnerDiagramIcon()
        this.addPorts()

        this.on("click", (s, e) => console.log('click node'))
        this.on("dblclick", (s, e) => this.editInnerDiagram())
    }

    setCanvas(canvas) {
        super.setCanvas(canvas)
        this.diagramIcon?.shape?.attr({ "cursor": "pointer" })
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
        const isNode = this.type === Node.nodeType
        const colorItems = Colors.nodeColorNames().map((name) => {
            return new Item(name, () => this.canvas.runCmd(new CommandChangeColor(this, name)))
        })
        const iconItems = getNodeIconNames().map((name) => {
            return new Item(name, () => this.canvas.runCmd(new CommandChangeIcon(this, name)))
        })

        return [
            new NestedItem('Inner diagram', [
                new Item('Show', () => this.showInnerDiagram(), this.innerDiagram == null, isNode),
                new Item('Hide (click)', () => this.hideInnerDiagram(), this.innerDiagram != null, isNode),
                new Item('Edit (dbl-click)', () => this.editInnerDiagram(), true, isNode),
            ], true, isNode),
            new NestedItem('Change color', colorItems),
            new NestedItem('Change icon', iconItems),
            new Item('To front', () => this.toFront()),
            new Item('To back', () => this.toBack()),
            new Item('Delete node', () => this.canvas.runCmd(new draw2d.command.CommandDelete(this)))
        ]
    }

    setName(name) {
        this.nameLabel?.setText(name)
    }

    setDescription(description) {
        this.descriptionLabel?.setText(description)
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
        this.icon?.setColor(fontColor)
        this.diagramIcon?.setColor(fontColor)
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

        const canvasData = store.read(this.getId())
        this.innerDiagram = new InnerDiagram(this, canvasData)
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
        if (this.type !== Node.nodeType) {
            return
        }

        if (this.innerDiagram == null) {
            this.showInnerDiagram()
        }

        PubSub.publish('canvas.EditInnerDiagram', this)
    }

    setChildrenVisible(isVisible) {
        this.nameLabel?.setVisible(isVisible)
        this.descriptionLabel?.setVisible(isVisible)
        this.icon?.setVisible(isVisible)
        this.diagramIcon?.setVisible(isVisible)
    }

    addLabels = (name, description) => {
        const fontColor = Colors.getNodeFontColor(this.colorName)

        this.nameLabel = new draw2d.shape.basic.Label({
            text: name, stroke: 0,
            fontSize: 20, fontColor: fontColor, bold: true,
        })

        this.descriptionLabel = new draw2d.shape.basic.Text({
            text: description, stroke: 0,
            fontSize: 14, fontColor: fontColor, bold: false,
        })

        this.nameLabel.installEditor(new draw2d.ui.LabelInplaceEditor());
        this.nameLabel.labelLocator = new LabelLocator(7)
        this.add(this.nameLabel, this.nameLabel.labelLocator);
        this.descriptionLabel.installEditor(new draw2d.ui.LabelInplaceEditor());
        this.descriptionLabel.labelLocator = new LabelLocator(30)
        this.add(this.descriptionLabel, this.descriptionLabel.labelLocator);
    }

    addIcon(iconName) {
        if (iconName == null) {
            return
        }
        const icon = createNodeIcon(iconName)
        if (icon == null) {
            return
        }

        this.iconName = iconName
        this.icon = icon
        const iconColor = Colors.getNodeFontColor(this.colorName)
        this.icon.attr({ width: 15, height: 15, color: iconColor, bgColor: 'none' })
        this.add(this.icon, new draw2d.layout.locator.XYRelPortLocator(1, 1))
    }


    addInnerDiagramIcon() {
        if (this.type !== Node.nodeType) {
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
    }
}

