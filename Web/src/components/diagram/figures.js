import draw2d from "draw2d";
import { getNodeColor, getNodeFontColor, getNodeBorderColor } from "./colors";
import { Tweenable } from "shifty"

const defaultNodeWidth = 230
const defaultNodeHeight = 150
const nodeType = 'node'
const userType = 'user'
const externalType = 'external'
const newUserIcon = () => new draw2d.shape.icon.User()
const newExternalIcon = () => new draw2d.shape.icon.NewWindow()


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
            case nodeType:
                figure = createNode(f.id, f.w, f.h, f.name, f.description, f.color)
                break;
            case userType:
                figure = createUserNode(f.id, f.w, f.h, f.name, f.description, f.color)
                break;
            case externalType:
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


export const createDefaultNode = () => {
    return createNode(
        draw2d.util.UUID.create(),
        defaultNodeWidth, defaultNodeHeight,
        //  nodeColor, nodeBorderColor,
        'Node', 'Description',
        "DeepPurple")
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

    icon.onClickDiagram = () => zoomAndMoveShowInnerDiagram(figure, icon)
    const locator = new InnerDiagramLocator()
    figure.add(icon, locator)
}

const zoomAndMoveShowInnerDiagram = (figure, icon) => {
    const canvas = icon.getCanvas()
    let tweenable = new Tweenable()

    const targetZoom = 0.2 * figure.width / defaultNodeWidth
    let area = canvas.getScrollArea()


    const fc = { x: figure.x + figure.width / 2, y: figure.y + figure.height / 2 }
    const cc = { x: canvas.getWidth() / 2, y: canvas.getHeight() / 2 }

    tweenable.tween({
        from: { 'zoom': canvas.zoomFactor },
        to: { 'zoom': targetZoom },
        duration: 2000,
        easing: "easeOutSine",
        step: params => {
            canvas.setZoom(params.zoom, false)

            // Scroll figure to center
            const tp = { x: fc.x - cc.x * params.zoom, y: fc.y - cc.y * params.zoom }
            // canvas.scrollTo((tp.x) / params.zoom, (tp.y) / params.zoom)
            area.scrollLeft((tp.x) / params.zoom)
            area.scrollTop((tp.y) / params.zoom)
        },
        finish: state => {
        }
    })

}

export const updateCanvasMaxFigureSize = (canvas) => {
    let x = 10000
    let y = 10000
    let w = 0
    let h = 0

    canvas.getFigures().each((i, f) => {
        let fx = f.getAbsoluteX()
        let fy = f.getAbsoluteY()
        let fw = fx + f.getWidth()
        let fh = fy + f.getHeight()

        if (i === 0) {
            x = fx
            y = fy
            w = fw
            h = fh
            return
        }

        if (fw > w) {
            w = fw
        }
        if (fh > h) {
            h = fh
        }
        if (fx < x) {
            x = fx
        }
        if (fy < y) {
            y = fy
        }
    })
    canvas.minFigureX = x
    canvas.minFigureY = y
    canvas.maxFigureWidth = w
    canvas.maxFigureHeight = h
    // console.log('figure size', w, h)
}

export const zoomAndMoveShowTotalDiagram = (canvas) => {
    updateCanvasMaxFigureSize(canvas)
    let tweenable = new Tweenable()
    let area = canvas.getScrollArea()

    const fc = {
        x: canvas.minFigureX + (canvas.maxFigureWidth - canvas.minFigureX) / 2,
        y: canvas.minFigureY + (canvas.maxFigureHeight - canvas.minFigureY) / 2
    }
    const cc = { x: canvas.getWidth() / 2, y: canvas.getHeight() / 2 }

    const targetZoom = Math.max(1,
        (canvas.maxFigureWidth - canvas.minFigureX) / (canvas.getWidth() - 100),
        (canvas.maxFigureHeight - canvas.minFigureY) / (canvas.getHeight() - 100))

    tweenable.tween({
        from: { 'zoom': canvas.zoomFactor },
        to: { 'zoom': targetZoom },
        duration: 2000,
        easing: "easeOutSine",
        step: params => {
            canvas.setZoom(params.zoom, false)

            // Scroll figure to center
            const tp = { x: fc.x - cc.x * params.zoom, y: fc.y - cc.y * params.zoom }
            // canvas.scrollTo((tp.x) / params.zoom, (tp.y) / params.zoom)
            area.scrollLeft((tp.x) / params.zoom)
            area.scrollTop((tp.y) / params.zoom)
        },
        finish: state => {
        }
    })

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