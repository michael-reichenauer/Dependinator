const azure = require('azure-storage');
var table = require('../shared/table.js');
var clientInfo = require('../shared/clientInfo.js');
var auth = require('../shared/auth.js');

const entGen = azure.TableUtilities.entityGenerator;
const baseTableName = 'diagrams'
const partitionKeyName = 'dep'
const usersTableName = 'users'
const userPartitionKey = 'users'


exports.connect = async (context) => {
    const req = context.req
    const clientPrincipal = auth.getClientPrincipal(req)

    const userId = clientPrincipal.userId
    if (!userId) {
        return null
    }

    try {
        const entity = await table.retrieveEntity(usersTableName, userPartitionKey, userId)
        context.log('got user', userId, entity)
        if (entity.tableId) {
            context.log('got user', userId, entity)
            await table.createTableIfNotExists(entity.tableId)
            return { token: entity.tableId }
        }
        context.log('Failed to get table id')
    } catch (err) {
        context.log('failed to get', userId, err)
        // User not yet added 
    }

    // Create a new random diagrams table id to be used for the user
    const tableId = baseTableName + makeRandomId()

    // Create a user in the users table
    await table.createTableIfNotExists(usersTableName)
    const batch = new azure.TableBatch()
    batch.insertEntity(toUserItem(userId, tableId))
    await table.executeBatch(usersTableName, batch)

    // Create the actual diagram table
    await table.createTableIfNotExists(tableId)
    return { token: tableId }
}

exports.newDiagram = async (context, diagram) => {
    const tableName = getTableName(context)
    const { diagramId, name } = diagram.diagramData

    const canvasData = diagram.canvases ? diagram.canvases[0] : null
    if (!diagramId || !name || !canvasData) {
        throw new Error('missing parameters: ');
    }


    const diagramData = { diagramId: diagramId, name: name, accessed: Date.now() }

    const batch = new azure.TableBatch()
    batch.insertEntity(toDiagramDataItem(diagramData))
    batch.insertEntity(toCanvasDataItem(canvasData))

    await table.executeBatch(tableName, batch)

    const entity = await table.retrieveEntity(tableName, partitionKeyName, diagramKey(diagramId))
    return toDiagramInfo(entity)
}

exports.setCanvas = async (context, canvasData) => {
    const tableName = getTableName(context)
    if (!canvasData) {
        throw new Error('missing parameters');
    }

    const { diagramId } = canvasData
    const diagramData = { diagramId: diagramId, accessed: Date.now() }

    const batch = new azure.TableBatch()
    batch.mergeEntity(toDiagramDataItem(diagramData))
    batch.insertOrReplaceEntity(toCanvasDataItem(canvasData))

    await table.executeBatch(tableName, batch)

    const entity = await table.retrieveEntity(tableName, partitionKeyName, diagramKey(diagramId))
    return toDiagramInfo(entity)
}

exports.getAllDiagramsData = async (context) => {
    const tableName = getTableName(context)

    var tableQuery = new azure.TableQuery()
        .where('type == ?string?', 'diagram');

    const items = await table.queryEntities(tableName, tableQuery, null)
    context.log(`queried: ${items.length}`)

    return items.map(i => toDiagramInfo(i))
}

exports.getDiagram = async (context, diagramId) => {
    const tableName = getTableName(context)
    context.log('table name', tableName)

    let tableQuery = new azure.TableQuery()
        .where('diagramId == ?string?', diagramId);

    const items = await table.queryEntities(tableName, tableQuery, null)

    const diagram = { canvases: [] }

    items.forEach(i => {
        if (i.type === 'diagram') {
            diagram.diagramData = toDiagramInfo(i)
        } else if (i.type === 'canvas') {
            diagram.canvases.push(toCanvasData(i))
        }
    })

    if (!diagram.diagramData || diagram.canvases.length == 0) {
        throw new Error('NOTFOUND')
    }

    // Update accessed diagram time
    const diagramData = { diagramId: diagramId, accessed: Date.now() }
    const batch = new azure.TableBatch()
    batch.mergeEntity(toDiagramDataItem(diagramData))
    await table.executeBatch(tableName, batch)

    return diagram
}

exports.deleteDiagram = async (context, parameters) => {
    const tableName = getTableName(context)
    const { diagramId } = parameters
    if (!diagramId) {
        throw new Error('Missing parameter')
    }

    let tableQuery = new azure.TableQuery()
        .where('diagramId == ?string?', diagramId);

    const items = await table.queryEntities(tableName, tableQuery, null)
    context.log(`queried: ${items.length}`)

    const batch = new azure.TableBatch()
    items.forEach(i => {
        if (i.type === 'diagram') {
            batch.deleteEntity(toDiagramDataItem(toDiagramInfo(i)))
        } else if (i.type === 'canvas') {
            batch.deleteEntity(toCanvasDataItem(toCanvasData(i)))
        }
    })

    await table.executeBatch(tableName, batch)
}

exports.updateDiagram = async (context, diagram) => {
    const tableName = getTableName(context)
    const { diagramId } = diagram.diagramData
    if (!diagramId) {
        throw new Error('missing parameters: ');
    }

    const diagramData = { ...diagram.diagramData, accessed: Date.now() }

    const batch = new azure.TableBatch()
    batch.mergeEntity(toDiagramDataItem(diagramData))
    if (diagram.canvases) {
        diagram.canvases.forEach(canvasData => batch.insertOrReplaceEntity(toCanvasDataItem(canvasData)))
    }


    await table.executeBatch(tableName, batch)

    const entity = await table.retrieveEntity(tableName, partitionKeyName, diagramKey(diagramId))
    return toDiagramInfo(entity)
}


exports.uploadDiagrams = async (context, diagrams) => {
    const tableName = getTableName(context)
    if (!diagrams) {
        throw new Error('missing parameters: ');
    }

    const batch = new azure.TableBatch()
    diagrams.forEach(diagram => {
        const diagramData = { ...diagram.diagramData, accessed: Date.now() }
        batch.insertOrMergeEntity(toDiagramDataItem(diagramData))
        if (diagram.canvases) {
            diagram.canvases.forEach(canvasData => batch.insertOrReplaceEntity(toCanvasDataItem(canvasData)))
        }
    })

    await table.executeBatch(tableName, batch)
}

exports.downloadAllDiagrams = async (context) => {
    const tableName = getTableName(context)

    let tableQuery = new azure.TableQuery()
        .where('type == ?string? || type == ?string?', 'diagram', 'canvas');

    const items = await table.queryEntities(tableName, tableQuery, null)

    const diagrams = {}

    items.forEach(i => {
        if (i.type === 'diagram') {
            const diagramData = toDiagramInfo(i)
            const id = diagramData.diagramId
            diagrams[id] = { ...diagrams[id], diagramData: diagramData }
        } else if (i.type === 'canvas') {
            const canvasData = toCanvasData(i)
            const id = canvasData.diagramId
            if (diagrams[id] == null) {
                diagrams[id] = { canvases: [canvasData] }
            } else {
                const canvases = diagrams[id].canvases ? diagrams[id].canvases : []
                canvases.push(canvasData)
                diagrams[id].canvases = canvases
            }
        }
    })

    return Object.entries(diagrams).map(e => e[1])
}




// // -----------------------------------------------------------------


function makeRandomId() {
    let ID = "";
    let characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    for (var i = 0; i < 12; i++) {
        ID += characters.charAt(Math.floor(Math.random() * 36));
    }
    return ID;
}

async function delay(time) {
    return new Promise(res => {
        setTimeout(res, time)
    })
}


function getTableName(context) {
    const info = clientInfo.getInfo(context)
    if (!info.token) {
        throw new Error('Invalid token')
    }

    return info.token
}

function canvasKey(diagramId, canvasId) {
    return `${diagramId}.${canvasId}`
}

function diagramKey(diagramId) {
    return `${diagramId}`
}

function toUserItem(userId, tableId) {
    return {
        RowKey: entGen.String(userId),
        PartitionKey: entGen.String(userPartitionKey),

        tableId: entGen.String(tableId),
    }
}

function toCanvasDataItem(canvasData) {
    const { diagramId, canvasId } = canvasData
    return {
        RowKey: entGen.String(canvasKey(diagramId, canvasId)),
        PartitionKey: entGen.String(partitionKeyName),

        type: entGen.String('canvas'),
        diagramId: entGen.String(diagramId),
        canvasId: entGen.String(canvasId),
        canvasData: entGen.String(JSON.stringify(canvasData))
    }
}

function toCanvasData(item) {
    const canvasData = JSON.parse(item.canvasData)
    canvasData.etag = item['odata.etag']
    canvasData.timestamp = item.Timestamp
    return canvasData
}

function toDiagramDataItem(diagramData) {
    const { diagramId, name, accessed } = diagramData
    const item = {
        RowKey: entGen.String(diagramKey(diagramId)),
        PartitionKey: entGen.String(partitionKeyName),

        type: entGen.String('diagram'),
        diagramId: entGen.String(diagramId),
    }
    if (name != null) {
        item.name = entGen.String(name)
    }
    if (accessed != null) {
        item.accessed = entGen.Int64(accessed)
    }
    return item
}

function toDiagramInfo(item) {
    return {
        etag: item['odata.etag'],
        timestamp: item.Timestamp,
        diagramId: item.diagramId,
        name: item.name,
        accessed: item.accessed,
    }
}
