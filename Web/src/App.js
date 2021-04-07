import React from "react";
import { useWindowSize } from "./common/windowSizeX"
import ApplicationBar from './application/ApplicationBar'
import Diagram from "./application/Diagram";
import { useActivityMonitor } from "./common/activity";
import About from "./application/About";
import Login from "./application/Login";


function App() {
  const [size] = useWindowSize()

  useActivityMonitor()

  return (
    <>
      <ApplicationBar height={55} />
      <Diagram width={size.width} height={size.height - 55} />
      <About />
      <Login />
    </>
  );
}

export default App;
