import React from "react";

import Diagram from './components/Diagram';
import { useWindowSize } from "./common/windowsize"
import ApplicationBar from './components/ApplicationBar'

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
