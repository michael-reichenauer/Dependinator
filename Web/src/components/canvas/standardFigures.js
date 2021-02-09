import draw2d from "draw2d";

const defaultNodeWidth = 200
const defaultNodeHeight = 150

export const addNode = (canvas, p) => {
    const figure = new draw2d.shape.node.Between({
        width: defaultNodeWidth, height: defaultNodeHeight,
        color: '#4B0082', bgColor: '#9370DB'
    });

    addFigureLabels(figure, 'Node')
    figure.createPort("input", new InputTopPortLocator());
    figure.createPort("output", new OutputBottomPortLocator());
    addFigure(canvas, figure, p);
}

export const addUserNode = (canvas, p) => {
    const figure = new draw2d.shape.node.Start({
        width: defaultNodeWidth, height: defaultNodeHeight,
        color: '#111111', bgColor: '#B9B9B9',
        radius: 30,

    });

    addFigureLabels(figure, 'User')
    figure.createPort("output", new OutputBottomPortLocator());
    addFigure(canvas, figure, p);
}

export const addExternalNode = (canvas, p) => {
    const figure = new draw2d.shape.node.Between({
        width: defaultNodeWidth, height: defaultNodeHeight,
        color: '#111111', bgColor: '#B9B9B9'
    });

    addFigureLabels(figure, 'External System')
    figure.createPort("input", new InputTopPortLocator());
    figure.createPort("output", new OutputBottomPortLocator());
    addFigure(canvas, figure, p);
}

const addFigure = (canvas, figure, p) => {
    hidePortsIfReadOnly(canvas, figure)

    const command = new draw2d.command.CommandAdd(canvas, figure, p.x - figure.width / 2, p.y - figure.height / 2);
    canvas.getCommandStack().execute(command);
}

const addFigureLabels = (figure, name) => {
    const nameLabel = new draw2d.shape.basic.Label({
        text: name, stroke: 0,
        fontSize: 20, fontColor: 'white', bold: true
    })

    const descriptionLabel = new draw2d.shape.basic.Text({
        text: "Description", stroke: 0,
        fontSize: 14, fontColor: 'white', bold: false
    })

    nameLabel.installEditor(new draw2d.ui.LabelInplaceEditor());
    figure.add(nameLabel, labelLocator(5));
    descriptionLabel.installEditor(new draw2d.ui.LabelInplaceEditor());
    figure.add(descriptionLabel, labelLocator(30));
}


const hidePortsIfReadOnly = (canvas, figure) => {
    if (canvas.isReadOnlyMode) {
        figure.getPorts().each((i, port) => { port.setVisible(false) })
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