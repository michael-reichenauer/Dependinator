import React from "react";
import ApplicationBar from './application/ApplicationBar'
import { useWindowSize } from "./common/windowSizeX"
import { useActivityMonitor } from "./common/activity";
import { useAppVersionMonitor } from "./common/appVersion";
import Diagram from "./application/Diagram";
import About from "./application/About";
import Login from "./application/Login";
import AlertDialog from "./common/AlertDialog";


function App() {
  const [size] = useWindowSize()

  // Enable user activity detection (e.g. moving mouse ) and new available web site at server detection
  useActivityMonitor()
  useAppVersionMonitor()

  return (
    <>
      <ApplicationBar height={55} />
      <Diagram width={size.width} height={size.height - 55} />
      <About />
      <Login />
      <AlertDialog />
    </>
  );
}

export default App;
