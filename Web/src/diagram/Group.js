import draw2d from "draw2d";
import PubSub from 'pubsub-js'
import { Item } from "../common/ContextMenu";
import Colors from "./colors";


const defaultOptions = () => {
    return {
        id: draw2d.util.UUID.create(),
        width: Group.defaultWidth,
        height: Group.defaultHeight,
        description: 'Description',
    }
}


export default class Group extends draw2d.shape.composite.Raft {
    NAME = 'Group'

    static groupType = 'group'
    static defaultWidth = 1000
    static defaultHeight = Group.defaultWidth * 150 / 230

    type = Group.groupType
    colorName = null
    nameLabel = null
    descriptionLabel = null


    getName = () => this.nameLabel?.text ?? ''
    getDescription = () => this.descriptionLabel?.text ?? ''

    constructor(name = 'Group', description = 'Description', options) {
        const o = { ...defaultOptions(), ...options }

        super({
            id: o.id, width: o.width, height: o.height,
            bgColor: Colors.canvasBackground, alpha: 0.5, color: Colors.canvasText,
            dasharray: '- ', radius: 5, stroke: 2
        });

        this.setDeleteable(false)
        this.addLabels(name, description)
        this.addPorts()

        this.on("click", (s, e) => PubSub.publish('canvas.SetEditMode', false))
        this.on("dblclick", (s, e) => PubSub.publish('canvas.AddDefaultNode', { x: e.x, y: e.y }))
    }

    setCanvas(canvas) {
        super.setCanvas(canvas)
        if (canvas != null) {
            canvas.group = this
        }
    }

    static deserialize(data) {
        return new Group(data.name, data.description,
            { id: data.id, width: data.w, height: data.h })
    }

    serialize() {
        return {
            type: this.type, id: this.id, x: this.x, y: this.y, w: this.width, h: this.height,
            name: this.getName(), description: this.getDescription(), color: this.colorName
        }
    }

    getContextMenuItems(x, y) {
        // Reuse the canvas context menu
        return [
            ...this.getCanvas().canvas.getContextMenuItems(x, y),
            new Item('Set default size', () => this.setDefaultSize()),
        ]
    }

    setName(name) {
        this.nameLabel?.setText(name)
    }

    setDescription(description) {
        this.descriptionLabel?.setText(description)
    }

    setDefaultSize() {
        this.setWidth(Group.defaultWidth)
        this.setHeight(Group.defaultHeight)
    }

    addLabels(name, description) {
        this.nameLabel = new draw2d.shape.basic.Label({
            text: name, stroke: 0, fontSize: 30, fontColor: Colors.canvasText, bold: true,
        })
        this.nameLabel.installEditor(new draw2d.ui.LabelInplaceEditor());
        this.add(this.nameLabel, new GroupNameLocator());

        this.descriptionLabel = new draw2d.shape.basic.Label({
            text: description, stroke: 0, fontSize: 14, fontColor: Colors.canvasText, bold: false,
        })
        this.descriptionLabel.installEditor(new draw2d.ui.LabelInplaceEditor());
        this.add(this.descriptionLabel, new GroupDescriptionLocator());
    }

    addPorts() {
        this.createPort("input", new draw2d.layout.locator.XYRelPortLocator(0, 50))
        this.createPort("input", new draw2d.layout.locator.XYRelPortLocator(50, 0))
        this.createPort("output", new draw2d.layout.locator.XYRelPortLocator(100, 50))
        this.createPort("output", new draw2d.layout.locator.XYRelPortLocator(50, 100))
    }
}


class GroupNameLocator extends draw2d.layout.locator.Locator {
    relocate(index, target) {
        target.setPosition(2, -60)
    }
}

class GroupDescriptionLocator extends draw2d.layout.locator.Locator {
    relocate(index, target) {
        target.setPosition(4, -24)
    }
}


