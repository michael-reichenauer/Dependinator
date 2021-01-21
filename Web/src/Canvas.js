import React, { Component } from "react";
import "import-jquery";
import "jquery-ui-bundle"; // you also need this
import "jquery-ui-bundle/jquery-ui.css";
import draw2d from "draw2d";

class Canvas extends Component {

    componentDidMount() {
        this.renderCanvas();
    }

    renderCanvas() {
        var canvas = new draw2d.Canvas("canvassvg");


        canvas.add(new draw2d.shape.basic.Oval(), 100, 100);
        canvas.add(new draw2d.shape.basic.Rectangle(), 120, 150);

        canvas.add(new draw2d.shape.node.Start(), 80, 80);
        canvas.add(new draw2d.shape.node.Start(), 50, 50);

        canvas.add(new draw2d.shape.node.End(), 150, 150);
        canvas.add(new draw2d.shape.node.End(), 350, 250);
    }
    render() {
        return (
            <div>
                <div id="canvassvg" style={{ height: 1500, width: 1500 }}></div>
            </div>
        );
    }
}
export default Canvas;