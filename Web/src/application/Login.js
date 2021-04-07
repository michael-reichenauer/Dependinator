import { atom, useAtom } from "jotai"
import { Box, List, Popover, Typography } from "@material-ui/core";
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
        <Popover
            open={show}
            onClose={() => setShow(false)}
            anchorEl={document.body}
            anchorOrigin={{ vertical: 'center', horizontal: 'center' }}
            transformOrigin={{ vertical: 'center', horizontal: 'center' }}
        >
            <Box style={{ width: 400, height: 320, padding: 20 }}>
                <Typography variant="h6">Enable Cloud Sync</Typography>
                <Typography >
                    Dependinator can sync diagrams between different devices.
                    Login with one of the supported identity provides:
            </Typography>
                <List component="nav" style={{ padding: 10 }}>

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
            </Box>
        </Popover>
    )
}