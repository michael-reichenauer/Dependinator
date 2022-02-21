import React, { useEffect } from "react";
import { SnackbarKey, SnackbarProvider, useSnackbar } from "notistack";

let setErrorFunc: (msg: string) => void | null;
let setInfoFunc: (msg: string) => void | null;
let setSuccessFunc: (msg: string) => void | null;

export const setErrorMessage = (message: string) => setErrorFunc?.(message);
export const setInfoMessage = (message: string) => setInfoFunc?.(message);
export const setSuccessMessage = (message: string) => setSuccessFunc?.(message);
export const clearErrorMessages = () =>
  errorSnackBars.forEach((sb) => closeSnackbarFn(sb));

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

const errorSnackBars: SnackbarKey[] = [];
let closeSnackbarFn = (k: SnackbarKey) => {};

const Enable = () => {
  const { enqueueSnackbar, closeSnackbar } = useSnackbar();
  closeSnackbarFn = closeSnackbar;

  useEffect(() => {
    // Initialize canvas
    setErrorFunc = (errorMsg: string) => {
      const sb = enqueueSnackbar(errorMsg, {
        variant: "error",
        onClick: () => {
          closeSnackbar(sb);
          removeSnackbar(errorSnackBars, sb);
        },
        persist: true,
      });
      errorSnackBars.push(sb);
    };
    setInfoFunc = (msg: string) => {
      const sb = enqueueSnackbar(msg, {
        variant: "info",
        onClick: () => closeSnackbar(sb),
      });
    };
    setSuccessFunc = (msg) => {
      clearErrorMessages();
      const sb = enqueueSnackbar(msg, {
        variant: "success",
        onClick: () => closeSnackbar(sb),
        autoHideDuration: 3000,
      });
    };
  }, [closeSnackbar, enqueueSnackbar]);

  return null;
};

function removeSnackbar(arr: SnackbarKey[], value: SnackbarKey) {
  var index = arr.indexOf(value);
  if (index > -1) {
    arr.splice(index, 1);
  }
  return arr;
}
