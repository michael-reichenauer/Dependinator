import { useState } from "react";
import { atom, useAtom } from "jotai"
import PubSub from 'pubsub-js'
import { makeStyles } from '@material-ui/core/styles';
import { Box, Collapse, Dialog, List, ListItem, ListItemIcon, ListItemText, Paper, Typography } from "@material-ui/core";
import SearchBar from "material-ui-search-bar";
import { ExpandLess, ExpandMore } from "@material-ui/icons";
import { icons } from './../common/icons';


const nodesAtom = atom(false)
const useStyles = makeStyles((theme) => ({
    root: {
        width: '100%',
        maxWidth: 360,
        backgroundColor: theme.palette.background.paper,
        position: 'relative',
        overflow: 'auto',
        maxHeight: 300,
    },
    nested: {
        paddingLeft: theme.spacing(4),
    },
    listSection: {
        backgroundColor: 'inherit',
    },
    ul: {
        backgroundColor: 'inherit',
        padding: 0,
    },
}));

export const useNodes = () => useAtom(nodesAtom)

export default function Nodes() {
    const classes = useStyles();
    const [show, setShow] = useNodes()
    const [openAzure, setOpenAzure] = useState(false);
    const [filter, setFilter] = useState('');


    const requestSearch = (value) => {
        setFilter(value.toLowerCase())
        if (!openAzure && value) {
            setOpenAzure(true)
        }
        console.log('Search', value)
    }

    const cancelSearch = () => {
        console.log('Cancel search')
    }

    const handleClick = () => {
        setOpenAzure(!openAzure);
    };

    const clickedItem = (item) => {
        PubSub.publish('canvas.addNode', item.key)
    }

    const getListItems = (items, first, filter) => {

        return items.map((item, i) => {
            if (item.parts[0] !== first) {
                return null
            }

            if (filter) {
                const filters = filter.split(' ')
                for (i = 0; i < filters.length; i++) {
                    if (!item.fullName.toLowerCase().includes(filters[i])) {
                        return null
                    }
                }
            }

            return (
                <ListItem button onClick={clickedItem(item)} key={item.key} className={classes.nested}>
                    <ListItemIcon>
                        <img src={item.src} alt='' width='30' height='30' />
                    </ListItemIcon>
                    <ListItemText primary={item.name} />
                </ListItem>

            )
        })
            .filter(item => item != null)
    }

    return (
        <Dialog open={show} onClose={() => { setShow(false) }}  >

            <Box style={{ width: 400, height: 530, padding: 20 }}>

                <Typography style={{ paddingBottom: 10 }} >Add Node</Typography>

                <SearchBar
                    value={filter}
                    onChange={(searchVal) => requestSearch(searchVal)}
                    onCancelSearch={() => cancelSearch()}
                />

                <Paper style={{ maxHeight: 440, overflow: 'auto' }}>
                    <List component="nav" dense disablePadding>
                        <ListItem button onClick={handleClick} key='az'>
                            <ListItemIcon>
                                <img src={icons.getIcon('Azure').src} alt='' width='30' height='30' />
                            </ListItemIcon>
                            <ListItemText primary="Azure" />
                            {openAzure ? <ExpandLess /> : <ExpandMore />}
                        </ListItem>
                        <Collapse in={openAzure} timeout="auto" unmountOnExit>
                            <List component="div" disablePadding dense>
                                {getListItems(icons.getAllIcons(), 'Azure', filter)}
                            </List>
                        </Collapse>
                    </List>
                </Paper>
            </Box>
        </Dialog >
    )
}

