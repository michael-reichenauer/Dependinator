import React from "react";
import { List, ListItem, ListItemText, Typography } from "@material-ui/core";
import { store } from "./diagram/Store";



function ListItemLink(props) {
    return <ListItem button component="a" {...props} />;
}


export default function QRDeviceList() {
    const onClick = (provider) => {
        store.login(provider)
    }

    return (
        <div style={{ margin: 15 }} >
            <Typography >
                Dependinator can sync diagrams between different devices if you accept the specified device.
           </Typography>
            <Typography style={{ paddingTop: 15, fontWeight: 'bold' }} variant="subtitle1">Login with: </Typography>
            <List component="nav" style={{ paddingLeft: 20 }} >


                <ListItemLink style={{ padding: 0 }} onClick={() => onClick('Google')}>
                    <ListItemText primary="- Google" />
                </ListItemLink>


                <ListItemLink style={{ padding: 0 }} onClick={() => onClick('Microsoft')}>
                    <ListItemText primary="- Microsoft" />
                </ListItemLink>


                <ListItemLink style={{ padding: 0 }} onClick={() => onClick('Facebook')}>
                    <ListItemText primary="- Facebook" />
                </ListItemLink>

            </List>
        </div>
    )
}