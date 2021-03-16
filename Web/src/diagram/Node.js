import draw2d from "draw2d";
import PubSub from 'pubsub-js'
import { Item, NestedItem } from "../common/ContextMenu";
import { onClickHandler } from "../common/mouseClicks";
import { timing } from "../common/timing";
import Colors from "./colors";
import { CommandChangeColor } from "./commandChangeColor";
import { InnerDiagram } from "./innerDiagram";
import { store } from "./store";



const defaultOptions = (type) => {
    const dv = {
        id: draw2d.util.UUID.create(),
        width: 230,
        height: 150,
        description: 'Description',
    }

    switch (type) {
        case Node.nodeType:
            return { ...dv, name: 'Node', colorName: 'DeepPurple', icon: null }
        case Node.userType:
            return { ...dv, name: 'External User', colorName: 'BlueGrey', icon: new draw2d.shape.icon.User() }
        case Node.externalType:
            return { ...dv, name: 'External System', colorName: 'BlueGrey', icon: new draw2d.shape.icon.NewWindow() }
        default:
            throw new Error('Unknown type: ' + type);
    }
}


export default class Node extends draw2d.shape.node.Between {
    static nodeType = 'node'
    static userType = 'user'
    static externalType = 'external'


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

    static deserialize(data) {
        return new Node(data.type,
            {
                id: data.id, width: data.w, height: data.h,
                name: data.name, description: data.description, colorName: data.color
            })
    }

    serialize() {
        return {
            type: this.type, id: this.id, x: this.x, y: this.y, w: this.width, h: this.height,
            name: this.getName(), description: this.getDescription(), color: this.colorName
        }
    }

    getContextMenuItems(x, y) {
        const colorItems = Colors.nodeColorNames().map((colorName) => {
            return new Item(colorName, () => this.runSetColorCmd(colorName))
        })

        const menuItems = [
            new Item('Show inner diagram', () => this.showInnerDiagram(), this.innerDiagram == null),
            new Item('Hide inner diagram', () => this.hideInnerDiagram(), this.innerDiagram != null),
            new Item('Edit inner diagram (dbl-click)', () => this.editInnerDiagram()),
            new NestedItem('Set color', colorItems),
            new Item('To front', () => this.toFront()),
            new Item('To back', () => this.toBack()),
            new Item('Delete node', () => this.deleteNodeCmd())
        ]

        return menuItems
    }

    deleteNodeCmd() {
        const command = new draw2d.command.CommandDelete(this);
        this.getCanvas().getCommandStack().execute(command);
    }

    runSetColorCmd(colorName) {
        const command = new CommandChangeColor(this, colorName);
        this.getCanvas().getCommandStack().execute(command);
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

    showInnerDiagram() {
        const t = timing()
        const canvasData = store.read(this.getId())

        this.nameLabel?.setVisible(false)
        this.descriptionLabel?.setVisible(false)
        this.diagramIcon?.setVisible(false)

        this.innerDiagram = new InnerDiagram(this, canvasData)
        this.innerDiagram.onClick = onClickHandler(
            () => this.hideInnerDiagram(),
            () => this.editInnerDiagram())

        this.add(this.innerDiagram, new InnerDiagramLocator())
        this.repaint()
        t.log('added')
    }


    hideInnerDiagram() {
        const t = timing()
        if (this.innerDiagram == null) {
            return
        }

        this.nameLabel?.setVisible(true)
        this.descriptionLabel?.setVisible(true)
        this.diagramIcon?.setVisible(true)

        this.remove(this.innerDiagram)
        this.innerDiagram = null
        t.log()
    }

    editInnerDiagram() {
        if (this.innerDiagram == null) {
            this.showInnerDiagram()
        }

        PubSub.publish('canvas.EditInnerDiagram', this)
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
        this.add(this.nameLabel, new LabelLocator(7));
        this.descriptionLabel.installEditor(new draw2d.ui.LabelInplaceEditor());
        this.add(this.descriptionLabel, new LabelLocator(30));
    }

    addIcon(icon) {
        if (icon == null) {
            return
        }
        this.icon = icon
        const iconColor = Colors.getNodeFontColor(this.colorName)
        this.icon.attr({ width: 18, height: 15, color: iconColor, bgColor: 'none' })
        this.add(this.icon, new draw2d.layout.locator.XYRelPortLocator(1, 1))
    }


    addInnerDiagramIcon() {
        if (this.type !== Node.nodeType) {
            return
        }
        const iconColor = Colors.getNodeFontColor(this.colorName)
        this.diagramIcon = new draw2d.shape.icon.Diagram({
            width: 20, height: 20, color: iconColor, bgColor: 'none',
        })

        this.diagramIcon.on("click", () => this.showInnerDiagram())

        this.add(this.diagramIcon, new InnerDiagramIconLocator())
    }

    addPorts() {
        this.createPort("input", new InputTopPortLocator());
        this.createPort("output", new OutputBottomPortLocator());
    }
}


class LabelLocator extends draw2d.layout.locator.XYRelPortLocator {
    constructor(y) {
        super(0, y)
    }
    relocate(index, figure) {
        let parent = figure.getParent()
        this.applyConsiderRotation(
            figure,
            parent.getWidth() / 2 - figure.getWidth() / 2,
            parent.getHeight() / 100 * this.y
        )
    }
}


class InnerDiagramIconLocator extends draw2d.layout.locator.PortLocator {
    relocate(index, figure) {
        const parent = figure.getParent()
        this.applyConsiderRotation(figure, parent.getWidth() / 2 - 10, parent.getHeight() - 25);
    }
}

class InnerDiagramLocator extends draw2d.layout.locator.Locator {
    relocate(index, target) {
        target.setPosition(2, 2)
    }
}


class InputTopPortLocator extends draw2d.layout.locator.PortLocator {
    relocate(index, figure) {
        this.applyConsiderRotation(figure, figure.getParent().getWidth() / 2, 0);
    }
}

class OutputBottomPortLocator extends draw2d.layout.locator.PortLocator {
    relocate(index, figure) {
        var p = figure.getParent();
        this.applyConsiderRotation(figure, p.getWidth() / 2, p.getHeight());
    }
}

