import { useState, useEffect } from "react";
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
const allIcons = icons.getAllIcons()



export default function Nodes() {
    const classes = useStyles();
    const [show, setShow] = useNodes()
    const [openAzure, setOpenAzure] = useState(false);
    const [openAws, setOpenAws] = useState(false);
    const [filter, setFilter] = useState('');

    useEffect(() => {
        PubSub.subscribe('nodes.showDialog', (_, position) => setShow(position))
        return () => PubSub.unsubscribe('nodes.showDialog')
    }, [setShow])

    const onChangeSearch = (value) => {
        setFilter(value.toLowerCase())
    }

    const cancelSearch = () => {
        setFilter('')
    }

    const handleAzureClick = () => {
        setOpenAzure(!openAzure)
    };

    const handleAwsClick = () => {
        setOpenAws(!openAws)
    };

    const clickedItem = (item) => {
        var position = show
        console.log('show', show)
        if (position === true) {
            position = null
        }
        PubSub.publish('canvas.AddNode', { icon: item.key, position: position })
        setShow(false)
    }

    const getListItems = (items, root, filter) => {

        // Filter
        const filtered = items.filter(item => {
            // Check if root items match (e.g. searching Azure or Aws)
            if (item.root !== root) {
                return false
            }

            // This filter uses implicit 'AND' between words in the search filter
            // Check if all words are part of the items full name
            const filters = filter.split(' ')
            for (var i = 0; i < filters.length; i++) {
                if (!item.fullName.toLowerCase().includes(filters[i])) {
                    return false
                }
            }
            return true
        })

        // In some cases the same icon exist in different groups, lets get unique items
        const unique = filtered.filter(
            (item, index) => index === filtered.findIndex(it => it.src === item.src && it.name === item.name))

        return unique.map((item, i) => {
            return (
                <ListItem button onClick={() => clickedItem(item)} key={item.key} className={classes.nested}>
                    <ListItemIcon>
                        <img src={item.src} alt='' width='30' height='30' />
                    </ListItemIcon>
                    <ListItemText primary={item.name} />
                </ListItem>
            )
        })
    }

    return (
        <Dialog open={show ? true : false} onClose={() => { setShow(false) }}  >

            <Box style={{ width: 400, height: 530, padding: 20 }}>

                <Typography style={{ paddingBottom: 10 }} >Add Node</Typography>

                <SearchBar
                    value={filter}
                    onChange={(searchVal) => onChangeSearch(searchVal)}
                    onCancelSearch={() => cancelSearch()}
                />

                <Paper style={{ maxHeight: 440, overflow: 'auto' }}>
                    <List component="nav" dense disablePadding>
                        <ListItem button onClick={handleAzureClick} key='az'>
                            <ListItemIcon>
                                <img src={icons.getIcon('Azure').src} alt='' width='30' height='30' />
                            </ListItemIcon>
                            <ListItemText primary="Azure" />
                            {openAzure ? <ExpandLess /> : <ExpandMore />}
                        </ListItem>
                        <Collapse in={openAzure} timeout="auto" unmountOnExit>
                            <List component="div" disablePadding dense>
                                {getListItems(allIcons, 'Azure', filter)}
                            </List>
                        </Collapse>
                        <ListItem button onClick={handleAwsClick} key='aw'>
                            <ListItemIcon>
                                <img src={icons.getIcon('Aws').src} alt='' width='30' height='30' />
                            </ListItemIcon>
                            <ListItemText primary="Aws" />
                            {openAws ? <ExpandLess /> : <ExpandMore />}
                        </ListItem>
                        <Collapse in={openAws} timeout="auto" unmountOnExit>
                            <List component="div" disablePadding dense>
                                {getListItems(allIcons, 'Aws', filter)}
                            </List>
                        </Collapse>
                    </List>
                </Paper>
            </Box>
        </Dialog >
    )
}

