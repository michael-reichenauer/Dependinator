import { useState, useEffect } from "react";
import { atom, useAtom } from "jotai"
import PubSub from 'pubsub-js'
import { makeStyles } from '@material-ui/core/styles';
import { Box, Dialog, Button, ListItem, ListItemIcon, ListItemText, Typography, Menu } from "@material-ui/core";
import SearchBar from "material-ui-search-bar";
import Stack from '@mui/material/Stack';
import { defaultIconKey, greenNumberIconKey, icons } from './../common/icons';
import { FixedSizeList } from 'react-window';
import { useLocalStorage } from "../common/useLocalStorage";
import { MenuItem } from "@mui/material";
import CheckIcon from '@material-ui/icons/Check';
import KeyboardArrowDownIcon from '@material-ui/icons/KeyboardArrowDown';


const subItemsSize = 12
const mruSize = 8
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
    topScrollPaper: {
        alignItems: 'flex-start',
    },
    topPaperScrollBody: {
        verticalAlign: 'top',
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
    const [iconSets, setIconSets] = useLocalStorage('iconSets', ['Azure', 'Aws', 'OSA'])
    const [groupType, setGroupType] = useState(false)
    const [anchorEl, setAnchorEl] = useState(null);
    const open = Boolean(anchorEl);

    useEffect(() => {
        // Listen for nodes.showDialog commands to show this Nodes dialog
        PubSub.subscribe('nodes.showDialog', (_, data) => {
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
            .slice(0, mruSize)
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

        PubSub.publish('canvas.AddNode', { icon: item.key, position: position, group: groupType })
    }

    const handleMenuSelect = (iconSet) => {
        if (iconSets.includes(iconSet)) {
            setIconSets(iconSets.filter(i => i !== iconSet))
        } else {
            iconSets.push(iconSet)
            setIconSets(iconSets)
        }

        setAnchorEl(null);
    };

    return (
        <Dialog open={!!show} onClose={() => { setShow(false); setGroupType(false) }}
            classes={{
                scrollPaper: classes.topScrollPaper,
                paperScrollBody: classes.topPaperScrollBody,
            }} >
            <Box style={{ width: 400, height: 530, padding: 20 }}>
                <Stack direction="row" spacing={5} style={{ paddingBottom: 10, }}>

                    <Typography variant="subtitle1"  >{title}</Typography>
                    <Button
                        variant="contained"
                        disableElevation
                        onClick={e => setAnchorEl(e.currentTarget)}
                        endIcon={<KeyboardArrowDownIcon />}
                    >
                        Icon sets
                    </Button>
                </Stack>
                <Menu
                    anchorEl={anchorEl}
                    open={open}
                    onClose={() => setAnchorEl(null)}
                    anchorOrigin={{
                        vertical: 'top',
                        horizontal: 'right',
                    }}
                    transformOrigin={{
                        vertical: 'top',
                        horizontal: 'right',
                    }}
                >
                    <MenuItem onClick={() => handleMenuSelect('Azure')}>
                        <ListItemIcon>
                            {iconSets.includes('Azure') && <CheckIcon fontSize="small" />}
                        </ListItemIcon>
                        Azure
                    </MenuItem>
                    <MenuItem onClick={() => handleMenuSelect('Aws')}>
                        <ListItemIcon>
                            {iconSets.includes('Aws') && <CheckIcon fontSize="small" />}
                        </ListItemIcon>
                        Aws
                    </MenuItem>
                    <MenuItem onClick={() => handleMenuSelect('OSA')}>
                        <ListItemIcon>
                            {iconSets.includes('OSA') && <CheckIcon fontSize="small" />}
                        </ListItemIcon>
                        OSA
                    </MenuItem>
                </Menu>

                <SearchBar
                    value={filter}
                    onChange={(searchVal) => onChangeSearch(searchVal)}
                    onCancelSearch={() => cancelSearch()}
                />

                {NodesList(iconSets, mru, filter, clickedItem)}

            </Box >
        </Dialog >
    )
}


const NodesList = (roots, mru, filter, clickedItem) => {
    const filteredItems = filterItems(mru, roots, filter)
    const items = groupedItems(filteredItems)
    const height = Math.min(items.length, subItemsSize) * subItemsHeight

    const renderRow = (props, items) => {
        const { index, style } = props;
        const item = items[index]

        if (item.groupHeader) {
            return (
                <ListItem key={index} button style={style} >
                    <Typography variant='caption' >{item.groupHeader.replace('/', ' - ')}</Typography>
                </ListItem>
            )
        }

        return (
            <ListItem key={index} button style={style} onClick={() => clickedItem(item)} >
                <ListItemIcon>
                    <img src={item.src} alt='' width={iconsSize} height={iconsSize} />
                </ListItemIcon>
                <ListItemText primary={item.name} />
            </ListItem>
        );
    }

    return (
        <FixedSizeList height={height} itemSize={subItemsHeight} itemCount={items.length} >
            {(props) => renderRow(props, items)}
        </FixedSizeList>
    )
}


const groupedItems = (items) => {
    var it = []
    var group = ''
    for (var i = 0; i < items.length; i++) {
        const item = items[i]
        if (item.key !== greenNumberIconKey) {
            const itemGroup = items[i].isMru ? 'Recently used' : items[i].root + ': ' + items[i].group
            if (itemGroup !== group) {
                group = itemGroup
                it.push({ groupHeader: itemGroup })
            }
        }
        it.push(items[i])
    }

    return it
}

// Filter node items based on root and filter
// This filter uses implicit 'AND' between words in the search filter
const filterItems = (mru, roots, filter) => {
    const filterWords = filter.split(' ')

    const items = allIcons.filter(item => {
        // Check if root items match (e.g. searching Azure or Aws)
        if (!roots.includes(item.root)) {
            return false
        }

        return isItemInFilterWords(item, filterWords)
    })


    // Some icons exist in multiple groups, lets get unique items
    const uniqueItems = items.filter(
        (item, index) => index === items.findIndex(it => it.src === item.src && it.name === item.name))

    const mruItems = mru.filter(mruItem => {
        if (mruItem === defaultIconKey || mruItem === greenNumberIconKey) {
            return false
        }

        const item = icons.getIcon(mruItem)
        return isItemInFilterWords(item, filterWords)
    })
        .map(item => ({ ...icons.getIcon(item), isMru: true }))

    const numberItem = icons.getIcon(greenNumberIconKey)
    return [numberItem].concat(mruItems, uniqueItems)
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