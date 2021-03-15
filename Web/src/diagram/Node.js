import draw2d from "draw2d";
import Colors from "./colors";
import { store } from "./store";
import { showInnerDiagram } from './figures'



const newId = () => draw2d.util.UUID.create()

export default class Node {
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

    constructor(figure) {
        this.figure = figure
        this.figure.userData = this
    }


    static createDefault = (type) => {
        const node = new Node(new draw2d.shape.node.Between())
        node.configure(type)
        return node
    }

    static create = (type, id, width, height, name, description, colorName) => {
        const node = new Node(new draw2d.shape.node.Between())
        node.configureNode(type, id, width, height, name, description, colorName, node.getDefaultIcon(type))
        return node
    }


    setColor = (colorName) => {
        this.colorName = colorName
        const color = Colors.getNodeColor(colorName)
        const borderColor = Colors.getNodeBorderColor(colorName)
        const fontColor = Colors.getNodeFontColor(colorName)

        this.figure.setBackgroundColor(color)
        this.figure.setColor(borderColor)

        this.nameLabel?.setFontColor(fontColor)
        this.descriptionLabel?.setFontColor(fontColor)
        this.icon?.setColor(fontColor)
        this.diagramIcon?.setColor(fontColor)
    }


    configure = (type) => {
        const { name, colorName, icon } = this.getDefaultValues(type)
        this.configureNode(type, newId(), Node.defaultWidth, Node.defaultHeight,
            name, 'Description', colorName, icon)
    }


    configureNode = (type, id, width, height, name, description, colorName, icon) => {
        this.type = type
        this.colorName = colorName
        const color = Colors.getNodeColor(colorName)
        const borderColor = Colors.getNodeBorderColor(colorName)
        const fontColor = Colors.getNodeFontColor(colorName)
        this.figure.attr({
            id: id,
            width: width, height: height,
            bgColor: color, color: borderColor,
            radius: 5,
        });

        this.addLabels(name, description)
        this.addIcon(icon);
        this.addInnerDiagramIcon(this.figure, fontColor, color)
        this.addPorts()

        this.figure.on("click", (src, event) => console.log('click node'))
        this.figure.on("dblclick", (src, event) => console.log('double click node'))
    }

    addLabels = (name, description) => {
        const fontColor = Colors.getNodeFontColor(this.colorName)

        this.nameLabel = new draw2d.shape.basic.Label({
            text: name, stroke: 0,
            fontSize: 20, fontColor: fontColor, bold: true,
            userData: { type: "name" }
        })

        this.descriptionLabel = new draw2d.shape.basic.Text({
            text: description, stroke: 0,
            fontSize: 14, fontColor: fontColor, bold: false,
            userData: { type: "description" }
        })

        this.nameLabel.installEditor(new draw2d.ui.LabelInplaceEditor());
        this.figure.add(this.nameLabel, labelLocator(7));
        this.descriptionLabel.installEditor(new draw2d.ui.LabelInplaceEditor());
        this.figure.add(this.descriptionLabel, labelLocator(30));
    }

    addIcon = (icon) => {
        if (icon == null) {
            return
        }
        this.icon = icon
        const iconColor = Colors.getNodeFontColor(this.colorName)
        this.icon.attr({ width: 18, height: 15, color: iconColor, bgColor: 'none' })
        this.figure.add(this.icon, new draw2d.layout.locator.XYRelPortLocator(1, 1))
    }


    addInnerDiagramIcon = () => {
        if (this.type !== Node.nodeType) {
            return
        }
        const iconColor = Colors.getNodeFontColor(this.colorName)
        this.diagramIcon = new draw2d.shape.icon.Diagram({
            width: 20, height: 20, color: iconColor, bgColor: 'none',
        })

        this.diagramIcon.on("click", () => showInnerDiagram(this.figure, store))

        this.figure.add(this.diagramIcon, new InnerDiagramIconLocator())
    }

    addPorts = () => {
        this.figure.createPort("input", new InputTopPortLocator());
        this.figure.createPort("output", new OutputBottomPortLocator());
    }


    getDefaultValues = (type) => {
        switch (type) {
            case Node.nodeType:
                return { name: 'Node', colorName: 'DeepPurple', icon: this.getDefaultIcon(type) }
            case Node.userType:
                return { name: 'External User', colorName: 'BlueGrey', icon: this.getDefaultIcon(type) }
            case Node.externalType:
                return { name: 'External System', colorName: 'BlueGrey', icon: this.getDefaultIcon(type) }
            default:
                throw new Error('Unknown type: ' + type);
        }
    }

    getDefaultIcon = (type) => {
        switch (type) {
            case Node.nodeType:
                return null
            case Node.userType:
                return new draw2d.shape.icon.User()
            case Node.externalType:
                return new draw2d.shape.icon.NewWindow()
            default:
                throw new Error('Unknown type: ' + type);
        }
    }
}



const labelLocator = (y) => {
    const locator = new draw2d.layout.locator.XYRelPortLocator(0, y)
    locator.relocate = (index, figure) => {
        let parent = figure.getParent()
        locator.applyConsiderRotation(
            figure,
            parent.getWidth() / 2 - figure.getWidth() / 2,
            parent.getHeight() / 100 * locator.y
        )
    }
    return locator
}


class InnerDiagramIconLocator extends draw2d.layout.locator.PortLocator {
    relocate(index, figure) {
        const parent = figure.getParent()
        this.applyConsiderRotation(figure, parent.getWidth() / 2 - 10, parent.getHeight() - 25);
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