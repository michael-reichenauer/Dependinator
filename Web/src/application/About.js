import { atom, useAtom } from "jotai"
import { Box, Button, Link, Popover, Typography } from "@material-ui/core";

const aboutAtom = atom(false)

export const useAbout = () => useAtom(aboutAtom)

export default function About() {
    const [show, setShow] = useAbout()

    return (
        <Popover
            open={show}
            onClose={() => setShow(false)}
            anchorEl={document.body}
            anchorOrigin={{ vertical: 'center', horizontal: 'center' }}
            transformOrigin={{ vertical: 'center', horizontal: 'center' }}
        >
            <Box style={{ width: 320, height: 200, padding: 20 }}>
                <Typography variant="h5">Dependinator</Typography>
                <Typography >
                    A tool for visualizing software architecture inspired by map tools for
                navigation and the "<Link href="https://c4model.com" target="_blank">C4 Model</Link>"
                by Simon Brown.
                </Typography>
                <Box style={{
                    position: 'absolute',
                    bottom: 20,
                    left: '40%',
                }}
                    textAlign='center'> <Button onClick={() => setShow(false)} variant="contained" >Close</Button>
                </Box>
            </Box>
        </Popover>
    )
}