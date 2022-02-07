import { ApplicationBar } from "./application/ApplicationBar";
import React from "react";
import useWindowSize from "./common/windowSize";
import Activity, { useActivityMonitor } from "./common/activity";
import { useAppVersionMonitor } from "./common/appVersion";
import Diagram from "./application/Diagram";
import About from "./application/About";
import { LoginDlg } from "./application/LoginDlg";
import AlertDialog from "./common/AlertDialog";
import PromptDialog from "./common/PromptDialog";
import Nodes from "./application/Nodes";
import NodeLabelDialog from "./application/diagram/LabelEditor";

const App: React.FC = () => {
  const [size] = useWindowSize();

  // Enable user activity detection (e.g. moving mouse ) and new available web site at server detection
  useActivityMonitor();
  useAppVersionMonitor();

  return (
    <>
      <ApplicationBar height={55} />
      <Diagram width={size.width} height={size.height - 55} />
      <About />
      <LoginDlg />
      <Nodes />
      <AlertDialog />
      <PromptDialog />
      <NodeLabelDialog />
      <Activity />
    </>
  );
};

export default App;
