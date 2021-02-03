import React from "react";

import './App.css';
import Canvas from './Canvas';
import { useWindowSize } from "./common/windowsize"
import ApplicationBar from './components/ApplicationBar'

function App() {
  const [size] = useWindowSize()

  return (
    <div>
      <ApplicationBar height={55} />
      <Canvas width={size.width} height={size.height - 55} />
    </div>

  );
}

export default App;
