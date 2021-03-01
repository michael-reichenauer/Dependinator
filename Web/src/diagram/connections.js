import draw2d from "draw2d";
import { canvasBackground } from "./colors";

export const connectionColor = canvasBackground.getIdealTextColor()
const labelTextColor = canvasBackground.getIdealTextColor();
const labelColorBackground = canvasBackground;

export const configureDefaultConnection = (connection) => {
    return configureConnection(connection, 'Description')
}


export const createDefaultConnection = (src, srcPortName, dst, dstPortName) => {
    const connection = new draw2d.Connection()
    const srcPort = src.getPort(srcPortName)
    const dstPort = dst.getPort(dstPortName)
    connection.setSource(srcPort)
    connection.setTarget(dstPort)

    configureConnection(connection, 'Description')
    return connection
}

export const serializeConnections = (canvas) => {
    return canvas.getLines().asArray().map((line) => {
        //console.log('connection', line)
        const children = line.getChildren().asArray()
        // console.log('connection children', children)
        const l = line.getPersistentAttributes();
        const descriptionLabel = children.find(c => c.userData?.type === 'description');
        return {
            src: l.source.node,
            srcPort: l.source.port,
            trg: l.target.node,
            trgPort: l.target.port,
            v: l.vertex,
            description: descriptionLabel?.text ?? ''
        }
    });
}


export const deserializeConnections = (canvas, connections) => {
    return connections.map(c => {
        const connection = new draw2d.Connection()
        const src = canvas.getFigure(c.src)
        const srcPort = src.getPort(c.srcPort)
        const trg = canvas.getFigure(c.trg)
        const trgPort = trg.getPort(c.trgPort)
        connection.setSource(srcPort)
        connection.setTarget(trgPort)

        configureConnection(connection, c.description)
        return connection
    }).filter(c => c != null)
}

const configureConnection = (connection, description) => {
    connection.setColor(connectionColor)
    connection.setRouter(new draw2d.layout.connection.VertexRouter());

    const arrow = new draw2d.decoration.connection.ArrowDecorator()
    arrow.setBackgroundColor(connection.getColor())
    arrow.setDimension(12, 12)
    connection.targetDecorator = arrow

    const label = new draw2d.shape.basic.Text({
        text: description, stroke: 0,
        fontSize: 14, bold: false,
        fontColor: labelTextColor, bgColor: labelColorBackground,
        userData: { type: "description" }
    })

    label.installEditor(new draw2d.ui.LabelInplaceEditor());
    connection.add(label, new draw2d.layout.locator.ManhattanMidpointLocator(connection));

    return connection;
}

