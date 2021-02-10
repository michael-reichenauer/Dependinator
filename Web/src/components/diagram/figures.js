import draw2d from "draw2d";

const defaultNodeWidth = 200
const defaultNodeHeight = 150
const darkerBorder = 0.5
const nodeColor = new draw2d.util.Color("#9370DB");
const nodeBorderColor = nodeColor.darker(darkerBorder);
const userNodeColor = new draw2d.util.Color("#B9B9B9");
const userNodeBorderColor = userNodeColor.darker(darkerBorder);
const externalNodeColor = new draw2d.util.Color("#B9B9B9");
const externalNodeBorderColor = externalNodeColor.darker(darkerBorder);


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
            description: descriptionLabel?.text ?? ''
        };
    });
}

export const deserializeFigures = (figures) => {
    return figures.map(f => {
        let figure
        switch (f.type) {
            case 'node':
                figure = createNode(f.id, f.w, f.h, f.name, f.description)
                break;
            case 'user':
                figure = createUserNode(f.id, f.w, f.h, f.name, f.description)
                break;
            case 'external':
                figure = createExternalNode(f.id, f.w, f.h, f.name, f.description)
                break;
            default:
                throw new Error('Unexpected node typw!');
        }
        figure.x = f.x
        figure.y = f.y
        return figure
    })
}


export const createDefaultNode = () => {
    return createNode(
        draw2d.util.UUID.create(),
        defaultNodeWidth, defaultNodeHeight,
        //  nodeColor, nodeBorderColor,
        'Node', 'Description')
}

export const createDefaultUserNode = () => {
    return createUserNode(
        draw2d.util.UUID.create(),
        defaultNodeWidth, defaultNodeHeight,
        // userNodeColor, userNodeBorderColor,
        'External User', 'Description')
}

export const createDefaultExternalNode = () => {
    return createExternalNode(
        draw2d.util.UUID.create(),
        defaultNodeWidth, defaultNodeHeight,
        // externalNodeColor, externalNodeBorderColor,
        'External System', 'Description')
}


export const createNode = (id, width, height, name, description) => {
    const color = nodeColor
    const borderColor = nodeBorderColor
    const figure = new draw2d.shape.node.Between({
        id: id,
        width: width, height: height,
        bgColor: color, color: borderColor,
        radius: 5,
        userData: { type: "node" }
    });

    addFigureLabels(figure, name, description)
    figure.createPort("input", new InputTopPortLocator());
    figure.createPort("output", new OutputBottomPortLocator());

    return figure
}

export const createUserNode = (id, width, height, name, description) => {
    const color = userNodeColor
    const borderColor = userNodeBorderColor
    const figure = new draw2d.shape.node.Start({
        id: id,
        width: width, height: height,
        bgColor: color, color: borderColor,
        radius: 5,
        userData: { type: "user" }
    });

    addFigureLabels(figure, name, description)
    figure.createPort("output", new OutputBottomPortLocator());

    return figure
}

export const createExternalNode = (id, width, height, name, description) => {
    const color = externalNodeColor
    const borderColor = externalNodeBorderColor
    const figure = new draw2d.shape.node.Between({
        id: id,
        width: width, height: height,
        bgColor: color, color: borderColor,
        radius: 5,
        userData: { type: "external" }
    });

    addFigureLabels(figure, name, description)
    figure.createPort("input", new InputTopPortLocator());
    figure.createPort("output", new OutputBottomPortLocator());

    return figure
}


const addFigureLabels = (figure, name, description) => {
    const nameLabel = new draw2d.shape.basic.Label({
        text: name, stroke: 0,
        fontSize: 20, fontColor: 'white', bold: true,
        userData: { type: "name" }
    })

    const descriptionLabel = new draw2d.shape.basic.Text({
        text: description, stroke: 0,
        fontSize: 14, fontColor: 'white', bold: false,
        userData: { type: "description" }
    })

    nameLabel.installEditor(new draw2d.ui.LabelInplaceEditor());
    figure.add(nameLabel, labelLocator(5));
    descriptionLabel.installEditor(new draw2d.ui.LabelInplaceEditor());
    figure.add(descriptionLabel, labelLocator(30));
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