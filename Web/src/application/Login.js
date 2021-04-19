import { atom, useAtom } from "jotai"
import { Box, Button, Dialog, List, Typography } from "@material-ui/core";
import ListItem from '@material-ui/core/ListItem';
import ListItemText from '@material-ui/core/ListItemText';
import { store } from "./diagram/Store";


const loginAtom = atom(false)

export const useLogin = () => useAtom(loginAtom)


function ListItemLink(props) {
    return <ListItem button component="a" {...props} />;
}


export default function Login() {
    const [show, setShow] = useLogin()
    const isLocalDev = store.isLocal()

    const onClick = (provider) => {
        setShow(false)
        store.login(provider)
    }


    return (
        <Dialog open={show} onClose={() => { }} >
            <Box style={{ width: 320, height: 350, padding: 20 }}>
                <Typography variant="h6">Enable Cloud Sync</Typography>
                <Typography >
                    Dependinator can sync diagrams between different devices if you
                    login with one of the supported identity providers.
                </Typography>
                <Typography style={{ paddingTop: 15, fontWeight: 'bold' }} variant="subtitle1">Login with: </Typography>
                <List component="nav" style={{ paddingLeft: 20 }} >

                    <ListItemLink style={{ padding: 0 }} onClick={() => onClick('Custom')}>
                        <ListItemText primary="- Dependinator" />
                    </ListItemLink>

                    {!isLocalDev &&
                        <ListItemLink style={{ padding: 0 }} onClick={() => onClick('Google')}>
                            <ListItemText primary="- Google" />
                        </ListItemLink>
                    }
                    {!isLocalDev &&
                        <ListItemLink style={{ padding: 0 }} onClick={() => onClick('Microsoft')}>
                            <ListItemText primary="- Microsoft" />
                        </ListItemLink>
                    }
                    {!isLocalDev &&
                        <ListItemLink style={{ padding: 0 }} onClick={() => onClick('Facebook')}>
                            <ListItemText primary="- Facebook" />
                        </ListItemLink>
                    }
                    {!isLocalDev &&
                        <ListItemLink style={{ padding: 0 }} onClick={() => onClick('GitHub')}>
                            <ListItemText primary="- GitHub" />
                        </ListItemLink>
                    }


                </List>
                <Box style={{
                    position: 'absolute',
                    bottom: 20,
                    left: '40%',
                }}
                    textAlign='center'> <Button onClick={() => setShow(false)} variant="contained" color="primary">Later</Button>
                </Box>
            </Box>
        </Dialog>
    )
}