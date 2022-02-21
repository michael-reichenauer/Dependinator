import draw2d from "draw2d";
import Connection from "./Connection";
import Colors from "./Colors";
import Group from "./Group";
import Node from "./Node";
import NodeGroup from "./NodeGroup";
import NodeNumber from "./NodeNumber";
import Canvas from "./Canvas";
import { Figure2d, Line2d, Box } from "./draw2dTypes";
import { CanvasDto, ConnectionDto, FigureDto } from "./StoreDtos";

export default class CanvasSerializer {
  canvas: Canvas;

  constructor(canvas: Canvas) {
    this.canvas = canvas;
  }

  serialize(): CanvasDto {
    // If canvas is a group, mark all nodes within the group as group to be included in data
    const node = this.canvas.getFigure(this.canvas.mainNodeId);
    if (node instanceof Group) {
      node
        .getAboardFigures(true)
        .each((_: number, f: Figure2d) => (f.group = node));
    }

    const canvasDto: CanvasDto = {
      id: this.canvas.canvasId ?? "",
      rect: this.canvas.getFiguresRect(),
      figures: this.serializeFigures(),
      connections: this.serializeConnections(),
    };

    // Unmark all nodes
    this.canvas.getFigures().each((_: number, f: Figure2d) => (f.group = null));
    // console.log('data', canvasData)
    return canvasDto;
  }

  deserialize(canvasDto: CanvasDto): void {
    // console.log('data', canvasData)
    this.canvas.canvasId = canvasDto.id;

    // const figures = this.deserializeFigures(canvasData.figures)
    // figures.forEach(figure => this.canvas.add(figure));

    // const connection = this.deserializeConnections(canvasData.connections)
    // connection.forEach(connection => this.canvas.add(connection));
    this.canvas.addAll(this.deserializeFigures(canvasDto.figures));

    this.canvas.addAll(this.deserializeConnections(canvasDto.connections));
  }

  export(
    rect: Box,
    width: number,
    height: number,
    margin: number,
    resultHandler: (svgText: string) => void
  ): void {
    var writer = new draw2d.io.svg.Writer();
    writer.marshal(this.canvas, (svg: string) => {
      // console.log('svg org:', svg)

      const areaWidth = width + margin * 2;
      const areaHeight = height + margin * 2;
      if (rect.w < areaWidth && rect.h < areaHeight) {
        // Image smaller than area; Center image and resize to normal size
        const xd = areaWidth - rect.w;
        const yd = areaHeight - rect.h;

        rect.x = rect.x - xd / 2;
        rect.y = rect.y - yd / 2;
        rect.w = rect.w + xd;
        rect.h = rect.h + yd;
      } else {
        // Image larger than area; Resize and add margin for image larger than area
        rect.x = rect.x - margin;
        rect.y = rect.y - margin;
        rect.w = rect.w + margin * 2;
        rect.h = rect.h + margin * 2;
      }

      // Export size (A4) and view box
      const prefix = `<svg width="${width}" height="${height}" version="1.1" viewBox="${rect.x} ${rect.y} ${rect.w} ${rect.h}" `;

      // Replace svg size with A4 size and view box
      const index = svg.indexOf('xmlns="http://www.w3.org/2000/svg"');
      let res = prefix + svg.substr(index);

      // Adjust style for color and page brake
      res = res.replace(
        'style="',
        `style="background-color:${Colors.canvasDivBackground};`
      );
      res = res.replace('style="', `style="page-break-after: always;`);

      // Remove org view box (if it exists)
      res = res.replace('viewBox="0 0 10000 10000"', "");

      resultHandler(res);
    });
  }

  serializeFigures = (): FigureDto[] => {
    const figures = this.canvas.getFigures().clone();
    figures.sort((a: Figure2d, b: Figure2d) => {
      // return 1  if a before b
      // return -1 if b before a
      return a.getZOrder() > b.getZOrder() ? 1 : -1;
    });

    return figures
      .asArray()
      .map((figure: Figure2d): FigureDto => figure.serialize());
  };

  deserializeFigures = (figures: FigureDto[]): Figure2d[] => {
    return figures
      .map((f: FigureDto): Figure2d => this.deserializeFigure(f))
      .filter((f: Figure2d) => f != null);
  };

  deserializeFigure = (f: FigureDto): Figure2d => {
    let figure;
    if (f.type === Group.groupType) {
      figure = Group.deserialize(f);
    } else if (f.type === NodeGroup.nodeType) {
      figure = NodeGroup.deserialize(f);
    } else if (f.type === NodeNumber.nodeType) {
      figure = NodeNumber.deserialize(f);
    } else {
      figure = Node.deserialize(f);
    }

    figure.x = f.rect.x;
    figure.y = f.rect.y;
    return figure;
  };

  serializeConnections(): ConnectionDto[] {
    return this.canvas
      .getLines()
      .asArray()
      .map((connection: Line2d) => connection.serialize());
  }

  deserializeConnections(connections: ConnectionDto[]): Line2d[] {
    return connections
      .map((c: ConnectionDto) => Connection.deserialize(this.canvas, c))
      .filter((c: Line2d) => c != null);
  }
}
