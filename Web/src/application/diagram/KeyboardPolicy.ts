import draw2d from "draw2d";
import PubSub from "pubsub-js";
import Canvas from "./Canvas";

export default class KeyboardPolicy extends draw2d.policy.canvas
  .KeyboardPolicy {
  onKeyDown(
    canvas: Canvas,
    keyCode: number,
    shiftKey: boolean,
    ctrlKey: boolean
  ): void {
    // console.log('Key', keyCode, shiftKey, ctrlKey)

    const handleKey = (keys: any) => {
      // Get key definition for keyCode and predicate (ctrl, ...)
      const keyDef = keys.find(
        (key: any) => keyCode === key[0].charCodeAt(0) && key[1]
      );
      if (keyDef != null) {
        const keyAction = keyDef[2];
        keyAction();
        return;
      }

      super.onKeyDown(canvas, keyCode, shiftKey, ctrlKey);
    };

    const isSelected = canvas.getPrimarySelection() !== null;

    handleKey([
      ["Z", ctrlKey, () => PubSub.publish("canvas.Undo")],
      ["Y", ctrlKey, () => PubSub.publish("canvas.Redo")],
      ["B", ctrlKey && isSelected, () => canvas.getPrimarySelection().toBack()],
      [
        "F",
        ctrlKey && isSelected,
        () => canvas.getPrimarySelection().toFront(),
      ],
    ]);
  }
}

// Grouping:
// const keyG = 71
//     if (canvas.getPrimarySelection() instanceof draw2d.shape.composite.Group && canvas.getSelection().getSize() === 1) {
//         canvas.getCommandStack().execute(new draw2d.command.CommandUngroup(canvas, canvas.getPrimarySelection()))
//     }
//     else {
//         canvas.getCommandStack().execute(new draw2d.command.CommandGroup(canvas, canvas.getSelection()))
//     }
