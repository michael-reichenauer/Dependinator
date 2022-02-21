import React from "react";
import Button from "@material-ui/core/Button";
import Dialog from "@material-ui/core/Dialog";
import TextField from "@material-ui/core/TextField";
import DialogActions from "@material-ui/core/DialogActions";
import DialogContent from "@material-ui/core/DialogContent";
import DialogContentText from "@material-ui/core/DialogContentText";
import draw2d from "draw2d";
import { atom, useAtom } from "jotai";
import Connection from "./Connection";
import Colors from "./Colors";
import { makeStyles } from "@material-ui/core";
import { Figure2d } from "./draw2dTypes";

const labelAtom = atom(null);
let setLabelFunc: any = null;

const useStyles = makeStyles(() => ({
  topScrollPaper: {
    alignItems: "flex-start",
  },
  topPaperScrollBody: {
    verticalAlign: "top",
  },
}));

export class LabelEditor {
  parent: Figure2d;

  constructor(parent: Figure2d) {
    this.parent = parent;
  }

  start() {
    const canvas = this.parent.getCanvas();
    const cmdStack = canvas.getCommandStack();
    const nameLabel = this.parent.nameLabel;
    const descriptionLabel = this.parent.descriptionLabel;

    showEditLabelDialog(
      nameLabel.getText(),
      descriptionLabel.getText(),
      (name: string, description: string) => {
        let nameBackground = nameLabel.background;
        let descriptionBackground = descriptionLabel.background;
        if (this.parent instanceof Connection) {
          nameBackground = !name ? "none" : Colors.canvasBackground;
          descriptionBackground = !description
            ? "none"
            : Colors.canvasBackground;
        }
        const nameCmd = new draw2d.command.CommandAttr(nameLabel, {
          text: name,
          bgColor: nameBackground,
        });
        const descriptionCmd = new draw2d.command.CommandAttr(
          descriptionLabel,
          { text: description, bgColor: descriptionBackground }
        );

        cmdStack.startTransaction("Edit Label");
        cmdStack.execute(nameCmd);
        cmdStack.execute(descriptionCmd);
        cmdStack.commitTransaction();
      }
    );
  }
}

export const showEditLabelDialog = (
  name: string,
  description: string,
  onOk: any
) => setLabelFunc?.({ name: name, description: description, onOk: onOk });

const useLabel = () => {
  const [label, setLabel] = useAtom(labelAtom);
  if (setLabelFunc == null) {
    setLabelFunc = setLabel;
  }

  return [label, setLabel];
};

export default function LabelEditorDialog() {
  const classes = useStyles();
  const [label, setPrompt] = useLabel();

  const handleCancel = () => {
    setPrompt?.(null);
  };

  const handleOK = () => {
    setPrompt?.(null);
    // @ts-ignore
    label?.onOk?.(label.name, label.description);
  };

  const handleNameFieldChange = (e: any) => {
    // @ts-ignore
    setPrompt?.({ ...label, name: e.target.value });
  };

  const handleDescriptionFieldChange = (e: any) => {
    // @ts-ignore
    setPrompt?.({ ...label, description: e.target.value });
  };

  return (
    <Dialog
      open={!!label}
      onClose={handleCancel}
      classes={{
        scrollPaper: classes.topScrollPaper,
        paperScrollBody: classes.topPaperScrollBody,
      }}
    >
      <DialogContent style={{ minWidth: 200 }}>
        <DialogContentText>Name and Description</DialogContentText>

        <TextField
          autoFocus
          id="name"
          label="Name"
          fullWidth
          variant="standard"
          size="small"
          multiline
          rows={2}
          defaultValue={label?.name ?? ""}
          onChange={handleNameFieldChange}
          //onKeyPress={catchReturn}
        />
        <TextField
          id="description"
          label="Description"
          fullWidth
          variant="standard"
          size="small"
          multiline
          rows={3}
          // @ts-ignore
          defaultValue={label?.description ?? ""}
          onChange={handleDescriptionFieldChange}
        />
      </DialogContent>

      <DialogActions>
        <Button
          onClick={handleOK}
          color="primary"
          variant="contained"
          style={{ margin: 5, width: 85 }}
        >
          OK
        </Button>
        <Button
          onClick={handleCancel}
          color="primary"
          variant="contained"
          style={{ margin: 5, width: 85 }}
        >
          Cancel
        </Button>
      </DialogActions>
    </Dialog>
  );
}
