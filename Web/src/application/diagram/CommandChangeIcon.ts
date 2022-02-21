import draw2d from "draw2d";
import { Figure2d } from "./draw2dTypes";
import Node from "./Node";
import NodeGroup from './NodeGroup';


export default class CommandChangeIcon extends draw2d.command.Command {
    NAME = "CommandChangeIcon"

    figure:Figure2d
    iconName:string
    oldIconName:string

    constructor(figure:Figure2d, iconName:string) {
        super("change icon")
        this.figure = this.getFigure(figure)
        this.oldIconName = figure?.iconName ?? ""
        this.iconName = iconName
    }

    canExecute(): boolean {
        return this.figure != null && this.oldIConName !== this.iconName
    }

    execute(): void {
        this.redo()
    }

    undo(): void {
        this.figure.setIcon(this.oldIconName)
    }

    redo():void {
        this.figure.setIcon(this.iconName)
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