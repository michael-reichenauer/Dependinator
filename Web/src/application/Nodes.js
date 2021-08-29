import { useState, useEffect } from "react";
import { atom, useAtom } from "jotai"
import PubSub from 'pubsub-js'
import { makeStyles } from '@material-ui/core/styles';
import { Box, Collapse, Dialog, List, ListItem, ListItemIcon, ListItemText, Paper, Typography } from "@material-ui/core";
import SearchBar from "material-ui-search-bar";
import { ExpandLess, ExpandMore } from "@material-ui/icons";
import { icons } from './../common/icons';
import { FixedSizeList } from 'react-window';

const subItemsSize = 9
const iconsSize = 30
const subItemsHeight = iconsSize + 6

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


const filterItems = (root, filter) => {
    const filters = filter.split(' ')
    const items = allIcons.filter(item => {
        // Check if root items match (e.g. searching Azure or Aws)
        if (item.root !== root) {
            return false
        }

        // This filter uses implicit 'AND' between words in the search filter
        // Check if all words are part of the items full name
        for (var i = 0; i < filters.length; i++) {
            if (!item.fullName.toLowerCase().includes(filters[i])) {
                return false
            }
        }
        return true
    })

    // Some icons exist in multiple groups, lets get unique items
    const uniqueItems = items.filter(
        (item, index) => index === items.findIndex(it => it.src === item.src && it.name === item.name))

    return uniqueItems
}


export default function Nodes() {
    const classes = useStyles();
    const [show, setShow] = useNodes()
    const [openAzure, setOpenAzure] = useState(false);
    const [openAws, setOpenAws] = useState(false);
    const [filter, setFilter] = useState('');

    useEffect(() => {
        // Listen for nodes.showDialog commands to show this Nodes dialog
        PubSub.subscribe('nodes.showDialog', (_, position) => setShow(position))
        return () => PubSub.unsubscribe('nodes.showDialog')
    }, [setShow])

    const onChangeSearch = (value) => {
        setFilter(value.toLowerCase())
    }

    const cancelSearch = () => {
        setFilter('')
    }

    // Expand Azure/aws sub items
    const handleAzureClick = () => setOpenAzure(!openAzure)
    const handleAwsClick = () => setOpenAws(!openAws)


    const clickedItem = (item) => {
        // show value can be a position or just 'true'. Is position, then the new node will be added
        // at that position. But is value is 'true', the new node position will centered in the diagram
        var position = show
        if (position === true) {
            position = null
        }

        PubSub.publish('canvas.AddNode', { icon: item.key, position: position })
        setShow(false)
    }

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

    // Azure and Aws items
    const azureItems = filterItems('Azure', filter)
    const azureHeight = Math.min(azureItems.length, subItemsSize) * subItemsHeight
    const renderAzureRow = (props) => renderRow(props, azureItems)
    const awsItems = filterItems('Aws', filter)
    const awsHeight = Math.min(awsItems.length, subItemsSize) * subItemsHeight
    const renderAwsRow = (props) => renderRow(props, awsItems)






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

                        {/* Azure items */}
                        <ListItem button onClick={handleAzureClick}>
                            <ListItemIcon>
                                <img src={icons.getIcon('Azure').src} alt='' width={iconsSize} height={iconsSize} />
                            </ListItemIcon>
                            <ListItemText primary="Azure" />
                            {openAzure ? <ExpandLess /> : <ExpandMore />}
                        </ListItem>
                        <Collapse in={openAzure} timeout="auto" unmountOnExit>
                            <FixedSizeList width={400} height={azureHeight} itemSize={subItemsHeight} itemCount={azureItems.length} >
                                {renderAzureRow}
                            </FixedSizeList>
                        </Collapse>

                        {/* Aws items */}
                        <ListItem button onClick={handleAwsClick}>
                            <ListItemIcon>
                                <img src={icons.getIcon('Aws').src} alt='' width={iconsSize} height={iconsSize} />
                            </ListItemIcon>
                            <ListItemText primary="Aws" />
                            {openAws ? <ExpandLess /> : <ExpandMore />}
                        </ListItem>
                        <Collapse in={openAws} timeout="auto" unmountOnExit>
                            <FixedSizeList width={400} height={awsHeight} itemSize={subItemsHeight} itemCount={awsItems.length} >
                                {renderAwsRow}
                            </FixedSizeList>
                        </Collapse>

                    </List>
                </Paper>
            </Box>
        </Dialog >
    )
}

