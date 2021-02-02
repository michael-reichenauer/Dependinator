import React from "react";

import './App.css';
import Canvas from './Canvas';
import { useWindowSize } from "./common/windowsize"


function App() {
  const [size] = useWindowSize()

  return (
    <div>
      <div style={{ margin: 100, backgroundColor: "green" }}>
        <Canvas width={size.width - 200} height={size.height - 200} />
      </div>
    </div>

  );
}

export default App;
