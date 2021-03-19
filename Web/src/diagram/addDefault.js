import draw2d from "draw2d";
import Connection from "./Connection";
import Group from "./Group";
import Node from "./Node";
import { zoomAndMoveShowTotalDiagram } from "./showTotalDiagram";


export const addFigureToCanvas = (canvas, figure, p) => {
    const command = new draw2d.command.CommandAdd(canvas, figure, p.x - figure.width / 2, p.y - figure.height / 2);
    canvas.getCommandStack().execute(command);
}


export const addDefaultNewDiagram = (canvas) => {
    const user = new Node(Node.userType)
    const system = new Node(Node.nodeType)
    const external = new Node(Node.externalType)
    const b = canvas.getDimension().getWidth() / 2
    addFigureToCanvas(canvas, user, { x: b + 200, y: b + 400 })
    addFigureToCanvas(canvas, system, { x: b + 600, y: b + 400 })
    addConnectionToCanvas(canvas, new Connection(null, user, 'output0', system, 'input0'))
    addFigureToCanvas(canvas, external, { x: b + 1000, y: b + 400 })
    addConnectionToCanvas(canvas, new Connection(null, system, 'output0', external, 'input0'))

    zoomAndMoveShowTotalDiagram(canvas)
}

export const addDefaultInnerDiagram = (canvas, name) => {
    // Add a default group at the center of the canvas
    const group = new Group(name)
    const d = canvas.getDimension()
    const gx = d.getWidth() / 2 + (canvas.getWidth() - 1000) / 2
    const gy = d.getHeight() / 2 + 250
    canvas.add(group, gx, gy)

    // Add a default node in the center of the group
    const node = new Node(Node.nodeType)
    const nx = gx + group.getWidth() / 2 - node.getWidth() / 2
    const ny = gy + group.getHeight() / 2 - node.getHeight() / 2
    canvas.add(node, nx, ny)

}


const addConnectionToCanvas = (canvas, connection) => {
    const command = new draw2d.command.CommandAdd(canvas, connection, 0, 0);
    canvas.getCommandStack().execute(command);
}
