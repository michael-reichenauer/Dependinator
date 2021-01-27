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

        //let rsp = new PanningPolicy()
        let selection = null
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


        // canvas.add(new draw2d.shape.basic.Oval(), 100, 100);
        // canvas.add(new draw2d.shape.basic.Rectangle(), 120, 150);

        let f1 = new draw2d.shape.node.Start();
        f1.setWidth(100);
        f1.setHeight(100);
        canvas.add(f1, 100, 100);
        // canvas.add(new draw2d.shape.node.Start(), 50, 50);

        let f2 = new draw2d.shape.node.End(100, 100);
        f2.setWidth(100);
        f2.setHeight(100);
        canvas.add(f2, 300, 300);
        // canvas.add(new draw2d.shape.node.End(), 350, 250);

        let nwp = new WheelZoomPolicy()

        canvas.installEditPolicy(rsp)
        canvas.installEditPolicy(nwp);


        canvas.getCommandStack().addEventListener(function (e) {
            if (e.isPostChangeEvent()) {
                console.log('event:', e)
            }
        });
    }

    render() {
        return (
            <div style={{ margin: '80px', background: 'red' }}>
                <div id="canvas" style={{ height: 1000, width: 1000, position: 'absolute', overflow: 'scroll' }}></div>
            </div>
        );
    }
}

export default Canvas;