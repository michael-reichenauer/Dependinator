import { atom, useAtom } from "jotai"
import { Box, Button, Dialog, Link, Tooltip, Typography } from "@material-ui/core";
import { localBuildTime, localSha } from "../common/appVersion";
import { useLogin } from "./Login";

const aboutAtom = atom(false)

export const useAbout = () => useAtom(aboutAtom)

export default function About() {
    const [show, setShow] = useAbout()
    const [, setShowLogin] = useLogin()

    const hasShown = localStorage.getItem('hasShownAbout')
    localStorage.setItem('hasShownAbout', 'true')

    if (!show && hasShown !== 'true') {
        console.log('Set timeout')
        setTimeout(() => setShow(true), 3000);
    }

    const enableCloudSync = () => {
        setShowLogin(true);
    }

    return (
        <Dialog open={show} onClose={() => { }} >
            <Box style={{ width: 320, height: 330, padding: 20 }}>
                <Tooltip title={`version: ${localBuildTime} (${localSha.substring(0, 6)})`}>
                    <Typography variant="h5">About Dependinator</Typography>
                </Tooltip>
                <Typography >
                    A tool for modeling and visualizing software architecture inspired by map tools for
                    navigation.
                </Typography>

                <Typography style={{ paddingTop: 10 }} >
                    Checkout the  "<Link href="https://c4model.com" target="_blank">C4 Model</Link>"
                    by Simon Brown  to better understand on how to use the tool.
                </Typography>
                <Typography style={{ paddingTop: 10 }} >
                    You can sync diagrams between different devices if you <Link onClick={enableCloudSync}>enable cloud sync</Link>
                </Typography>
                <Typography style={{ paddingTop: 30 }} variant="body2">
                    Hint: Use context menus to access functionality.
                </Typography>

                <Box style={{ position: 'absolute', bottom: 20, left: '40%', }}
                    textAlign='center'> <Button onClick={() => setShow(false)} variant="contained" color="primary">Close</Button>
                </Box>
            </Box>
        </Dialog>
    )
}