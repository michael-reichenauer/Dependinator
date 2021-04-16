import { atom, useAtom } from "jotai"
import { Box, Button, Link, Popover, Tooltip, Typography } from "@material-ui/core";

import { store } from "./diagram/Store";
import { platformInfo } from "../common/info";

var QRCode = require('qrcode.react');

const qrConnectAtom = atom(true)

export const useQRConnectAtom = () => useAtom(qrConnectAtom)


export default function QRConnect() {
    const [show, setShow] = useQRConnectAtom()
    const { name, product, os } = platformInfo
    const now = Date.now()

    const randomId = makeRandomId()
    const url = `${window.location.protocol}//${window.location.host}?qr=${randomId}`

    // const onClick = (provider) => {
    //     setShow(false)
    //     store.login(provider)
    // }

    return (
        <Popover
            open={show}
            onClose={() => { }}
            anchorEl={document.body}
            anchorOrigin={{ vertical: 'center', horizontal: 'center' }}
            transformOrigin={{ vertical: 'center', horizontal: 'center' }}
        >
            <Box style={{ width: 320, height: 350, padding: 20 }}>
                <Typography variant="h6">Connect Devices with QR code</Typography>
                <Typography >
                    This device:
                </Typography>
                <Typography >
                    {name} on {product ?? os} at
                </Typography>
                <Typography >
                    {dateToLocalISO(now)}
                </Typography>

                <Box style={{ paddingTop: 30 }} textAlign='center'>
                    <Tooltip title={url} >
                        <Link href={url} target="_blank">
                            <QRCode value={url} />
                        </Link>
                    </Tooltip>
                </Box>
                <Box style={{ paddingTop: 5 }} textAlign='center'>
                    <Link href={url} target="_blank">
                        <Typography variant="body2">
                            {url}
                        </Typography>
                    </Link>
                </Box>



                <Box style={{ position: 'absolute', bottom: 20, left: '40%', }}
                    textAlign='center'> <Button onClick={() => setShow(false)} variant="contained" >Close</Button>
                </Box>
            </Box >
        </Popover >
    )
}

function dateToLocalISO(dateText) {
    const date = new Date(dateText)
    const off = date.getTimezoneOffset()
    const absoff = Math.abs(off)
    return (new Date(date.getTime() - off * 60 * 1000).toISOString().substr(0, 23) +
        (off > 0 ? '-' : '+') +
        (absoff / 60).toFixed(0).padStart(2, '0') + ':' +
        (absoff % 60).toString().padStart(2, '0'))
}

function makeRandomId() {
    let ID = "";
    let characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    for (var i = 0; i < 12; i++) {
        ID += characters.charAt(Math.floor(Math.random() * 36));
    }
    return ID;
}