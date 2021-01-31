import React, { Component } from "react";
import "import-jquery";
import "jquery-ui-bundle"; // you also need this
import "jquery-ui-bundle/jquery-ui.css";
import draw2d from "draw2d";
import { WheelZoomPolicy } from "./WheelZoomPolicy"
import { PanPolicyReadOnly } from "./PanPolicyReadOnly"


class Canvas extends Component {

    canvas = null;

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

        let rsp = new PanPolicyReadOnly()
        let psp = new draw2d.policy.canvas.PanningSelectionPolicy()

        rsp.onClick = (figure, mouseX, mouseY, shiftKey, ctrlKey) => {
            console.log('rsp click;', figure, mouseX, mouseY, shiftKey, ctrlKey)
            if (figure !== null) {
                this.canvas.uninstallEditPolicy(rsp)
                //psp = new draw2d.policy.canvas.PanningSelectionPolicy()
                canvas.installEditPolicy(psp)
                psp.select(canvas, figure)
            }
        }

        psp.onClick = (figure, mouseX, mouseY, shiftKey, ctrlKey) => {
            console.log('psp click;', figure, mouseX, mouseY, shiftKey, ctrlKey)
            if (figure === null) {
                canvas.uninstallEditPolicy(psp)
                canvas.installEditPolicy(rsp)
            }
        }


        let nwp = new WheelZoomPolicy()

        canvas.installEditPolicy(rsp)
        canvas.installEditPolicy(nwp);
        // canvas.setDimension(new draw2d.geo.Rectangle(0, 0, 50, 50))

        canvas.getCommandStack().addEventListener(function (e) {
            if (e.isPostChangeEvent()) {
                console.log('event:', e)
            }
        });

        //canvas.setDimension(new draw2d.geo.Rectangle(0, 0, 10000, 10000))


        let cf = new draw2d.shape.composite.Jailhouse({ width: 200, height: 200, bgColor: 'none' })
        canvas.add(cf, 400, 400);

        let f = new draw2d.shape.node.Between({ width: 50, height: 50 });
        f.getPorts().each((i, port) => { port.setVisible(false) })
        canvas.add(f, 450, 450);
        cf.assignFigure(f)

        console.time('figures')
        for (var i = 0; i < 10; i++) {
            setTimeout(() => {
                for (var i = 0; i < 100; i++) {
                    let f = new draw2d.shape.node.Between();
                    f.setWidth(random(10, 50));
                    f.setHeight(random(10, 50));
                    f.getPorts().each((i, port) => { port.setVisible(false) })
                    canvas.add(f, random(0, 8000), random(0, 8000));
                }
            }, 0)
        }

        setTimeout(() => {
            canvas.setDimension()
            console.timeEnd('figures')
        }, 0);

    }

    render() {
        console.log('render')
        let w = this.props.width
        let h = this.props.height

        return (
            <div id="canvas"
                style={{ width: w, height: h, position: 'absolute', overflow: 'scroll', maxWidth: w, maxHeight: h }}></div>
        );
    }
}

function random(min, max) {
    min = Math.ceil(min);
    max = Math.floor(max);
    return Math.floor(Math.random() * (max - min) + min); //The maximum is exclusive and the minimum is inclusive
}


export default Canvas;