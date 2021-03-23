import draw2d from "draw2d";
import Connection from "./Connection";
import Group from "./Group";
import Node from "./Node";
import { zoomAndMoveShowTotalDiagram } from "./showTotalDiagram";


export const addFigureToCanvas = (canvas, figure, p) => {
    const x = p.x - figure.width / 2
    const y = p.y - figure.height / 2
    canvas.runCmd(new draw2d.command.CommandAdd(canvas, figure, x, y))
}


export const addDefaultNewDiagram = (canvas) => {
    // Add a user connected to a system, connected to an external system 
    const marginY = 200
    const user = new Node(Node.userType)
    const system = new Node(Node.nodeType)
    const external = new Node(Node.externalType)

    // Add nodes at the center of the canvas
    const cx = canvas.getDimension().getWidth() / 2
    const cy = canvas.getDimension().getHeight() / 2
    const x = cx
    const y = cy - user.height / 2 - marginY

    addFigureToCanvas(canvas, user, { x: x, y: y })

    addFigureToCanvas(canvas, system, { x: x, y: user.y + user.height + marginY })
    addConnectionToCanvas(canvas, new Connection(null, user, 'output1', system, 'input1'))

    addFigureToCanvas(canvas, external, { x: x, y: system.y + system.height + marginY })
    addConnectionToCanvas(canvas, new Connection(null, system, 'output1', external, 'input1'))

    zoomAndMoveShowTotalDiagram(canvas)
}

export const addDefaultInnerDiagram = (canvas, name, description) => {
    // Add a default group at the center of the canvas
    const group = new Group(name, description)
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
    canvas.runCmd(new draw2d.command.CommandAdd(canvas, connection, 0, 0))
}
