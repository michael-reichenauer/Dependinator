import React, { useCallback, useRef } from "react";
import {
  Backdrop,
  CircularProgress,
  Fade,
  makeStyles,
} from "@material-ui/core";
import { atom, useAtom } from "jotai";

const progressAtom = atom(false);
let setProgressFunc: any = null;

export const setProgress = (flag: boolean) => setProgressFunc?.(flag);

export default function Progress() {
  const classes = useStyles();
  const [isProgress] = useProgress();

  return (
    <Fade
      in={isProgress as boolean}
      style={{
        transitionDelay: isProgress ? "800ms" : "0ms",
      }}
      unmountOnExit
    >
      <Backdrop className={classes.backdrop} open={isProgress as boolean}>
        <CircularProgress className={classes.colorPrimary} color="primary" />
      </Backdrop>
    </Fade>
  );
}

const useProgress = () => {
  const [isProgress, setProgress] = useAtom(progressAtom);
  const count = useRef(0);

  if (!setProgressFunc) {
    setProgressFunc = setProgress;
  }

  const set = useCallback(
    (isStart) => {
      if (isStart) {
        count.current = count.current + 1;
        if (count.current === 1) {
          setProgress(true);
        }
      } else {
        if (count.current > 0) {
          count.current = count.current - 1;
          if (count.current === 0) {
            setProgress(false);
          }
        }
      }
    },
    [count, setProgress]
  );

  return [isProgress, set];
};

const useStyles = makeStyles((theme) => ({
  backdrop: {
    zIndex: theme.zIndex.drawer + 1,
    color: "#fff",
  },
  colorPrimary: {
    color: "white",
  },
}));
