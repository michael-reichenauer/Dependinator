import React from "react";
import { useWindowSize } from "./common/windowSize"
import ApplicationBar from './application/ApplicationBar'
import Diagram from "./diagram/Diagram";


function App() {
  const [size] = useWindowSize()

  return (
    <>
      <ApplicationBar height={55} />
      <Diagram width={size.width} height={size.height - 55} />
    </>
  );
}

export default App;
