import React from 'react';
import Button from '@material-ui/core/Button';
import Dialog from '@material-ui/core/Dialog';
import TextField from '@material-ui/core/TextField';
import DialogActions from '@material-ui/core/DialogActions';
import DialogContent from '@material-ui/core/DialogContent';
import DialogContentText from '@material-ui/core/DialogContentText';
import draw2d from "draw2d";
import { atom, useAtom } from 'jotai';
import Connection from './Connection';
import Colors from './Colors';


const labelAtom = atom(null)
let setLabelFunc = null

export class LabelEditor {
    constructor(parent) {
        this.parent = parent
    }

    start() {
        const canvas = this.parent.getCanvas()
        const cmdStack = canvas.getCommandStack()
        const nameLabel = this.parent.nameLabel
        const descriptionLabel = this.parent.descriptionLabel

        showEditLabelDialog(nameLabel.getText(), descriptionLabel.getText(),
            (name, description) => {
                let nameBackground = nameLabel.background
                let descriptionBackground = descriptionLabel.background
                if (this.parent instanceof Connection) {
                    nameBackground = !name ? 'none' : Colors.canvasBackground
                    descriptionBackground = !description ? 'none' : Colors.canvasBackground
                }
                const nameCmd = new draw2d.command.CommandAttr(nameLabel, { text: name, bgColor: nameBackground })
                const descriptionCmd = new draw2d.command.CommandAttr(descriptionLabel, { text: description, bgColor: descriptionBackground })

                cmdStack.startTransaction('Edit Label')
                cmdStack.execute(nameCmd)
                cmdStack.execute(descriptionCmd)
                cmdStack.commitTransaction()
            })
    }
}


export const showEditLabelDialog = (name, description, onOk) => setLabelFunc?.(
    { name: name, description: description, onOk: onOk })


const useLabel = () => {
    const [label, setLabel] = useAtom(labelAtom)
    if (setLabelFunc == null) {
        setLabelFunc = setLabel
    }

    return [label, setLabel]
}


export default function LabelEditorDialog() {
    const [label, setPrompt] = useLabel();

    const handleCancel = () => {
        setPrompt(null);
    };

    const handleOK = () => {
        setPrompt(null);
        label?.onOk?.(label.name, label.description)
    };

    const handleNameFieldChange = (e) => {
        setPrompt({ ...label, name: e.target.value });
    }

    const handleDescriptionFieldChange = (e) => {
        setPrompt({ ...label, description: e.target.value });
    }

    // const catchReturn = (ev) => {
    //     if (ev.key === 'Enter') {
    //         handleOK()
    //         ev.preventDefault();
    //     }
    // }


    return (
        <Dialog open={!!label} onClose={handleCancel}   >
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
                    defaultValue={label?.name ?? ''}
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
                    defaultValue={label?.description ?? ''}
                    onChange={handleDescriptionFieldChange}
                />
            </DialogContent>

            <DialogActions>
                <Button onClick={handleOK} color="primary" variant="contained" style={{ margin: 5, width: 85 }}>
                    OK
                </Button>
                <Button onClick={handleCancel} color="primary" variant="contained" style={{ margin: 5, width: 85 }}>
                    Cancel
                </Button>
            </DialogActions>
        </Dialog>
    );
}