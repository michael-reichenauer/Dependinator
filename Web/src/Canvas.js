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

        var figure1 = new draw2d.shape.basic.Oval();
        var figure2 = new draw2d.shape.basic.Rectangle();
        canvas.add(figure1, 10, 100);
        canvas.add(figure2, 10, 150);
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