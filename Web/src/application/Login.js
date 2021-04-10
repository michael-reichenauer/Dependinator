import { atom, useAtom } from "jotai"
import { Box, Button, List, Popover, Typography } from "@material-ui/core";
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
    const isLocalDev = !store.isLocal()

    const hasShown = localStorage.getItem('showLogin')
    localStorage.setItem('showLogin', 'true')
    if (!hasShown) {
        setShow(true)
    }


    const onClick = (provider) => {
        setShow(false)
        store.login(provider)
    }

    return (
        <Popover
            open={show}
            onClose={() => setShow(false)}
            anchorEl={document.body}
            anchorOrigin={{ vertical: 'center', horizontal: 'center' }}
            transformOrigin={{ vertical: 'center', horizontal: 'center' }}
        >
            <Box style={{ width: 320, height: 350, padding: 20 }}>
                <Typography variant="h6">Enable Cloud Sync</Typography>
                <Typography >
                    Dependinator can sync diagrams between different devices if you
                    login with one of the supported identity providers:
            </Typography>
                <List component="nav" style={{ padding: 0 }}>

                    {!isLocalDev &&
                        <ListItemLink onClick={() => onClick('Google')}>
                            <ListItemText primary="Google" />
                        </ListItemLink>
                    }
                    {!isLocalDev &&
                        <ListItemLink onClick={() => onClick('Microsoft')}>
                            <ListItemText primary="Microsoft" />
                        </ListItemLink>
                    }
                    {!isLocalDev &&
                        <ListItemLink onClick={() => onClick('Facebook')}>
                            <ListItemText primary="Facebook" />
                        </ListItemLink>
                    }
                    {!isLocalDev &&
                        <ListItemLink onClick={() => onClick('GitHub')}>
                            <ListItemText primary="GitHub" />
                        </ListItemLink>
                    }
                    {isLocalDev &&
                        <ListItemLink onClick={() => onClick('Local')}>
                            <ListItemText primary="Local" />
                        </ListItemLink>
                    }

                </List>
                <Box style={{
                    position: 'absolute',
                    bottom: 20,
                    left: '40%',
                }}
                    textAlign='center'> <Button onClick={() => setShow(false)} variant="contained" >Later</Button>
                </Box>
            </Box>
        </Popover>
    )
}