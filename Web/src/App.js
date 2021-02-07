import React, { useRef } from "react";

import Diagram from './components/Diagram';
import { useWindowSize } from "./common/windowsize"
import ApplicationBar from './components/ApplicationBar'



function App() {
  const [size] = useWindowSize()
  const commands = useRef({})

  return (
    <>
      <ApplicationBar height={55} commands={commands.current} />
      <Diagram width={size.width} height={size.height - 55} commands={commands.current} />
    </>
  );
}

export default App;
