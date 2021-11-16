import draw2d from "draw2d";
import {
    blueGrey, deepPurple, indigo, pink, purple, blue, cyan,
    teal, green, brown, lightGreen, deepOrange, grey, red, yellow
} from '@material-ui/core/colors';
import { Color2d } from "./draw2dTypes";


const shade = 600
const background = 50
const darkerBackground = 100
const diagramBackgroundShade = 50
const darkerBorder = 0.3

interface NodeColors{
    Pink: Color2d,
    Purple: Color2d,
    DeepPurple: Color2d,
    Indigo: Color2d,
    Blue: Color2d,
    Cyan: Color2d,
    Teal: Color2d,
    Green: Color2d,
    LightGreen: Color2d,
    DeepOrange: Color2d,
    Brown: Color2d,
    Grey: Color2d,
    BlueGrey: Color2d,
}

interface BackgroundColors{
    None: Color2d,
    Grey: Color2d,
    Red: Color2d,
    Purple: Color2d,
    Blue: Color2d,
    Green: Color2d,
    Yellow: Color2d,
}


export default class Colors {
    static canvasDivBackground = blueGrey[diagramBackgroundShade]
    static canvasBackground = new draw2d.util.Color(Colors.canvasDivBackground)
    static canvasText = Colors.canvasBackground.getIdealTextColor()
    static canvasGridRgb = Colors.canvasBackground.darker(0.1).rgba()
    static connectionColor = Colors.canvasText.lighter(0.5).rgba()
    static labelColor = Colors.canvasText
    static nodeBorderColor = Colors.canvasBackground.darker(0.1).rgba()
    static buttonBackground = new draw2d.util.Color(grey[50])


    static nodeColors:NodeColors = {
        Pink: new draw2d.util.Color(pink[shade]),
        Purple: new draw2d.util.Color(purple[shade]),
        DeepPurple: new draw2d.util.Color(deepPurple[shade]),
        Indigo: new draw2d.util.Color(indigo[shade]),
        Blue: new draw2d.util.Color(blue[shade]),
        Cyan: new draw2d.util.Color(cyan[shade]),
        Teal: new draw2d.util.Color(teal[shade]),
        Green: new draw2d.util.Color(green[shade]),
        LightGreen: new draw2d.util.Color(lightGreen[shade]),
        DeepOrange: new draw2d.util.Color(deepOrange[shade]),
        Brown: new draw2d.util.Color(brown[shade]),
        Grey: new draw2d.util.Color(grey[shade]),
        BlueGrey: new draw2d.util.Color(blueGrey[shade])
    }

    static backgroundColors:BackgroundColors = {
        None: new draw2d.util.Color(blueGrey[background]),
        Grey: new draw2d.util.Color(blueGrey[darkerBackground]),
        Red: new draw2d.util.Color(red[background]),
        Purple: new draw2d.util.Color(deepPurple[background]),
        Blue: new draw2d.util.Color(blue[background]),
        Green: new draw2d.util.Color(green[background]),
        Yellow: new draw2d.util.Color(yellow[background]),
    }

    static NodeFontColors = Colors.createNodeFontColors()
    static NodeBorderColors = Colors.createNodeBorderColors()
    static NodeHexColors = Colors.createNodeHexColors()
    static NodeBorderHexColors = Colors.createNodeBorderHexColors()
    static NodeFontHexColors = Colors.createNodeFontHexColors()

    static nodeColorNames() {
        return Object.entries(Colors.nodeColors).map(e => e[0])
    }
    static backgroundColorNames() {
        return Object.entries(Colors.backgroundColors).map(e => e[0])
    }
    static getNodeColor(name:string) :Color2d {
        // @ts-ignore
        return Colors.nodeColors[name] 
    }
    static getBackgroundColor(name:string):Color2d {
        // @ts-ignore
        return Colors.backgroundColors[name]
    }
    static getNodeFontColor(name:string) {
        return Colors.NodeFontColors[name]
    }
    static getNodeBorderColor(name:string) {
        return Colors.NodeBorderColors[name]
    }
    static getNodeHexColor(name:string) {
        return Colors.NodeHexColors[name]
    }
    static getNodeBorderHexColor(name:string) {
        return Colors.NodeBorderHexColors[name]
    }
    static getNodeFontHexColor(name:string) {
        return Colors.NodeFontHexColors[name]
    }



    static createNodeBorderColors() {
        const colors :any= {}
        Colors.nodeColorNames().forEach(name => colors[name] = Colors.getNodeColor(name).darker(darkerBorder))
        return colors
    }


    static createNodeFontColors() {
        const colors :any = {}
        Colors.nodeColorNames().forEach(name => colors[name] = Colors.getNodeColor(name).getIdealTextColor())
        return colors
    }

    static createNodeHexColors() {
        const colors  :any= {}
        Colors.nodeColorNames().forEach(name => colors[name] = '#' + Colors.getNodeColor(name).hex())
        return colors
    }

    static createNodeBorderHexColors() {
        const colors  :any= {}
        Colors.nodeColorNames().forEach(name => colors[name] = '#' + Colors.getNodeBorderColor(name).hex())
        return colors
    }

    static createNodeFontHexColors() {
        const colors  :any= {}
        Colors.nodeColorNames().forEach(name => colors[name] = '#' + Colors.getNodeFontColor(name).hex())
        return colors
    }
}


