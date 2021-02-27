import draw2d from "draw2d";

import {
    blueGrey, deepPurple, indigo, pink, purple, red, blue, cyan,
    teal, green, brown, lightGreen, orange, deepOrange, grey
} from '@material-ui/core/colors';


const shade = 600
const diagramBackgroundShade = 50
const darkerBorder = 0.8

export const canvasDivBackground = blueGrey[diagramBackgroundShade]
export const canvasBackground = new draw2d.util.Color(canvasDivBackground)


export const NodeColors = {
    Red: new draw2d.util.Color(red[shade]),
    Pink: new draw2d.util.Color(pink[shade]),
    Purple: new draw2d.util.Color(purple[shade]),
    DeepPurple: new draw2d.util.Color(deepPurple[shade]),
    Indigo: new draw2d.util.Color(indigo[shade]),
    Blue: new draw2d.util.Color(blue[shade]),
    Cyan: new draw2d.util.Color(cyan[shade]),
    Teal: new draw2d.util.Color(teal[shade]),
    Green: new draw2d.util.Color(green[shade]),
    LightGreen: new draw2d.util.Color(lightGreen[shade]),
    Orange: new draw2d.util.Color(orange[shade]),
    DeepOrange: new draw2d.util.Color(deepOrange[shade]),
    Brown: new draw2d.util.Color(brown[shade]),
    Grey: new draw2d.util.Color(grey[shade]),
    BlueGrey: new draw2d.util.Color(blueGrey[shade])
}

export const nodeColorNames = () => {
    return Object.entries(NodeColors).map(e => e[0])
}

export const getNodeColor = (name) => {
    return NodeColors[name]
}

const createNodeFontColors = () => {
    const colors = {}

    nodeColorNames().forEach(name => colors[name] = getNodeColor(name).getIdealTextColor())
    return colors
}
export const NodeFontColors = createNodeFontColors()
export const getNodeFontColor = (name) => {
    return NodeFontColors[name]
}

const createNodeBorderColors = () => {
    const colors = {}

    nodeColorNames().forEach(name => colors[name] = getNodeColor(name).darker(darkerBorder))
    return colors
}
export const NodeBorderColors = createNodeBorderColors()
export const getNodeBorderColor = (name) => {
    return NodeBorderColors[name]
}
