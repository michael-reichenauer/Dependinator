import draw2d from "draw2d";
import Colors from "./colors";
import PubSub from 'pubsub-js'
import { InnerDiagram } from "./InnerDiagram";
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
            type: f.type, id: f.id, x: f.x, y: f.y, w: f.width, h: f.height,
            name: f.getName(), description: f.getDescription(), color: f.colorName
        };
    }


    deserializeFigure = (f) => {
        let figure
        if (f.type === Group.groupType) {
            figure = new Group(f.name, { id: f.id, width: f.w, height: f.h, description: f.description })
        } else {
            figure = new Node(f.type, { id: f.id, width: f.w, height: f.h, name: f.name, description: f.description, colorName: f.color })
        }

        figure.x = f.x
        figure.y = f.y
        return figure
    }
}
export const figures = new Figures()


const createInnerNode = (figure, store) => {
    const name = figure.getName()
    const canvasData = store.read(figure.getId())

    const bgColor = Colors.canvasBackground
    const innerDiagramNode = new InnerDiagram(name, canvasData,
        {
            width: figure.width - 4,
            height: figure.height - 4,
            keepAspectRatio: true,
            color: 'none',
            bgColor: bgColor,
            radius: 5,
            userData: { type: 'svg', color: "BlueGrey" },
        })
    return innerDiagramNode
}



// const getNameLabel = (figure) => {
//     return figure.getChildren().asArray()
//         .find(c => c.userData?.type === 'name')
// }

// const getDescriptionLabel = (figure) => {
//     return figure.getChildren().asArray()
//         .find(c => c.userData?.type === 'description')
// }

// const getDiagramIcon = (figure) => {
//     return figure.getChildren().asArray()
//         .find(c => c instanceof draw2d.shape.icon.Diagram)
// }



export const showInnerDiagram = (figure, store) => {
    const t = timing()

    figure.nameLabel?.setVisible(false)
    figure.descriptionLabel?.setVisible(false)
    figure.diagramIcon?.setVisible(false)

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

    figure.nameLabel?.setVisible(true)
    figure.descriptionLabel?.setVisible(true)
    figure.diagramIcon?.setVisible(true)

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