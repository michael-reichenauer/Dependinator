import draw2d from "draw2d";
import PubSub from 'pubsub-js'


export default class KeyboardPolicy extends draw2d.policy.canvas.KeyboardPolicy {

    onKeyDown(canvas, keyCode, shiftKey, ctrlKey) {
        if (canvas.getPrimarySelection() !== null && ctrlKey === true) {
            // When node is selected
            switch (keyCode) {
                case 71: // G
                    if (canvas.getPrimarySelection() instanceof draw2d.shape.composite.Group && canvas.getSelection().getSize() === 1) {
                        canvas.getCommandStack().execute(new draw2d.command.CommandUngroup(canvas, canvas.getPrimarySelection()))
                    }
                    else {
                        canvas.getCommandStack().execute(new draw2d.command.CommandGroup(canvas, canvas.getSelection()))
                    }
                    break
                case 66: // B
                    canvas.getPrimarySelection().toBack()
                    break
                case 70: // F
                    canvas.getPrimarySelection().toFront()
                    break
                case 90: // z
                    PubSub.publish('canvas.Undo')
                    break
                case 89: // y
                    PubSub.publish('canvas.Redo')
                    break
                default:
                //console.log('Key', keyCode)
            }
        }
        else {
            // console.log('Key', keyCode)
            if (keyCode === 90 && ctrlKey) { // ctrl-z
                PubSub.publish('canvas.Undo')
            } else if (keyCode === 89 && ctrlKey) {  // ctrl-y
                PubSub.publish('canvas.Redo')
            } else {
                super.onKeyDown(canvas, keyCode, shiftKey, ctrlKey)
            }
        }
    }
}
