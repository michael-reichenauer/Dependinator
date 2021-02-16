import React from "react";
import { useWindowSize } from "./common/windowsize"
import ApplicationBar from './components/ApplicationBar'
import Diagram from "./components/diagram/Diagram";


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
