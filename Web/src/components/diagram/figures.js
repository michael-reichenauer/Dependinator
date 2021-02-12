import draw2d from "draw2d";
import { getNodeColor, getNodeFontColor, getNodeBorderColor } from "./colors";

export const defaultNodeWidth = 230
export const defaultNodeHeight = 150


export const serializeFigures = (canvas) => {
    return canvas.getFigures().asArray().map((f) => {
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
    });
}

export const deserializeFigures = (figures) => {
    return figures.map(f => {
        let figure
        switch (f.type) {
            case 'node':
                figure = createNode(f.id, f.w, f.h, f.name, f.description, f.color)
                break;
            case 'user':
                figure = createUserNode(f.id, f.w, f.h, f.name, f.description, f.color)
                break;
            case 'external':
                figure = createExternalNode(f.id, f.w, f.h, f.name, f.description, f.color)
                break;
            default:
                throw new Error('Unexpected node typw!');
        }
        figure.x = f.x
        figure.y = f.y
        return figure
    })
}


export const setNodeColor = (figure, colorName) => {
    figure.setBackgroundColor(getNodeColor(colorName))
    const children = figure.getChildren().asArray()
    const nameLabel = children.find(c => c.userData?.type === 'name');
    const descriptionLabel = children.find(c => c.userData?.type === 'description');
    const icon = children.find(c => c instanceof draw2d.shape.icon.Icon);

    nameLabel?.setFontColor(getNodeFontColor(colorName))
    descriptionLabel?.setFontColor(getNodeFontColor(colorName))
    icon?.setBackgroundColor(getNodeColor(colorName))
    icon?.setColor(getNodeFontColor(colorName))

    figure.setUserData({ ...figure.getUserData(), color: colorName })
}


export const createDefaultNode = () => {
    return createNode(
        draw2d.util.UUID.create(),
        defaultNodeWidth, defaultNodeHeight,
        //  nodeColor, nodeBorderColor,
        'Node', 'Description',
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


export const createNode = (id, width, height, name, description, colorName) => {
    return createCommonNode("node", id, width, height, name, description, colorName,
        null)
}


export const createUserNode = (id, width, height, name, description, colorName) => {
    return createCommonNode("user", id, width, height, name, description, colorName,
        new draw2d.shape.icon.User())
}


export const createExternalNode = (id, width, height, name, description, colorName) => {
    return createCommonNode("external", id, width, height, name, description, colorName,
        new draw2d.shape.icon.NewWindow())
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