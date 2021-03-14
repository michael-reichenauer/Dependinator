import draw2d from "draw2d";

export const CommandChangeColor = draw2d.command.Command.extend(
    {
        NAME: "CommandChangeColor",

        init: function (figure, colorName) {
            this._super("change color")
            this.figure = figure
            this.oldColorName = figure.userData?.colorName ?? ""
            this.colorName = colorName
        },


        canExecute: function () {
            // return false if we doesn't modify the model => NOP Command
            return this.oldColorName !== this.colorName
        },


        execute: function () {
            this.redo()
        },


        undo: function () {
            this.figure.userData.setColor(this.oldColorName)
        },


        redo: function () {
            this.figure.userData.setColor(this.colorName)
        },
    })
