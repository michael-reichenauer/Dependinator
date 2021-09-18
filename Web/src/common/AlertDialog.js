import React from 'react';
import Button from '@material-ui/core/Button';
import Dialog from '@material-ui/core/Dialog';
import DialogActions from '@material-ui/core/DialogActions';
import DialogContent from '@material-ui/core/DialogContent';
import DialogContentText from '@material-ui/core/DialogContentText';
import DialogTitle from '@material-ui/core/DialogTitle';
import { atom, useAtom } from 'jotai';

const alertAtom = atom(null)
let setAlertFunc = null


// Show a confirm (ok/cancel) alert
export const showConfirmAlert = (title, message, onOk, onCancel) => setAlertFunc?.({ title: title, message: message, onOk: onOk, onCancel: onCancel, confirm: true })


// Shows a OK alert
export const showOKAlert = (title, message, onOk) => setAlertFunc?.({ title: title, message: message, onOk: onOk, confirm: false })


// Use alert for OK/cancel or just OK
export const useAlert = () => {
  const [alert, setAlert] = useAtom(alertAtom)
  if (setAlertFunc == null) {
    setAlertFunc = setAlert
  }

  return [alert, setAlert]
}


export default function AlertDialog() {
  const [alert, setAlert] = useAlert();

  const handleCancel = () => {
    setAlert(null);
    alert?.onCancel?.()
  };

  const handleOK = () => {
    setAlert(null);
    alert?.onOk?.()
  };


  return (
    <Dialog open={!!alert} onClose={() => { }}   >
      <DialogTitle>{alert?.title}</DialogTitle>
      <DialogContent style={{ minWidth: 300 }}>
        <DialogContentText >
          {alert?.message}
        </DialogContentText>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleOK} color="primary" autoFocus variant="contained" style={{ margin: 5, width: 85 }}>
          OK
        </Button>
        {alert?.confirm &&
          <Button onClick={handleCancel} color="primary" variant="contained" style={{ margin: 5, width: 85 }}>
            Cancel
          </Button>
        }
      </DialogActions>
    </Dialog>
  );
}