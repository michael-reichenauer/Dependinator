import { useState, useEffect } from "react";
import { atom, useAtom } from "jotai"
import PubSub from 'pubsub-js'
import { makeStyles } from '@material-ui/core/styles';
import { Box, Collapse, Dialog, List, ListItem, ListItemIcon, ListItemText, Paper, Typography } from "@material-ui/core";
import SearchBar from "material-ui-search-bar";
import { ExpandLess, ExpandMore } from "@material-ui/icons";
import { icons } from './../common/icons';
import { FixedSizeList } from 'react-window';
import { useLocalStorage } from "../common/useLocalStorage";
import CropDinIcon from '@material-ui/icons/CropDin';

const subItemsSize = 9
const iconsSize = 30
const subItemsHeight = iconsSize + 6
const allIcons = icons.getAllIcons()

const nodesAtom = atom(false)
const useNodes = () => useAtom(nodesAtom)

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


export default function Nodes() {
    const [show, setShow] = useNodes()
    const [filter, setFilter] = useState('');
    const [mru, setMru] = useLocalStorage('nodesMru', [])

    useEffect(() => {
        // Listen for nodes.showDialog commands to show this Nodes dialog
        PubSub.subscribe('nodes.showDialog', (_, data) => setShow(data ?? true))
        return () => PubSub.unsubscribe('nodes.showDialog')
    }, [setShow])

    // Handle search
    const onChangeSearch = (value) => setFilter(value.toLowerCase())
    const cancelSearch = () => setFilter('')

    const clickedItem = (item) => {
        setShow(false)
        setMru([item.key, ...mru.filter(key => key !== item.key)].slice(0, 8))

        // show value can be a position or just 'true'. Is position, then the new node will be added
        // at that position. But is value is 'true', the new node position will centered in the diagram
        var position = null
        if (show !== true) {
            if (show.action) {
                show.action(item.key)
                return
            }

            position = show
        }

        console.log('Icon', item.key)
        PubSub.publish('canvas.AddNode', { icon: item.key, position: position })
    }

    const clickedGroup = () => {
        setShow(false)

        var position = null
        if (show !== true) {
            position = show
        }

        console.log('group', position)
        PubSub.publish('canvas.AddGroup', { position: position })
    }

    // The list of most recently used icons to make it easier to us
    const mruItems = (filter) => {
        const filterWords = filter.split(' ')

        return mru.slice(0, 8).map((key, i) => {
            const item = icons.getIcon(key)
            if (!isItemInFilterWords(item, filterWords)) {
                return null
            }
            return (
                <ListItem button onClick={() => clickedItem(item)} key={key} >
                    <ListItemIcon>
                        <img src={item.src} alt='' width={iconsSize} height={iconsSize} />
                    </ListItemIcon>
                    <ListItemText primary={item.name} />
                </ListItem>
            )
        }).filter(item => item !== null)
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

                <Paper style={{ maxHeight: 460, overflow: 'auto' }}>
                    <List component="nav" dense disablePadding>
                        <ListItem button onClick={clickedGroup}  >
                            <ListItemIcon>
                                <CropDinIcon />
                            </ListItemIcon>
                            <ListItemText primary='Group' />
                        </ListItem>
                        {mruItems(filter)}

                        {NodeItemsList('Azure', filter, clickedItem)}
                        {NodeItemsList('Aws', filter, clickedItem)}
                    </List>
                </Paper>

            </Box >
        </Dialog >
    )
}


// Items list for Azure and Aws (which can be filtered)
const NodeItemsList = (root, filter, clickedItem) => {
    const classes = useStyles();
    const items = filterItems(root, filter)
    const height = Math.min(items.length, subItemsSize) * subItemsHeight
    const [open, setOpen] = useState(false);
    const iconsSrc = icons.getIcon(root).src

    const renderRow = (props, items) => {
        const { index, style } = props;
        const item = items[index]

        return (
            <ListItem key={index} button style={style} onClick={() => clickedItem(item)} className={classes.nested}>
                <ListItemIcon>
                    <img src={item.src} alt='' width={iconsSize} height={iconsSize} />
                </ListItemIcon>
                <ListItemText primary={item.name} />
            </ListItem>
        );
    }

    return (
        <>
            <ListItem button onClick={() => setOpen(!open)}>
                <ListItemIcon>
                    <img src={iconsSrc} alt='' width={iconsSize} height={iconsSize} />
                </ListItemIcon>
                <ListItemText primary={root} />
                {open ? <ExpandLess /> : <ExpandMore />}
            </ListItem>
            <Collapse in={open} timeout="auto" unmountOnExit>
                <FixedSizeList width={380} height={height} itemSize={subItemsHeight} itemCount={items.length} >
                    {(props) => renderRow(props, items)}
                </FixedSizeList>
            </Collapse>
        </>
    )
}

// Filter node items based on root and filter
// This filter uses implicit 'AND' between words in the search filter
const filterItems = (root, filter) => {
    const filterWords = filter.split(' ')

    const items = allIcons.filter(item => {
        // Check if root items match (e.g. searching Azure or Aws)
        if (item.root !== root) {
            return false
        }

        return isItemInFilterWords(item, filterWords)
    })

    // Some icons exist in multiple groups, lets get unique items
    const uniqueItems = items.filter(
        (item, index) => index === items.findIndex(it => it.src === item.src && it.name === item.name))

    return uniqueItems
}

const isItemInFilterWords = (item, filterWords) => {
    // Check if all filter words are part of the items full name
    for (var i = 0; i < filterWords.length; i++) {
        if (!item.fullName.toLowerCase().includes(filterWords[i])) {
            return false
        }
    }
    return true
}