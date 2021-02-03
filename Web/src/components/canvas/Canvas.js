import React, { Component } from "react";
import "import-jquery";
import "jquery-ui-bundle"; // you also need this
import "jquery-ui-bundle/jquery-ui.css";
import draw2d from "draw2d";
import { WheelZoomPolicy } from "./WheelZoomPolicy"
import { PanPolicyReadOnly } from "./PanPolicyReadOnly"
import { PanPolicyEdit } from "./PanPolicyEdit"
import { ConnectionCreatePolicy } from "./ConnectionCreatePolicy"

class Canvas extends Component {

    canvas = null;

    canvasWidth = 0;
    canvasHeigh = 0;
    hasRendered = false;

    componentDidMount() {
        console.log('componentDidMount')
        this.renderCanvas();
    }

    componentWillUnmount() {
        console.log('componentWillUnmount')
        this.canvas.destroy()
    }

    renderCanvas() {

        this.canvas = new draw2d.Canvas("canvas");
        let canvas = this.canvas
        canvas.setScrollArea("#canvas");
        //canvas.installEditPolicy(new draw2d.policy.canvas.ShowGridEditPolicy())

        let createItem = (x, y, shiftKey, ctrlKey) => {
            console.log('onNoPortDrop', x, y, shiftKey, ctrlKey)

            let f = new draw2d.shape.node.Between({ width: 50, height: 50 });
            canvas.add(f, x - 25, y - 25);
            return f
        }

        let rsp = new PanPolicyReadOnly()
        let psp = new PanPolicyEdit()

        rsp.onClick = (figure, mouseX, mouseY, shiftKey, ctrlKey) => {
            console.log('rsp click;', figure, mouseX, mouseY, shiftKey, ctrlKey)
            if (ctrlKey) {
                let f = createItem(mouseX, mouseY, shiftKey, ctrlKey)
                this.canvas.uninstallEditPolicy(rsp)
                canvas.installEditPolicy(psp)
                psp.select(canvas, f)
                return
            }

            if (figure !== null) {
                this.canvas.uninstallEditPolicy(rsp)
                canvas.installEditPolicy(psp)
                psp.select(canvas, figure)
            }
        }

        psp.onClick = (figure, mouseX, mouseY, shiftKey, ctrlKey) => {
            console.log('psp click;', figure, mouseX, mouseY, shiftKey, ctrlKey)
            if (ctrlKey) {
                let f = createItem(mouseX, mouseY, shiftKey, ctrlKey)
                psp.select(canvas, f)
                return
            }
            if (figure === null) {
                canvas.uninstallEditPolicy(psp)
                canvas.installEditPolicy(rsp)
            }
        }



        let ccp = new ConnectionCreatePolicy()
        canvas.installEditPolicy(rsp)
        canvas.installEditPolicy(new WheelZoomPolicy());
        canvas.installEditPolicy(ccp)

        canvas.getCommandStack().addEventListener(function (e) {
            if (e.isPostChangeEvent()) {
                console.log('event:', e)
            }
        });

        let cf = new draw2d.shape.composite.Jailhouse({ width: 200, height: 200, bgColor: 'none' })
        canvas.add(cf, 400, 400);

        let f = new draw2d.shape.node.Between({ width: 50, height: 50 });
        canvas.add(f, 450, 450);
        f.getPorts().each((i, port) => { port.setVisible(false) })
        cf.assignFigure(f)

        let f2 = new draw2d.shape.node.Between({ width: 50, height: 50 });
        canvas.add(f2, 200, 200);
        f2.getPorts().each((i, port) => { port.setVisible(false) })

        let f3 = new draw2d.shape.node.Between({ width: 50, height: 50 });
        canvas.add(f3, 100, 100);
        f3.getPorts().each((i, port) => { port.setVisible(false) })

        var c = new draw2d.Connection({
            source: f3.getOutputPort(0),
            target: f2.getInputPort(0)
        });
        canvas.add(c);


        //createManyItems(canvas)
    }

    render() {
        console.log('render')
        let w = this.props.width
        let h = this.props.height
        console.log('size:', w, h)
        if (this.hasRendered && (this.canvasWidth !== w || this.canvasHeigh !== h)) {
            setTimeout(() => {
                this.canvas.setDimension(new draw2d.geo.Rectangle(0, 0, w, h));
            }, 0);
        }
        this.hasRendered = true
        this.canvasWidth = w;
        this.canvasHeigh = h;


        return (
            <div id="canvas"
                style={{ width: w, height: h, position: 'absolute', overflow: 'scroll', maxWidth: w, maxHeight: h }}></div>
        );
    }
}

// function createManyItems(canvas) {
//     console.time('figures')
//     for (var i = 0; i < 10; i++) {
//         setTimeout(() => {
//             for (var i = 0; i < 100; i++) {
//                 let f = new draw2d.shape.node.Between();
//                 f.setWidth(random(10, 50));
//                 f.setHeight(random(10, 50));

//                 canvas.add(f, random(0, 8000), random(0, 8000));
//                 f.getPorts().each((i, port) => { port.setVisible(false) })
//             }
//         }, 0)
//     }

//     setTimeout(() => {
//         canvas.setDimension()
//         console.timeEnd('figures')
//     }, 0);
// }

// function random(min, max) {
//     min = Math.ceil(min);
//     max = Math.floor(max);
//     return Math.floor(Math.random() * (max - min) + min); //The maximum is exclusive and the minimum is inclusive
// }


export default Canvas;