import draw2d from "draw2d";
import { Figure2d } from "./draw2dTypes";
import Node from "./Node";
import NodeGroup from "./NodeGroup";

export default class CommandChangeColor extends draw2d.command.Command {
    NAME:string = "CommandChangeColor"

    figure:Figure2d
    colorName:string
    oldColorName:string

    constructor(figure:Figure2d, colorName:string) {
        super("change color")
        this.figure = this.getFigure(figure)
        this.oldColorName = figure?.colorName ?? ""
        this.colorName = colorName
      
    }


    canExecute():boolean {
        // return false if we doesn't modify the model => NOP Command
        return this.figure != null && this.oldColorName !== this.colorName
    }


    execute():void {
        this.redo()
    }


    undo():void {
        this.figure.setNodeColor(this.oldColorName)
    }


    redo():void {
        this.figure.setNodeColor(this.colorName)
    }

    getFigure(figure:Figure2d):Figure2d {
        if (figure == null) {
            return null
        }
        if (figure instanceof Node) {
            return figure
        }
        if (figure instanceof NodeGroup) {
            return figure
        }
        return figure.getParent()
    }
}
