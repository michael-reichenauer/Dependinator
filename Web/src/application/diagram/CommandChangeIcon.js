import draw2d from "draw2d";
import Node from "./Node";


export default class CommandChangeIcon extends draw2d.command.Command {
    NAME = "CommandChangeIcon"

    constructor(figure, iconName) {
        super("change icon")
        this.figure = this.getFigure(figure)
        this.oldIconName = figure?.iconName ?? ""
        this.iconName = iconName
    }

    canExecute() {
        return this.figure != null && this.oldIConName !== this.iconName
    }

    execute() {
        this.redo()
    }

    undo() {
        this.figure.setIcon(this.oldIconName)
    }

    redo() {
        this.figure.setIcon(this.iconName)
    }

    getFigure(figure) {
        if (figure == null) {
            return null
        }
        if (figure instanceof Node) {
            return figure
        }
        return figure.getParent()
    }
}