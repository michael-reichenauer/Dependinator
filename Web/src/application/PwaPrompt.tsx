// @ts-nocheck
import React from "react";

const Transition = React.forwardRef(function Transition(props, ref) {
  return <Slide direction="down" ref={ref} {...props} />;
});

export const PwaPrompt = () => {
  const [showPwaPrompt, setShowPwaPrompt] = usePwaPrompt();

  const handleClose = () => {
    setShowPwaPrompt(false);
  };

  return (
    <>
      {showPwaPrompt && (
        <Dialog
          open={showPwaPrompt}
          TransitionComponent={Transition}
          keepMounted
          PaperProps={{
            style: {
              backgroundColor: "#333333",
            },
          }}
        >
          <DialogTitle>{"Hint: Install Log"}</DialogTitle>
          <DialogContent>
            <DialogContentText>
              Install this Log app on your home screen for quick and easy
              access.
            </DialogContentText>
            <DialogContentText>
              Just tap the share button and then 'Add to Home Screen'.
            </DialogContentText>
          </DialogContent>
          <DialogActions>
            <Button variant="outlined" color="primary" onClick={handleClose}>
              Close
            </Button>
          </DialogActions>
        </Dialog>
      )}
    </>
  );
};
