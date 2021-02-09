import React, { useRef } from "react";

import Canvas from './components/diagram/Canvas';
import { useWindowSize } from "./common/windowsize"
import ApplicationBar from './components/ApplicationBar'



function App() {
  const [size] = useWindowSize()
  const commands = useRef({})

  return (
    <>
      <ApplicationBar height={55} commands={commands.current} />
      <Canvas width={size.width} height={size.height - 55} commands={commands.current} />
    </>
  );
}

export default App;
