import draw2d from "draw2d";

import {
    blueGrey, deepPurple, indigo, pink, purple, red, blue, lightBlue, cyan,
    teal, green, lime, yellow, amber, brown, lightGreen, orange, deepOrange, grey
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
    LightBlue: new draw2d.util.Color(lightBlue[shade]),
    Cyan: new draw2d.util.Color(cyan[shade]),
    Teal: new draw2d.util.Color(teal[shade]),
    Green: new draw2d.util.Color(green[shade]),
    LightGreen: new draw2d.util.Color(lightGreen[shade]),
    Lime: new draw2d.util.Color(lime[shade]),
    Yellow: new draw2d.util.Color(yellow[shade]),
    Amber: new draw2d.util.Color(amber[shade]),
    Orange: new draw2d.util.Color(orange[shade]),
    DeepOrange: new draw2d.util.Color(deepOrange[shade]),
    Brown: new draw2d.util.Color(brown[shade]),
    Grey: new draw2d.util.Color(grey[shade]),
    BlueGrey: new draw2d.util.Color(blueGrey[shade])
}

export const NodeFontColors = {
    Red: NodeColors.Red.getIdealTextColor(),
    Pink: NodeColors.Pink.getIdealTextColor(),
    Purple: NodeColors.Purple.getIdealTextColor(),
    DeepPurple: NodeColors.DeepPurple.getIdealTextColor(),
    Indigo: NodeColors.Indigo.getIdealTextColor(),
    Blue: NodeColors.Blue.getIdealTextColor(),
    LightBlue: NodeColors.LightBlue.getIdealTextColor(),
    Cyan: NodeColors.Cyan.getIdealTextColor(),
    Teal: NodeColors.Teal.getIdealTextColor(),
    Green: NodeColors.Green.getIdealTextColor(),
    LightGreen: NodeColors.LightGreen.getIdealTextColor(),
    Lime: NodeColors.Lime.getIdealTextColor(),
    Yellow: NodeColors.Yellow.getIdealTextColor(),
    Amber: NodeColors.Amber.getIdealTextColor(),
    Orange: NodeColors.Orange.getIdealTextColor(),
    DeepOrange: NodeColors.DeepOrange.getIdealTextColor(),
    Brown: NodeColors.Brown.getIdealTextColor(),
    Grey: NodeColors.Grey.getIdealTextColor(),
    BlueGrey: NodeColors.BlueGrey.getIdealTextColor()
}

export const NodeBorderColors = {
    Red: NodeColors.Red.darker(darkerBorder),
    Pink: NodeColors.Pink.darker(darkerBorder),
    Purple: NodeColors.Purple.darker(darkerBorder),
    DeepPurple: NodeColors.DeepPurple.darker(darkerBorder),
    Indigo: NodeColors.Indigo.darker(darkerBorder),
    Blue: NodeColors.Blue.darker(darkerBorder),
    LightBlue: NodeColors.LightBlue.darker(darkerBorder),
    Cyan: NodeColors.Cyan.darker(darkerBorder),
    Teal: NodeColors.Teal.darker(darkerBorder),
    Green: NodeColors.Green.darker(darkerBorder),
    LightGreen: NodeColors.LightGreen.darker(darkerBorder),
    Lime: NodeColors.Lime.darker(darkerBorder),
    Yellow: NodeColors.Yellow.darker(darkerBorder),
    Amber: NodeColors.Amber.darker(darkerBorder),
    Orange: NodeColors.Orange.darker(darkerBorder),
    DeepOrange: NodeColors.DeepOrange.darker(darkerBorder),
    Brown: NodeColors.Brown.darker(darkerBorder),
    Grey: NodeColors.Grey.darker(darkerBorder),
    BlueGrey: NodeColors.BlueGrey.darker(darkerBorder)
}