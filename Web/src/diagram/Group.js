import draw2d from "draw2d";
import PubSub from 'pubsub-js'
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
    static defaultHeight = 800

    type = Group.groupType
    colorName = null
    nameLabel = null
    descriptionLabel = null


    getName = () => this.nameLabel?.text ?? ''
    getDescription = () => this.descriptionLabel?.text ?? ''

    constructor(name = 'Group', options) {
        const o = { ...defaultOptions(), ...options }

        super({
            id: o.id, width: o.width, height: o.height,
            bgColor: Colors.canvasBackground, alpha: 0.5, color: Colors.canvasText,
            dasharray: '- ', radius: 5,
        });

        this.setDeleteable(false)
        this.addLabel(name)

        this.on("click", (s, e) => PubSub.publish('canvas.SetEditMode', false))
        this.on("dblclick", (s, e) => PubSub.publish('canvas.AddDefaultNode', { x: e.x, y: e.y }))
    }

    static deserialize(data) {
        return new Group(data.name,
            { id: data.id, width: data.w, height: data.h, description: data.description })
    }

    serialize() {
        return {
            type: this.type, id: this.id, x: this.x, y: this.y, w: this.width, h: this.height,
            name: this.getName(), description: this.getDescription(), color: this.colorName
        }
    }

    getContextMenuItems(x, y) {
        // Reuse the canvas context menu
        return this.getCanvas().canvas.getContextMenuItems(x, y)
    }

    addLabel(name) {
        this.nameLabel = new draw2d.shape.basic.Label({
            text: name, stroke: 0, fontSize: 30, fontColor: Colors.canvasText, bold: true,
        })
        this.add(this.nameLabel, new GroupNameLocator());
    }
}


class GroupNameLocator extends draw2d.layout.locator.Locator {
    relocate(index, target) {
        let targetBoundingBox = target.getBoundingBox()
        target.setPosition(1, -(targetBoundingBox.h - 6))
    }
}

