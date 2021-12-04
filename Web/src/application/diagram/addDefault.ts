import draw2d from "draw2d";
import Canvas from "./Canvas";
import Connection from "./Connection";
import { Figure2d, Point } from "./draw2dTypes";
import Group from "./Group";
import Node from "./Node";
import { zoomAndMoveShowTotalDiagram } from "./showTotalDiagram";

const marginY = 200;

export const addFigureToCanvas = (
  canvas: Canvas,
  figure: Figure2d,
  x: number,
  y: number
): void => {
  canvas.runCmd(new draw2d.command.CommandAdd(canvas, figure, x, y));
};

export const addDefaultNewDiagram = (canvas: Canvas) => {
  // Add a system node with a connected external user and external system
  const system = new Node(Node.systemType);
  const user = new Node(Node.userType);
  const external = new Node(Node.externalType);

  // Add nodes at the center of the canvas
  const cx = canvas.getDimension().getWidth() / 2;
  const cy = canvas.getDimension().getHeight() / 2;
  const x = cx;
  const y = cy - user.height / 2 - marginY;

  addNode(canvas, user, { x: x, y: y });
  addNode(canvas, system, { x: x, y: user.y + user.height + marginY });
  addNode(canvas, external, { x: x, y: system.y + system.height + marginY });

  addConnection(canvas, user, system);
  addConnection(canvas, system, external);

  // @ts-ignore
  canvas.canvasId = "root";

  zoomAndMoveShowTotalDiagram(canvas);
};

export const addDefaultInnerDiagram = (
  canvas: Canvas,
  name: string,
  description: string
) => {
  console.log("canvas", canvas);
  // Add a default group at the center of the canvas
  const group = new Group(name, description);
  const d = canvas.getDimension();
  const gx = d.getWidth() / 2 + (canvas.getWidth() - 1000) / 2;
  const gy = d.getHeight() / 2 + 250;
  canvas.add(group, gx, gy);

  // Add a default node in the center of the group
  const node = new Node(Node.nodeType);
  const nx = gx + group.getWidth() / 2 - node.getWidth() / 2;
  const ny = gy + group.getHeight() / 2 - node.getHeight() / 2;
  canvas.add(node, nx, ny);
};

const addNode = (canvas: Canvas, node: Node, p: Point) => {
  const x = p.x - node.width / 2;
  const y = p.y - node.height / 2;
  canvas.add(node, x, y);
};

const addConnection = (canvas: Canvas, src: Node, trg: Node) => {
  canvas.add(new Connection("", "", src, "output1", trg, "input1"));
};
