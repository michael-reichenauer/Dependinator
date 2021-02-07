import draw2d from "draw2d";

const w = 200
const h = 150

export const addNode = (canvas, p) => {
    const figure = new draw2d.shape.node.Between({
        width: w, height: h,
        color: '#4B0082', bgColor: '#9370DB'
    });
    hidePortsIfReadOnly(canvas, figure)

    canvas.add(figure, p.x - w / 2, p.y - h / 2);
}

export const addUserNode = (canvas, p) => {
    const figure = new draw2d.shape.node.Between({
        width: w, height: h,
        color: '#8B4513', bgColor: '#DEB887'
    });
    hidePortsIfReadOnly(canvas, figure)
    canvas.add(figure, p.x - w / 2, p.y - h / 2);
}

export const addExternalNode = (canvas, p) => {
    const figure = new draw2d.shape.node.Between({
        width: w, height: h,
        color: '#A9A9A9', bgColor: '#D3D3D3'
    });
    hidePortsIfReadOnly(canvas, figure)
    canvas.add(figure, p.x - w / 2, p.y - h / 2);
}


const hidePortsIfReadOnly = (canvas, figure) => {
    if (canvas.isReadOnlyMode) {
        figure.getPorts().each((i, port) => { port.setVisible(false) })
    }
}
