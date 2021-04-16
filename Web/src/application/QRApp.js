import { AppBar, Toolbar, Typography } from "@material-ui/core";
import React from "react";
import QRDeviceList from "./QRDeviceList";


const params = (new URL(document.location)).searchParams;
let qr = params.get('qr');
// if (!qr) {
//     qr = 'abc'
// }
console.log('qr', qr)


export const isQRApp = () => {
    return !!qr
}

export default function QRApp() {
    const qrId = localStorage.getItem('qrSyncId')
    //localStorage.setItem('hasShownAbout', 'true')


    return (
        <>
            <AppBar position="static">
                <Toolbar>
                    <Typography variant="h6" noWrap>Connect Devices</Typography>
                </Toolbar>
            </AppBar >

            <QRDeviceList />
        </>
    )
}