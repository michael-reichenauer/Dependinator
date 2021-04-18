import React from "react";
import { useWindowSize } from "./common/windowSizeX"
import ApplicationBar from './application/ApplicationBar'
import Diagram from "./application/Diagram";
import { useActivityMonitor } from "./common/activity";
import About from "./application/About";
import Login from "./application/Login";
import { useAppVersionMonitor } from "./common/appVersion";
import Credential from "./application/Credential";
import AlertDialog from "./common/AlertDialog";


function App() {
  const [size] = useWindowSize()

  useActivityMonitor()
  useAppVersionMonitor()

  return (
    <>
      <ApplicationBar height={55} />
      <Diagram width={size.width} height={size.height - 55} />
      <About />
      <Login />
      <Credential />
      <AlertDialog />
    </>
  );
}

export default App;
