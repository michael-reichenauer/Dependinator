import React from "react";

import Canvas from './components/diagram/Canvas';
import { useWindowSize } from "./common/windowsize"
import ApplicationBar from './components/ApplicationBar'



function App() {
  const [size] = useWindowSize()

  return (
    <>
      <ApplicationBar height={55} />
      <Canvas width={size.width} height={size.height - 55} />
    </>
  );
}

export default App;
