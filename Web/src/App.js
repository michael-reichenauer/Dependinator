import React from "react";
import { useWindowSize } from "./common/windowSizeX"
import ApplicationBar from './application/ApplicationBar'
import Diagram from "./application/Diagram";
import { useActivityMonitor } from "./common/activity";
import About from "./application/About";
import Login from "./application/Login";
import QRConnect from "./application/QRConnect";
import { useAppVersionMonitor } from "./common/appVersion";
import QRApp, { isQRApp } from "./application/QRApp";


function App() {
  const [size] = useWindowSize()
  useActivityMonitor()
  useAppVersionMonitor()

  if (isQRApp()) {
    return <QRApp />
  }

  return (
    <>
      <ApplicationBar height={55} />
      <Diagram width={size.width} height={size.height - 55} />
      <About />
      <Login />
      <QRConnect />
    </>
  );
}

export default App;
