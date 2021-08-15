import draw2d from "draw2d";
import {
    blueGrey, deepPurple, indigo, pink, purple, blue, cyan,
    teal, green, brown, lightGreen, deepOrange, grey
} from '@material-ui/core/colors';


const shade = 600
const diagramBackgroundShade = 50
const darkerBorder = 0.3


export default class Colors {
    static canvasDivBackground = blueGrey[diagramBackgroundShade]
    static canvasBackground = new draw2d.util.Color(Colors.canvasDivBackground)
    static canvasText = Colors.canvasBackground.getIdealTextColor()
    static canvasGridRgb = Colors.canvasBackground.darker(0.1).rgba()
    static connectionColor = Colors.canvasText.lighter(0.5).rgba()
    static labelColor = Colors.canvasText
    static nodeBorderColor = Colors.canvasBackground.darker(0.1).rgba()


    static NodeColors = {
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

    static NodeFontColors = Colors.createNodeFontColors()
    static NodeBorderColors = Colors.createNodeBorderColors()
    static NodeHexColors = Colors.createNodeHexColors()
    static NodeBorderHexColors = Colors.createNodeBorderHexColors()
    static NodeFontHexColors = Colors.createNodeFontHexColors()

    static nodeColorNames() {
        return Object.entries(Colors.NodeColors).map(e => e[0])
    }
    static getNodeColor(name) {
        return Colors.NodeColors[name]
    }
    static getNodeFontColor(name) {
        return Colors.NodeFontColors[name]
    }
    static getNodeBorderColor(name) {
        return Colors.NodeBorderColors[name]
    }
    static getNodeHexColor(name) {
        return Colors.NodeHexColors[name]
    }
    static getNodeBorderHexColor(name) {
        return Colors.NodeBorderHexColors[name]
    }
    static getNodeFontHexColor(name) {
        return Colors.NodeFontHexColors[name]
    }



    static createNodeBorderColors() {
        const colors = {}
        Colors.nodeColorNames().forEach(name => colors[name] = Colors.getNodeColor(name).darker(darkerBorder))
        return colors
    }

    static createNodeFontColors() {
        const colors = {}
        Colors.nodeColorNames().forEach(name => colors[name] = Colors.getNodeColor(name).getIdealTextColor())
        return colors
    }

    static createNodeHexColors() {
        const colors = {}
        Colors.nodeColorNames().forEach(name => colors[name] = '#' + Colors.getNodeColor(name).hex())
        return colors
    }

    static createNodeBorderHexColors() {
        const colors = {}
        Colors.nodeColorNames().forEach(name => colors[name] = '#' + Colors.getNodeBorderColor(name).hex())
        return colors
    }

    static createNodeFontHexColors() {
        const colors = {}
        Colors.nodeColorNames().forEach(name => colors[name] = '#' + Colors.getNodeFontColor(name).hex())
        return colors
    }
}


