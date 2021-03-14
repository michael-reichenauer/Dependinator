import draw2d from "draw2d";
import Colors from "./colors";
import PubSub from 'pubsub-js'
import { InnerDiagram } from "./innerDiagramFigure";
import { timing } from "../common/timing";
//import { loadData } from "./store";
import { onClickHandler } from "../common/mouseClicks";
import { store } from "./store";

export const defaultGroupNodeWidth = 1000
export const defaultGroupNodeHeight = 800
export const defaultNodeWidth = 230
export const defaultNodeHeight = 150
export const nodeType = 'node'
export const userType = 'user'
export const externalType = 'external'
export const groupType = 'group'
const newUserIcon = () => new draw2d.shape.icon.User()
const newExternalIcon = () => new draw2d.shape.icon.NewWindow()


export default class Node {
    static nodeType = 'node'
    static userType = 'user'
    static externalType = 'external'
    static groupType = 'group'

    constructor(figure) {
        this.figure = figure
        this.figure.userData2 = this
    }

    static from = (figure) => {
        return new Node(figure)
    }


    static create = (type) => {
        const node = new Node(new draw2d.shape.node.Between())
        node.configure(type)

        const { name, colorName } = this.getDefaultValues(type)

        return new Node(createNode(
            draw2d.util.UUID.create(),
            defaultNodeWidth, defaultNodeHeight,
            name, 'Description', colorName))
    }

    getFigure = () => {
        return this.figure
    }

    configure = (type) => {
        //const { name, colorName } = this.getDefaultValues(type)

    }


    configureCommonNode = (type, id, width, height, name, description, colorName, icon) => {
        const color = Colors.getNodeColor(colorName)
        const borderColor = Colors.getNodeBorderColor(colorName)
        const fontColor = Colors.getNodeFontColor(colorName)
        this.figure.attr({
            id: id,
            width: width, height: height,
            bgColor: color, color: borderColor,
            radius: 5,
            userData: { type: type, color: colorName }
        });

        addFigureLabels(this.figure, fontColor, name, description, colorName)
        if (icon != null) {
            addIcon(this.figure, icon, fontColor, color);
        }
        if (type === nodeType) {
            addInnerDiagramIcon(this.figure, fontColor, color)
        }

        this.figure.on("click", function (emitter, event) {
            console.log('click node')
        });
        this.figure.on("dblclick", function (emitter, event) {
            console.log('double click node')
        });

        addPorts(this.figure)
    }


    getName = () => {
        const children = this.figure.getChildren().asArray()
        const nameLabel = children.find(c => c.userData?.type === 'name');
        return nameLabel?.text ?? ''
    }




    getDefaultValues = (type) => {
        switch (type) {
            case Node.nodeType:
                return { name: 'Node', colorName: 'DeepPurple' }
            case Node.userType:
                return { name: 'External User', colorName: 'BlueGrey' }
            case Node.externalType:
                return { name: 'External System', colorName: 'BlueGrey' }
            default:
                throw new Error('Unknown type: ' + type);
        }
    }
}



class Figures {
    // constructor(canvas) {
    //     this.canvas = canvas
    // }

    serializeFigures = (canvas) => {
        return canvas.getFigures().asArray().map((f) => this.serializeFigure(f));
    }

    deserializeFigures = (figures) => {
        return figures.map(f => this.deserializeFigure(f)).filter(f => f != null)
    }


    serializeFigure = (f) => {
        //console.log('figure', f)
        const children = f.getChildren().asArray()
        const nameLabel = children.find(c => c.userData?.type === 'name');
        const descriptionLabel = children.find(c => c.userData?.type === 'description');
        return {
            type: f.userData.type,
            id: f.id,
            x: f.x,
            y: f.y,
            w: f.width,
            h: f.height,
            name: nameLabel?.text ?? '',
            description: descriptionLabel?.text ?? '',
            color: f.userData.color
        };
    }


    deserializeFigure = (f) => {
        let figure
        switch (f.type) {
            case nodeType:
                figure = createNode(f.id, f.w, f.h, f.name, f.description, f.color)
                break;
            case userType:
                figure = createUserNode(f.id, f.w, f.h, f.name, f.description, f.color)
                break;
            case externalType:
                figure = createExternalNode(f.id, f.w, f.h, f.name, f.description, f.color)
                break;
            case groupType:
                figure = createGroupNode(f.id, f.w, f.h, f.name, f.description, f.color)
                break;
            default:
                return null
            //throw new Error('Unexpected node typw!');
        }
        figure.x = f.x
        figure.y = f.y
        return figure
    }
}
export const figures = new Figures()




export const getFigureName = (figure) => {
    const children = figure.getChildren().asArray()
    const nameLabel = children.find(c => c.userData?.type === 'name');
    return nameLabel?.text ?? ''
}


export const createDefaultNode = () => {
    return createNode(
        draw2d.util.UUID.create(),
        defaultNodeWidth, defaultNodeHeight,
        //  nodeColor, nodeBorderColor,
        'Node', 'Description',
        "DeepPurple")
}

const createInnerNode = (figure, store) => {
    const name = getFigureName(figure)
    const canvasData = store.read(figure.getId())

    const bgColor = Colors.canvasBackground
    const innerDiagramNode = new InnerDiagram({
        width: figure.width - 4,
        height: figure.height - 4,
        keepAspectRatio: true,
        color: 'none',
        bgColor: bgColor,
        radius: 5,
        userData: { type: 'svg', color: "BlueGrey" },
    },
        canvasData, name, () => hideInnerDiagram(figure))
    return innerDiagramNode
}


export const createDefaultSystemNode = () => {
    return createNode(
        draw2d.util.UUID.create(),
        defaultNodeWidth, defaultNodeHeight,
        //  nodeColor, nodeBorderColor,
        'System', 'Description',
        "DeepPurple")
}


export const createDefaultUserNode = () => {
    return createUserNode(
        draw2d.util.UUID.create(),
        defaultNodeWidth, defaultNodeHeight,
        // userNodeColor, userNodeBorderColor,
        'External User', 'Description',
        "BlueGrey")
}


export const createDefaultExternalNode = () => {
    return createExternalNode(
        draw2d.util.UUID.create(),
        defaultNodeWidth, defaultNodeHeight,
        // externalNodeColor, externalNodeBorderColor,
        'External System', 'Description',
        "BlueGrey")
}

export const createDefaultGroupNode = (name) => {
    return createGroupNode(
        draw2d.util.UUID.create(),
        defaultGroupNodeWidth, defaultGroupNodeHeight,
        name, 'Description',
        "BlueGrey")
}

export const createGroupNode = (id, width, height, name, description, colorName) => {
    const color = Colors.canvasBackground
    const borderColor = Colors.canvasText
    const fontColor = borderColor

    const figure = new draw2d.shape.composite.Raft({
        id: id,
        width: width, height: height,
        bgColor: color, alpha: 0.5, color: borderColor, dasharray: '- ',
        radius: 5,
        userData: { type: groupType, color: colorName }
    });
    figure.setDeleteable(false)

    const nameLabel = new draw2d.shape.basic.Label({
        text: name, stroke: 0,
        fontSize: 30, fontColor: fontColor, bold: true,
        userData: { type: "name" }
    })

    figure.on("click", function (emitter, event) {
        PubSub.publish('canvas.SetReadOnlyMode', figure)
    });

    figure.on("dblclick", function (emitter, event) {
        PubSub.publish('canvas.AddDefaultNode', { x: event.x, y: event.y })
    });


    const nameLocator = new GroupNameLocator()
    figure.add(nameLabel, nameLocator);
    return figure
}


export const createNode = (id, width, height, name, description, colorName) => {
    return createCommonNode(nodeType, id, width, height, name, description, colorName,
        null)
}


export const createUserNode = (id, width, height, name, description, colorName) => {
    return createCommonNode(userType, id, width, height, name, description, colorName, newUserIcon())
}


export const createExternalNode = (id, width, height, name, description, colorName) => {
    return createCommonNode(externalType, id, width, height, name, description, colorName, newExternalIcon())
}


export const setNodeColor = (figure, colorName) => {
    figure.setBackgroundColor(Colors.getNodeColor(colorName))
    const children = figure.getChildren().asArray()
    const nameLabel = getNameLabel(figure)
    const descriptionLabel = getDescriptionLabel(figure);
    const icon = children.find(c => c instanceof draw2d.shape.icon.Icon);
    const diagramIcon = getDiagramIcon(figure);

    nameLabel?.setFontColor(Colors.getNodeFontColor(colorName))
    descriptionLabel?.setFontColor(Colors.getNodeFontColor(colorName))
    //icon?.setBackgroundColor(getNodeColor(colorName))
    icon?.setColor(Colors.getNodeFontColor(colorName))
    // diagramIcon?.setBackgroundColor(getNodeColor(colorName))
    diagramIcon?.setColor(Colors.getNodeFontColor(colorName))

    figure.setUserData({ ...figure.getUserData(), color: colorName })
}

const getNameLabel = (figure) => {
    return figure.getChildren().asArray()
        .find(c => c.userData?.type === 'name')
}

const getDescriptionLabel = (figure) => {
    return figure.getChildren().asArray()
        .find(c => c.userData?.type === 'description')
}

const getDiagramIcon = (figure) => {
    return figure.getChildren().asArray()
        .find(c => c instanceof draw2d.shape.icon.Diagram)
}


const createCommonNode = (type, id, width, height, name, description, colorName, icon) => {
    const color = Colors.getNodeColor(colorName)
    const borderColor = Colors.getNodeBorderColor(colorName)
    const fontColor = Colors.getNodeFontColor(colorName)
    const figure = new draw2d.shape.node.Between({
        id: id,
        width: width, height: height,
        bgColor: color, color: borderColor,
        radius: 5,
        userData: { type: type, color: colorName }
    });

    addFigureLabels(figure, fontColor, name, description, colorName)
    if (icon != null) {
        addIcon(figure, icon, fontColor, color);
    }
    if (type === nodeType) {
        addInnerDiagramIcon(figure, fontColor, color)
    }

    figure.on("click", function (emitter, event) {
        console.log('click node')
    });
    figure.on("dblclick", function (emitter, event) {
        console.log('double click node')
    });

    addPorts(figure)
    return figure
}


const addFigureLabels = (figure, color, name, description) => {
    const nameLabel = new draw2d.shape.basic.Label({
        text: name, stroke: 0,
        fontSize: 20, fontColor: color, bold: true,
        userData: { type: "name" }
    })

    const descriptionLabel = new draw2d.shape.basic.Text({
        text: description, stroke: 0,
        fontSize: 14, fontColor: color, bold: false,
        userData: { type: "description" }
    })

    nameLabel.installEditor(new draw2d.ui.LabelInplaceEditor());
    figure.add(nameLabel, labelLocator(7));
    descriptionLabel.installEditor(new draw2d.ui.LabelInplaceEditor());
    figure.add(descriptionLabel, labelLocator(30));
}


const addIcon = (figure, icon, color, bgColor) => {
    icon.attr({ width: 18, height: 15, color: color, bgColor: 'none' })
    const iconLocator = new draw2d.layout.locator.XYRelPortLocator(1, 1)
    figure.add(icon, iconLocator)
}


const addInnerDiagramIcon = (figure, color, bgColor) => {
    const icon = new draw2d.shape.icon.Diagram({
        width: 20, height: 20, color: color, bgColor: 'none',
    })

    icon.on("click", () => showInnerDiagram(figure, store))

    const locator = new InnerDiagramIconLocator()
    figure.add(icon, locator)
}


export const showInnerDiagram = (figure, store) => {
    const t = timing()

    getNameLabel(figure)?.setVisible(false)
    getDescriptionLabel(figure)?.setVisible(false)
    getDiagramIcon(figure)?.setVisible(false)

    const innerDiagramNode = createInnerNode(figure, store)
    innerDiagramNode.onClick = onClickHandler(
        () => hideInnerDiagram(figure),
        () => editInnerDiagram(figure))
    figure.innerDiagram = innerDiagramNode
    figure.add(innerDiagramNode, new InnerDiagramLocator())
    figure.repaint()
    t.log('added')
}



const hideInnerDiagram = (figure) => {
    const t = timing()

    getNameLabel(figure)?.setVisible(true)
    getDescriptionLabel(figure)?.setVisible(true)
    getDiagramIcon(figure)?.setVisible(true)

    figure.remove(figure.innerDiagram)
    figure.innerDiagram = null
    t.log()
}

const editInnerDiagram = (figure) => {
    PubSub.publish('canvas.EditInnerDiagram', figure)
}

export const getCanvasFiguresRect = (canvas) => {
    let minX = 10000
    let minY = 10000
    let maxX = 0
    let maxY = 0

    canvas.getFigures().each((i, f) => {
        let fx = f.getAbsoluteX()
        let fy = f.getAbsoluteY()
        let fx2 = fx + f.getWidth()
        let fy2 = fy + f.getHeight()

        if (i === 0) {
            minX = fx
            minY = fy
            maxX = fx2
            maxY = fy2
            return
        }

        if (fx < minX) {
            minX = fx
        }
        if (fy < minY) {
            minY = fy
        }
        if (fx2 > maxX) {
            maxX = fx2
        }
        if (fy2 > maxY) {
            maxY = fy2
        }
    })

    return { x: minX, y: minY, w: maxX - minX, h: maxY - minY, x2: maxX, y2: maxY }
}





const addPorts = (figure) => {
    figure.createPort("input", new InputTopPortLocator());
    figure.createPort("output", new OutputBottomPortLocator());
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


const InnerDiagramIconLocator = draw2d.layout.locator.PortLocator.extend({
    init: function () {
        this._super();
    },
    relocate: function (index, figure) {
        const parent = figure.getParent()
        this.applyConsiderRotation(figure, parent.getWidth() / 2 - 10, parent.getHeight() - 25);
    }
});

const InputTopPortLocator = draw2d.layout.locator.PortLocator.extend({
    init: function () {
        this._super();
    },
    relocate: function (index, figure) {
        this.applyConsiderRotation(figure, figure.getParent().getWidth() / 2, 0);
    }
});


const OutputBottomPortLocator = draw2d.layout.locator.PortLocator.extend({
    init: function () {
        this._super();
    },
    relocate: function (index, figure) {
        var p = figure.getParent();
        this.applyConsiderRotation(figure, p.getWidth() / 2, p.getHeight());
    }
});

const GroupNameLocator = draw2d.layout.locator.Locator.extend({
    init: function () {
        this._super();
    },
    relocate: function (index, target) {
        let targetBoundingBox = target.getBoundingBox()
        target.setPosition(0, -(targetBoundingBox.h - 36))
    }
});


const InnerDiagramLocator = draw2d.layout.locator.Locator.extend({
    init: function () {
        this._super();
    },
    relocate: function (index, target) {
        // let parentBoundingBox = target.getParent().getBoundingBox()
        target.setPosition(2, 2)
    }
});