import draw2d from "draw2d";
import PubSub from 'pubsub-js'
import cuid from 'cuid'
import { menuItem } from "../../common/Menus";
import Colors from "./Colors";
import Label from "./Label";
import { LabelEditor } from './LabelEditor';
import NodeGroup from './NodeGroup';
import NodeSelectionFeedbackPolicy from './NodeSelectionFeedbackPolicy';


const defaultOptions = (type) => {
    const dv = {
        id: cuid(),
        width: Node.defaultWidth,
        height: Node.defaultHeight,
        description: '',
        colorName: 'Green'
    }
    return dv
}



export default class NodeNumber extends draw2d.shape.basic.Circle {
    static nodeType = 'number'
    static defaultWidth = 300
    static defaultHeight = 200

    static numbers = {}
    type = NodeNumber.nodeType


    type = null
    colorName = null
    nameLabel = null
    descriptionLabel = null

    canDelete = true

    getName = () => this.nameLabel?.text ?? ''
    getDescription = () => this.descriptionLabel?.text ?? ''


    constructor(type = NodeNumber.nodeType, options) {
        const o = { ...defaultOptions(type), ...options }

        const color = Colors.getNodeColor(o.colorName)
        super({
            id: o.id,
            diameter: 23, stroke: 0.1,
            bgColor: color, color: color,
            glow: true, resizeable: false,
        });

        if (!o.name) {
            o.name = this.generateName()
        }


        this.type = type
        this.colorName = o.colorName

        this.addLabels(o.name, o.description)

        // this.on("click", (s, e) => console.log('click node'))
        this.on("dblclick", (s, e) => this.nameLabel.editor.start(this))

        // Adjust selection handle sizes
        this.installEditPolicy(new NodeSelectionFeedbackPolicy())
    }

    static deserialize(data) {
        return new NodeNumber(data.type,
            {
                id: data.id, width: data.w, height: data.h,
                name: data.name, description: data.description, colorName: data.color,
            })
    }

    serialize() {
        return {
            type: this.type, id: this.id, x: this.x, y: this.y, w: this.width, h: this.height,
            name: this.getName(), description: this.getDescription(), color: this.colorName,
            hasGroup: this.group != null
        }
    }

    setCanvas(canvas) {
        super.setCanvas(canvas)

        // Keep track of used labels
        const name = this.getName()
        if (canvas) {
            NodeNumber.numbers[name] = true
        }
        else {
            delete NodeNumber.numbers[name]
        }
    }

    generateName() {
        for (let i = 1; i < 100; i++) {
            const name = i.toString()
            if (!NodeNumber.numbers[name]) {
                return name
            }
        }
        return '0'
    }

    getContextMenuItems(x, y) {
        //const hasDiagramIcon = this.diagramIcon != null

        return [
            menuItem('To front', () => this.moveToFront()),
            menuItem('To back', () => this.moveToBack()),
            menuItem('Edit label', () => this.nameLabel.editor.start(this)),
            menuItem('Delete node', () => this.canvas.runCmd(new draw2d.command.CommandDelete(this)), this.canDelete)
        ]
    }

    toBack(figure) {
        super.toBack(figure)

        // When node is moved back, all groups should be moved back as well
        this.moveAllGroupsToBack()
        // const group = this.getCanvas()?.group
        // group?.toBack()
    }

    moveAllGroupsToBack() {
        // Get all figures in z order
        const figures = this.canvas.getFigures().clone()
        figures.sort(function (a, b) {
            // return 1  if a before b
            // return -1 if b before a
            return a.getZOrder() > b.getZOrder() ? -1 : 1;
        });

        // move all group nodes to back to be behind all nodes
        figures.asArray().forEach(f => {
            if (f instanceof NodeGroup) {
                f.toBack()
            }
        })
    }

    moveToBack() {
        this.toBack()
        PubSub.publish('canvas.Save')
    }

    moveToFront() {
        this.toFront()
        PubSub.publish('canvas.Save')
    }


    setName(name) {
        const oldName = this.getName()
        delete NodeNumber.numbers[oldName]
        this.nameLabel?.setText(name)
        NodeNumber.numbers[name] = true
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
    }

    setDeleteable(flag) {
        super.setDeleteable(flag)
        this.canDelete = flag
    }


    setChildrenVisible(isVisible) {
        this.nameLabel?.setVisible(isVisible)
        this.descriptionLabel?.setVisible(isVisible)
    }

    addLabels = (name, description) => {
        const nameFontColor = Colors.getNodeFontColor(this.colorName)

        this.nameLabel = new Label(this.width + 40, {
            text: name, stroke: 0,
            fontSize: 12, fontColor: nameFontColor, bold: false,
        })

        this.nameLabel.installEditor(new LabelEditor(this));
        this.nameLabel.labelLocator = new NodeNameLocator()
        this.add(this.nameLabel, this.nameLabel.labelLocator);

        const descriptionFontColor = Colors.labelColor
        this.descriptionLabel = new Label(this.width + 40, {
            text: description, stroke: 0,
            fontSize: 9, fontColor: descriptionFontColor, bold: false,
        })

        this.descriptionLabel.installEditor(new LabelEditor(this));
        this.descriptionLabel.labelLocator = new NodeDescriptionLocator()
        this.add(this.descriptionLabel, this.descriptionLabel.labelLocator);
    }
}


class NodeNameLocator extends draw2d.layout.locator.Locator {
    relocate(index, label) {
        const node = label.getParent()
        const x = node.getWidth() / 2 - label.getWidth() / 2
        const y = node.getHeight() / 2 - label.getHeight() / 2
        label.setPosition(x, y)
    }
}

class NodeDescriptionLocator extends draw2d.layout.locator.Locator {
    relocate(index, label) {
        const node = label.getParent()
        const x = node.getWidth() / 2 - label.getWidth() / 2
        const y = node.getHeight() + 0
        label.setPosition(x, y)
    }
}



