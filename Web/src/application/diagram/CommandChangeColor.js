import draw2d from "draw2d";
import Node from "./Node";
import NodeGroup from "./NodeGroup";

export default class CommandChangeColor extends draw2d.command.Command {
    NAME = "CommandChangeColor"

    constructor(figure, colorName) {
        super("change color")
        this.figure = this.getFigure(figure)
        this.oldColorName = figure?.colorName ?? ""
        this.colorName = colorName
      
    }


    canExecute() {
        // return false if we doesn't modify the model => NOP Command
        return this.figure != null && this.oldColorName !== this.colorName
    }


    execute() {
        this.redo()
    }


    undo() {
        this.figure.setNodeColor(this.oldColorName)
    }


    redo() {
        this.figure.setNodeColor(this.colorName)
    }

    getFigure(figure) {
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
