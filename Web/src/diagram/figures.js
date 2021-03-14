import draw2d from "draw2d";
import Colors from "./colors";
import PubSub from 'pubsub-js'
import { InnerDiagram } from "./innerDiagramFigure";
import { timing } from "../common/timing";
import { onClickHandler } from "../common/mouseClicks";
import Node from "./Node";
import Group from "./Group";


class Figures {
    // constructor(canvas) {
    //     this.canvas = canvas
    // }

    serializeFigures = (canvas) => {
        return canvas.getFigures().asArray().map((f) => this.serializeFigure(f));
    }

    deserializeFigures = (figures) => {
        return figures.map(f => this.deserializeFigure(f)).filter(f => f != null)
    }


    serializeFigure = (f) => {
        //console.log('figure', f)
        return {
            type: f.userData.type, id: f.id, x: f.x, y: f.y, w: f.width, h: f.height,
            name: f.userData.getName(), description: f.userData.getDescription(), color: f.userData.colorName
        };
    }


    deserializeFigure = (f) => {
        let figure
        if (f.type === Group.groupType) {
            figure = new Group(f.name, f.id, f.w, f.h, f.description).figure
        } else {
            figure = Node.create(f.type, f.id, f.w, f.h, f.name, f.description, f.color).figure
        }

        figure.x = f.x
        figure.y = f.y
        return figure
    }
}
export const figures = new Figures()


const createInnerNode = (figure, store) => {
    const name = figure.userData.getName()
    const canvasData = store.read(figure.getId())

    const bgColor = Colors.canvasBackground
    const innerDiagramNode = new InnerDiagram({
        width: figure.width - 4,
        height: figure.height - 4,
        keepAspectRatio: true,
        color: 'none',
        bgColor: bgColor,
        radius: 5,
        userData: { type: 'svg', color: "BlueGrey" },
    },
        canvasData, name, () => hideInnerDiagram(figure))
    return innerDiagramNode
}





// export const setNodeColor = (figure, colorName) => {
//     figure.setBackgroundColor(Colors.getNodeColor(colorName))
//     const children = figure.getChildren().asArray()
//     const nameLabel = getNameLabel(figure)
//     const descriptionLabel = getDescriptionLabel(figure);
//     const icon = children.find(c => c instanceof draw2d.shape.icon.Icon);
//     const diagramIcon = getDiagramIcon(figure);

//     nameLabel?.setFontColor(Colors.getNodeFontColor(colorName))
//     descriptionLabel?.setFontColor(Colors.getNodeFontColor(colorName))
//     //icon?.setBackgroundColor(getNodeColor(colorName))
//     icon?.setColor(Colors.getNodeFontColor(colorName))
//     // diagramIcon?.setBackgroundColor(getNodeColor(colorName))
//     diagramIcon?.setColor(Colors.getNodeFontColor(colorName))

//     figure.setUserData({ ...figure.getUserData(), color: colorName })
// }

const getNameLabel = (figure) => {
    return figure.getChildren().asArray()
        .find(c => c.userData?.type === 'name')
}

const getDescriptionLabel = (figure) => {
    return figure.getChildren().asArray()
        .find(c => c.userData?.type === 'description')
}

const getDiagramIcon = (figure) => {
    return figure.getChildren().asArray()
        .find(c => c instanceof draw2d.shape.icon.Diagram)
}



export const showInnerDiagram = (figure, store) => {
    const t = timing()

    getNameLabel(figure)?.setVisible(false)
    getDescriptionLabel(figure)?.setVisible(false)
    getDiagramIcon(figure)?.setVisible(false)

    const innerDiagramNode = createInnerNode(figure, store)
    innerDiagramNode.onClick = onClickHandler(
        () => hideInnerDiagram(figure),
        () => editInnerDiagram(figure))
    figure.innerDiagram = innerDiagramNode
    figure.add(innerDiagramNode, new InnerDiagramLocator())
    figure.repaint()
    t.log('added')
}



const hideInnerDiagram = (figure) => {
    const t = timing()

    getNameLabel(figure)?.setVisible(true)
    getDescriptionLabel(figure)?.setVisible(true)
    getDiagramIcon(figure)?.setVisible(true)

    figure.remove(figure.innerDiagram)
    figure.innerDiagram = null
    t.log()
}

const editInnerDiagram = (figure) => {
    PubSub.publish('canvas.EditInnerDiagram', figure)
}

export const getCanvasFiguresRect = (canvas) => {
    let minX = 10000
    let minY = 10000
    let maxX = 0
    let maxY = 0

    canvas.getFigures().each((i, f) => {
        let fx = f.getAbsoluteX()
        let fy = f.getAbsoluteY()
        let fx2 = fx + f.getWidth()
        let fy2 = fy + f.getHeight()

        if (i === 0) {
            minX = fx
            minY = fy
            maxX = fx2
            maxY = fy2
            return
        }

        if (fx < minX) {
            minX = fx
        }
        if (fy < minY) {
            minY = fy
        }
        if (fx2 > maxX) {
            maxX = fx2
        }
        if (fy2 > maxY) {
            maxY = fy2
        }
    })

    return { x: minX, y: minY, w: maxX - minX, h: maxY - minY, x2: maxX, y2: maxY }
}





const InnerDiagramLocator = draw2d.layout.locator.Locator.extend({
    init: function () {
        this._super();
    },
    relocate: function (index, target) {
        // let parentBoundingBox = target.getParent().getBoundingBox()
        target.setPosition(2, 2)
    }
});