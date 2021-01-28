import React, { Component } from "react";
import "import-jquery";
import "jquery-ui-bundle"; // you also need this
import "jquery-ui-bundle/jquery-ui.css";
import draw2d from "draw2d";
import { WheelZoomPolicy } from "./WheelZoomPolicy"


class Canvas extends Component {

    componentDidMount() {
        this.renderCanvas();
    }

    renderCanvas() {
        var canvas = new draw2d.Canvas("canvas");
        canvas.setScrollArea("#canvas");
        canvas.installEditPolicy(new draw2d.policy.canvas.ShowGridEditPolicy())
        let rsp = new draw2d.policy.canvas.ReadOnlySelectionPolicy()
        let psp = new draw2d.policy.canvas.PanningSelectionPolicy()

        rsp.onClick = (figure, mouseX, mouseY, shiftKey, ctrlKey) => {
            console.log('rsp click;', figure, mouseX, mouseY, shiftKey, ctrlKey)
            if (figure !== null) {
                canvas.uninstallEditPolicy(rsp)
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

        // let f1 = new draw2d.shape.node.Between();
        // f1.setWidth(100);
        // f1.setHeight(100);
        // canvas.add(f1, 100, 100);

        // let f2 = new draw2d.shape.node.Between();
        // f2.setWidth(100);
        // f2.setHeight(100);
        // canvas.add(f2, 300, 300);


        let nwp = new WheelZoomPolicy()

        canvas.installEditPolicy(rsp)
        canvas.installEditPolicy(nwp);
        // canvas.setDimension(new draw2d.geo.Rectangle(0, 0, 50, 50))

        canvas.getCommandStack().addEventListener(function (e) {
            if (e.isPostChangeEvent()) {
                console.log('event:', e)
            }
        });

        let f = new draw2d.shape.node.Between();
        f.setWidth(100);
        f.setHeight(100);
        canvas.add(f, 3000, 3000);
        canvas.setDimension(new draw2d.geo.Rectangle(0, 0, 10000, 10000))

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
            console.timeEnd('figures')
        }, 0);

    }

    render() {
        return (
            <div style={{ margin: 100, background: 'red' }}>
                <div id="canvas"
                    style={{ height: 1000, width: 1000, position: 'absolute', overflow: 'scroll', maxWidth: 1000, maxHeight: 1000 }}></div>
            </div>
        );
    }
}

function random(min, max) {
    min = Math.ceil(min);
    max = Math.floor(max);
    return Math.floor(Math.random() * (max - min) + min); //The maximum is exclusive and the minimum is inclusive
}


export default Canvas;