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
// import { dataCrypt } from './common/dataCrypt';
import Nodes from "./application/Nodes";
import NodeLabelDialog from "./application/diagram/LabelEditor";

const App: React.FC = () => {
  const [size] = useWindowSize();

  // Enable user activity detection (e.g. moving mouse ) and new available web site at server detection
  useActivityMonitor();
  useAppVersionMonitor();

  // const org = '123456'
  // const password = 'abcd'

  // const edp = dataCrypt.encryptWithPassword(org, password)
  // edp.then(ed => {
  //   console.log('original: ', org)
  //   console.log('encrypted:', ed)
  //   const ddp = dataCrypt.decryptWithPassword(ed, password)
  //   ddp.then(dd => {
  //     console.log('decrypted:', dd)
  //     if (dd !== org) {
  //       console.error('Not same data', dd, org)
  //     }
  //   })
  //     .catch(e => console.error(e))
  // })

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