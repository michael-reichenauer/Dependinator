import { useState, useEffect } from "react";
import { atom, useAtom } from "jotai"
import PubSub from 'pubsub-js'
import { makeStyles } from '@material-ui/core/styles';
import { Box, Collapse, Dialog, List, ListItem, ListItemIcon, ListItemText, Paper, Typography } from "@material-ui/core";
import SearchBar from "material-ui-search-bar";
import { ExpandLess, ExpandMore } from "@material-ui/icons";
import { defaultIconKey, icons } from './../common/icons';
import { FixedSizeList } from 'react-window';
import { useLocalStorage } from "../common/useLocalStorage";


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
    const classes = useStyles();
    const [show, setShow] = useNodes(false)
    const [filter, setFilter] = useState('');
    const [mruNodes, setMruNodes] = useLocalStorage('nodesMru', [])
    const [mruGroups, setMruGroups] = useLocalStorage('groupsMru', [])
    const [groupType, setGroupType] = useState(false)

    useEffect(() => {
        // Listen for nodes.showDialog commands to show this Nodes dialog
        PubSub.subscribe('nodes.showDialog', (_, data) => {
            console.log('data', data)
            setShow(data)
            setGroupType(!!data.group)
        })
        return () => PubSub.unsubscribe('nodes.showDialog')
    }, [setShow])

    var [mru, setMru] = [mruNodes, setMruNodes]
    if (groupType) {
        [mru, setMru] = [mruGroups, setMruGroups]
    }

    // Handle search
    const onChangeSearch = (value) => setFilter(value.toLowerCase())
    const cancelSearch = () => setFilter('')

    const titleType = groupType ? 'Group' : 'Node'
    const title = !!show && show.add ? `Add ${titleType}` : `Change Icon`

    const addToMru = (list, key) => {
        const newList = [key, ...list.filter(k => k !== key && k !== defaultIconKey)]
            .slice(0, 8)
        return newList
    }

    const clickedItem = (item) => {
        setShow(false)
        setGroupType(false)
        setMru(addToMru(mru, item.key))

        if (show.action) {
            show.action(item.key)
            return
        }

        // show value can be a position or just 'true'. Is position, then the new node will be added
        // at that position. But is value is 'true', the new node position will centered in the diagram
        var position = null

        if (show.x) {
            position = { x: show.x, y: show.y }
        }

        console.log('Icon', item.key)
        PubSub.publish('canvas.AddNode', { icon: item.key, position: position, group: groupType })
    }


    // The list of most recently used icons to make it easier to us
    const mruItems = (filter) => {
        const filterWords = filter.split(' ')

        return mru.slice(0, 8).map((key, i) => {
            const item = icons.getIcon(key)
            if (!isItemInFilterWords(item, filterWords)) {
                return null
            }
            if (item.key === defaultIconKey) {
                return null
            }
            return (
                <ListItem button onClick={() => clickedItem(item)} key={key} className={classes.nested}>
                    <ListItemIcon>
                        <img src={item.src} alt='' width={iconsSize} height={iconsSize} />
                    </ListItemIcon>
                    <ListItemText primary={item.name} />
                </ListItem>
            )
        }).filter(item => item !== null)
    }

    return (
        <Dialog open={!!show} onClose={() => { setShow(false); setGroupType(false) }}  >
            <Box style={{ width: 400, height: 530, padding: 20 }}>

                <Typography variant="h5" style={{ paddingBottom: 10, }} >{title}</Typography>

                <SearchBar
                    value={filter}
                    onChange={(searchVal) => onChangeSearch(searchVal)}
                    onCancelSearch={() => cancelSearch()}
                />

                <Paper style={{ maxHeight: 450, overflow: 'auto', marginTop: 3 }}>
                    <List component="nav" dense disablePadding >
                        {mruItems(filter)}

                        {NodeItemsList('Azure', 'Azure icons', filter, clickedItem)}
                        {NodeItemsList('Aws', 'Aws icons', filter, clickedItem)}
                    </List>
                </Paper>

            </Box >
        </Dialog >
    )
}


// Items list for Azure and Aws (which can be filtered)
const NodeItemsList = (root, name, filter, clickedItem) => {
    const classes = useStyles();
    const filteredItems = filterItems(root, filter)
    const items = groupedItems(filteredItems)
    const height = Math.min(items.length, subItemsSize) * subItemsHeight
    const [open, setOpen] = useState(false);
    const iconsSrc = icons.getIcon(root).src

    const toggleList = () => {
        setOpen(!open)
    }


    const renderRow = (props, items) => {
        const { index, style } = props;
        const item = items[index]

        if (item.groupHeader) {
            return (
                <ListItem key={index} button style={style} className={classes.nested}>
                    <Typography variant='caption' >{item.groupHeader.replace('/', ' - ')}</Typography>
                </ListItem>
            )
        }

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
            <Box border={open ? 1 : 0}>
                <ListItem button onClick={toggleList}>
                    <ListItemIcon>
                        <img src={iconsSrc} alt='' width={iconsSize} height={iconsSize} />
                    </ListItemIcon>
                    <ListItemText primary={name} />
                    {open ? <ExpandLess /> : <ExpandMore />}
                </ListItem>
                <Collapse in={open} timeout="auto" unmountOnExit>

                    <FixedSizeList width={380} height={height} itemSize={subItemsHeight} itemCount={items.length} >
                        {(props) => renderRow(props, items)}
                    </FixedSizeList>

                </Collapse>
            </Box>
        </>
    )
}

const groupedItems = (items) => {
    var it = []
    var group = ''
    for (var i = 0; i < items.length; i++) {
        if (items[i].group !== group) {
            group = items[i].group
            it.push({ groupHeader: group })
        }
        it.push(items[i])
    }

    return it
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