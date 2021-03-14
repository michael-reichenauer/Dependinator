import draw2d from "draw2d";
import PubSub from 'pubsub-js'
import Colors from "./colors";


const newId = () => draw2d.util.UUID.create()


export default class Group {
    static groupType = 'group'
    static defaultWidth = 1000
    static defaultHeight = 800

    figure = null
    type = Group.groupType
    colorName = null
    nameLabel = null
    descriptionLabel = null


    getName = () => this.nameLabel?.text ?? ''
    getDescription = () => this.descriptionLabel?.text ?? ''

    constructor(name = 'group',
        id = newId(), width = Group.defaultWidth, height = Group.defaultHeight, description = 'Description') {
        const figure = new draw2d.shape.composite.Raft({
            id: id, width: width, height: height,
            bgColor: Colors.canvasBackground, alpha: 0.5, color: Colors.canvasText,
            dasharray: '- ', radius: 5,
        });

        figure.setDeleteable(false)

        this.nameLabel = new draw2d.shape.basic.Label({
            text: name, stroke: 0,
            fontSize: 30, fontColor: Colors.canvasText, bold: true,
            userData: { type: "name" }
        })
        figure.add(this.nameLabel, new GroupNameLocator());

        figure.on("click", (s, e) => PubSub.publish('canvas.SetReadOnlyMode', figure))
        figure.on("dblclick", (s, e) => PubSub.publish('canvas.AddDefaultNode', { x: e.x, y: e.y }))


        this.figure = figure
        this.figure.userData = this
    }
}


const GroupNameLocator = draw2d.layout.locator.Locator.extend({
    init: function () {
        this._super();
    },
    relocate: function (index, target) {
        let targetBoundingBox = target.getBoundingBox()
        target.setPosition(0, -(targetBoundingBox.h - 36))
    }
});

