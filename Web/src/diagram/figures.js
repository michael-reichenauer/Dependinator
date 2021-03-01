import draw2d from "draw2d";
import { getNodeColor, getNodeFontColor, getNodeBorderColor, canvasDivBackground, canvasBackground } from "./colors";
import { connectionColor } from './connections'
import PubSub from 'pubsub-js'

export const defaultGroupNodeWidth = 1000
export const defaultGroupNodeHeight = 800
export const defaultNodeWidth = 230
const defaultNodeHeight = 150
const nodeType = 'node'
const userType = 'user'
const externalType = 'external'
export const groupType = 'group'
const newUserIcon = () => new draw2d.shape.icon.User()
const newExternalIcon = () => new draw2d.shape.icon.NewWindow()


export const serializeFigures = (canvas) => {
    return canvas.getFigures().asArray().map((f) => serializeFigure(f));
}

const serializeFigure = (f) => {
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


export const getFigureName = (figure) => {
    const children = figure.getChildren().asArray()
    const nameLabel = children.find(c => c.userData?.type === 'name');
    return nameLabel?.text ?? ''
}

export const deserializeFigures = (figures) => {
    return figures.map(f => deserializeFigure(f))
}

const deserializeFigure = (f) => {
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



export const createDefaultNode = () => {
    const storeName = 'diagram'
    let canvasText = localStorage.getItem(storeName)

    if (canvasText == null) {
        console.log('no stored diagram for', storeName)
        return null
    }
    //console.log('saved', canvasText)
    const canvasData = JSON.parse(canvasText)
    if (canvasData == null || canvasData.figures == null || canvasData.figures.lengths === 0) {
        console.log('no diagram could be parsed (or no figures) for', storeName)
        return false
    }


    console.log('data', canvasData)
    return new InnerDiagram({
        width: defaultGroupNodeWidth,
        height: defaultGroupNodeHeight,
        userData: { type: 'svg', color: "BlueGrey" },
    }, canvasData)


    // return createNode(
    //     draw2d.util.UUID.create(),
    //     defaultNodeWidth, defaultNodeHeight,
    //     //  nodeColor, nodeBorderColor,
    //     'Node', 'Description',
    //     "DeepPurple")
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
    const type = groupType
    const color = canvasBackground
    const borderColor = color.darker(0.8)
    const fontColor = borderColor

    const figure = new draw2d.shape.composite.Raft({
        id: id,
        width: width, height: height,
        bgColor: color, color: borderColor, dasharray: '- ',
        radius: 5,
        userData: { type: type, color: colorName }
    });
    figure.setDeleteable(false)

    const nameLabel = new draw2d.shape.basic.Label({
        text: name, stroke: 0,
        fontSize: 14, fontColor: fontColor, bold: true,
        userData: { type: "name" }
    })

    const nameLocator = new GroupNameLocator()
    figure.add(nameLabel, nameLocator);

    const icon = new draw2d.shape.icon.Contract({
        width: 15, height: 15, color: fontColor, bgColor: canvasBackground,
    })
    icon.onClickDiagram = () => PubSub.publish('diagram.CloseInnerDiagram')
    const locator = new ContractIconLocator()
    figure.add(icon, locator)

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
    figure.setBackgroundColor(getNodeColor(colorName))
    const children = figure.getChildren().asArray()
    const nameLabel = children.find(c => c.userData?.type === 'name');
    const descriptionLabel = children.find(c => c.userData?.type === 'description');
    const icon = children.find(c => c instanceof draw2d.shape.icon.Icon);
    const diagramIcon = children.find(c => c instanceof draw2d.shape.icon.Diagram);

    nameLabel?.setFontColor(getNodeFontColor(colorName))
    descriptionLabel?.setFontColor(getNodeFontColor(colorName))
    icon?.setBackgroundColor(getNodeColor(colorName))
    icon?.setColor(getNodeFontColor(colorName))
    diagramIcon?.setBackgroundColor(getNodeColor(colorName))
    diagramIcon?.setColor(getNodeFontColor(colorName))

    figure.setUserData({ ...figure.getUserData(), color: colorName })
}


const createCommonNode = (type, id, width, height, name, description, colorName, icon) => {
    const color = getNodeColor(colorName)
    const borderColor = getNodeBorderColor(colorName)
    const fontColor = getNodeFontColor(colorName)
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
    addInnerDiagramIcon(figure, fontColor, color)

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
    icon.attr({ width: 18, height: 15, color: color, bgColor: bgColor })
    const iconLocator = new draw2d.layout.locator.XYRelPortLocator(1, 1)
    figure.add(icon, iconLocator)
}


const addInnerDiagramIcon = (figure, color, bgColor) => {
    const icon = new draw2d.shape.icon.Diagram({
        width: 20, height: 20, color: color, bgColor: bgColor,
    })

    icon.onClickDiagram = () => PubSub.publish('diagram.ShowInnerDiagram', figure)
    const locator = new InnerDiagramLocator()
    figure.add(icon, locator)
}

export const getCanvasFiguresRect = (canvas) => {
    let minX = 10000
    let minY = 10000
    let maxX = 0
    let maxY = 0

    canvas.getFigures().each((i, f) => {
        let fx = f.getAbsoluteX()
        let fy = f.getAbsoluteY()
        let fw = fx + f.getWidth()
        let fh = fy + f.getHeight()

        if (i === 0) {
            minX = fx
            minY = fy
            maxX = fw
            maxY = fh
            return
        }

        if (fx < minX) {
            minX = fx
        }
        if (fy < minY) {
            minY = fy
        }
        if (fw > maxX) {
            maxX = fw
        }
        if (fh > maxY) {
            maxY = fh
        }
    })

    return { x: minX, y: minY, w: maxX - minX, h: maxY - minY }
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


const InnerDiagramLocator = draw2d.layout.locator.PortLocator.extend({
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
        target.setPosition(0, -(targetBoundingBox.h + 2))
    }
});

const ContractIconLocator = draw2d.layout.locator.Locator.extend({
    init: function () {
        this._super();
    },
    relocate: function (index, target) {
        target.setPosition(6, 5)
    }
});


const InnerDiagram = draw2d.SetFigure.extend({
    NAME: "InnerDiagram",

    init: function (attr, canvasData) {
        this._super(attr);
        this.canvasData = canvasData
    },

    createSet: function () {
        var set = this.canvas.paper.set()

        // Set the group node
        set.push(this.rect({
            x: 0, y: 0, width: defaultGroupNodeWidth, height: defaultGroupNodeHeight,
            "stroke-width": "1", 'stroke-dasharray': '- ', r: 5, fill: canvasDivBackground
        }))

        console.log('this.figures', this.canvasData.figures)
        this.canvasData.figures.forEach(f => this.deserializeFigure(set, f))
        this.canvasData.connections.forEach(c => this.deserializeConnection(set, c))

        // set.push(this.node({
        //     x: 30, y: 30, width: defaultNodeWidth, height: defaultNodeHeight, r: 5, fill: "#4f6870",
        //     stroke: "#1b1b1b", "stroke-width": "0.5",
        // }))
        // set.push(this.text({ x: defaultNodeWidth / 2 + 30, y: 55, text: 'External User', fill: 'white', 'font-size': 20, 'font-weight': 'bold' }))

        // this.push(this.rect({
        //     x: 2000, y: 200, width: defaultNodeWidth, height: defaultNodeHeight, r: 5, fill: "#4f6870",
        //     stroke: "#1b1b1b", "stroke-width": "0.5",
        // }))

        return set;
    },


    deserializeFigure: function (set, f) {
        // console.log('figure', f)
        switch (f.type) {
            case nodeType:
            case userType:
            case externalType:
                set.push(this.node(f.x - 5100, f.y - 5000, f.w, f.h, f.color))
                set.push(this.nodeName(f.x - 5100, f.y - 5000, f.w, f.name, f.color))
                break;
            default:
                return null
            //throw new Error('Unexpected node typw!');
        }
    },

    deserializeConnection: function (set, c) {
        console.log('connection', c)
        let pathText = null
        c.v.forEach(v => {
            if (pathText === null) {
                pathText = `M${v.x - 5100},${v.y - 5000}`
            } else {
                pathText = pathText + `L${v.x - 5100},${v.y - 5000}`
            }
        })

        const path = this.canvas.paper.path(pathText);
        path.attr({ "stroke-width": 2, "stroke": connectionColor })

        set.push(path)
    },



    text: function (attr) {
        const f = this.canvas.paper.text()
        f.attr(attr)
        return f
    },

    nodeName: function (x, y, w, name, colorName) {
        const fontColor = '#' + getNodeFontColor(colorName).hex()
        const f = this.canvas.paper.text()
        f.attr({
            x: w / 2 + x, y: y + 25, text: name, fill: fontColor,
            'font-size': 20, 'font-weight': 'bold'
        })
        return f
    },



    node: function (x, y, w, h, colorName) {
        const color = '#' + getNodeColor(colorName).hex()
        const borderColor = '#' + getNodeBorderColor(colorName).hex()
        const f = this.canvas.paper.rect()
        f.attr({
            x: x, y: y, width: w, height: h,
            "stroke-width": 1, r: 5,
            fill: color, stroke: borderColor
        })
        return f
    },


    rect: function (attr) {
        const f = this.canvas.paper.rect()
        f.attr(attr)
        return f
    }

});