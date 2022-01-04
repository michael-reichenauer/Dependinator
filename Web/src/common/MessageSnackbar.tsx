import React, { useEffect } from "react";
import { SnackbarProvider, useSnackbar } from "notistack";

let setErrorFunc: (msg: string) => void | null;
let setInfoFunc: (msg: string) => void | null;
let setSuccessFunc: (msg: string) => void | null;

export const setErrorMessage = (message: string) => setErrorFunc?.(message);
export const setInfoMessage = (message: string) => setInfoFunc?.(message);
export const setSuccessMessage = (message: string) => setSuccessFunc?.(message);

// @ts-ignore
export const MessageProvider = (props) => {
  return (
    <SnackbarProvider
      maxSnack={3}
      preventDuplicate={true}
      anchorOrigin={{
        vertical: "top",
        horizontal: "center",
      }}
    >
      <Enable />
      {props.children}
    </SnackbarProvider>
  );
};

const Enable = () => {
  const { enqueueSnackbar, closeSnackbar } = useSnackbar();

  useEffect(() => {
    // Initialize canvas
    setErrorFunc = (errorMsg: string) => {
      const sb = enqueueSnackbar(errorMsg, {
        variant: "error",
        onClick: () => closeSnackbar(sb),
      });
    };
    setInfoFunc = (msg: string) => {
      const sb = enqueueSnackbar(msg, {
        variant: "info",
        onClick: () => closeSnackbar(sb),
      });
    };
    setSuccessFunc = (msg) => {
      const sb = enqueueSnackbar(msg, {
        variant: "success",
        onClick: () => {
          console.log("close success");
          closeSnackbar(sb);
        },
        autoHideDuration: 3000,
      });
    };
  }, [closeSnackbar, enqueueSnackbar]);

  return null;
};
