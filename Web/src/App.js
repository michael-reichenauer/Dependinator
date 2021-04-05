import React from "react";
import { useWindowSize } from "./common/windowSizeX"
import ApplicationBar from './application/ApplicationBar'
import Diagram from "./application/Diagram";
import { useActivityMonitor } from "./common/activity";


function App() {
  const [size] = useWindowSize()

  useActivityMonitor()

  return (
    <>
      <ApplicationBar height={55} />
      <Diagram width={size.width} height={size.height - 55} />
    </>
  );
}

export default App;
