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
    const userDetails = clientPrincipal.userDetails
    if (!userId) {
        throw new Error('No user id')
    }

    try {
        const entity = await table.retrieveEntity(usersTableName, userPartitionKey, userId)
        context.log('got user', userId, entity)
        if (entity.tableId) {
            context.log('got user', userId, entity)
            const tableName = baseTableName + entity.tableId
            await table.createTableIfNotExists(tableName)
            await table.insertOrReplaceEntity(tableName, toTableUserItem(clientPrincipal))
            return { token: entity.tableId }
        }
        context.log('Failed to get table id')
    } catch (err) {
        context.log('failed to get', userId, err)
        // User not yet added 
    }

    // Create a new random diagrams table id to be used for the user
    const tableId = makeRandomId()
    const tableName = baseTableName + tableId

    // Create the actual diagram table
    await table.createTableIfNotExists(tableName)
    await table.insertOrReplaceEntity(tableName, toTableUserItem(clientPrincipal))

    // Create a user in the users table
    await table.createTableIfNotExists(usersTableName)
    await table.insertOrReplaceEntity(usersTableName, toUserItem(clientPrincipal, tableId))

    return { token: tableId }
}


exports.clearAllData = async (context) => {
    const req = context.req
    const clientPrincipal = auth.getClientPrincipal(req)

    const userId = clientPrincipal.userId
    if (!userId) {
        throw new Error('No user id')
    }

    const tableName = getTableName(context)
    await table.deleteTableIfExists(tableName)

    try {
        await table.deleteEntity(usersTableName, toUserItem(userId, '', ''))
    } catch (error) {
        // No user, so done
    }
}


exports.newDiagram = async (context, diagram) => {
    const tableName = getTableName(context)
    const { diagramId, name } = diagram.diagramInfo

    const canvas = diagram.canvases ? diagram.canvases[0] : null
    if (!diagramId || !name || !canvas) {
        throw new Error('missing parameters: ');
    }

    const diagramInfo = { diagramId: diagramId, name: name, accessed: Date.now() }

    const batch = new azure.TableBatch()
    batch.insertEntity(toDiagramInfoItem(diagramInfo))
    batch.insertEntity(toCanvasItem(canvas))

    await table.executeBatch(tableName, batch)

    const entity = await table.retrieveEntity(tableName, partitionKeyName, diagramKey(diagramId))
    return toDiagramInfo(entity)
}

exports.setCanvas = async (context, canvas) => {
    const tableName = getTableName(context)
    if (!canvas) {
        throw new Error('missing parameters');
    }

    const { diagramId } = canvas
    const diagramInfo = { diagramId: diagramId, accessed: Date.now() }

    const batch = new azure.TableBatch()
    batch.mergeEntity(toDiagramInfoItem(diagramInfo))
    batch.insertOrReplaceEntity(toCanvasItem(canvas))

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
            diagram.diagramInfo = toDiagramInfo(i)
        } else if (i.type === 'canvas') {
            diagram.canvases.push(toCanvas(i))
        }
    })

    if (!diagram.diagramInfo || diagram.canvases.length == 0) {
        throw new Error('NOTFOUND')
    }

    // Update accessed diagram time
    const diagramInfo = { diagramId: diagramId, accessed: Date.now() }
    const batch = new azure.TableBatch()
    batch.mergeEntity(toDiagramInfoItem(diagramInfo))
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
            batch.deleteEntity(toDiagramInfoItem(toDiagramInfo(i)))
        } else if (i.type === 'canvas') {
            batch.deleteEntity(toCanvasItem(toCanvas(i)))
        }
    })

    await table.executeBatch(tableName, batch)
}

exports.updateDiagram = async (context, diagram) => {
    const tableName = getTableName(context)
    const { diagramId } = diagram.diagramInfo
    if (!diagramId) {
        throw new Error('missing parameters: ');
    }

    const diagramInfo = { ...diagram.diagramInfo, accessed: Date.now() }

    const batch = new azure.TableBatch()
    batch.mergeEntity(toDiagramInfoItem(diagramInfo))
    if (diagram.canvases) {
        diagram.canvases.forEach(canvas => batch.insertOrReplaceEntity(toCanvasItem(canvas)))
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
        const diagramInfo = { ...diagram.diagramInfo, accessed: Date.now() }
        batch.insertOrMergeEntity(toDiagramInfoItem(diagramInfo))
        if (diagram.canvases) {
            diagram.canvases.forEach(canvas => batch.insertOrReplaceEntity(toCanvasItem(canvas)))
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
            const diagramInfo = toDiagramInfo(i)
            const id = diagramInfo.diagramId
            diagrams[id] = { ...diagrams[id], diagramInfo: diagramInfo }
        } else if (i.type === 'canvas') {
            const canvas = toCanvas(i)
            const id = canvas.diagramId
            if (diagrams[id] == null) {
                diagrams[id] = { canvases: [canvas] }
            } else {
                const canvases = diagrams[id].canvases ? diagrams[id].canvases : []
                canvases.push(canvas)
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

    return baseTableName + info.token
}

function canvasKey(diagramId, canvasId) {
    return `${diagramId}.${canvasId}`
}

function diagramKey(diagramId) {
    return `${diagramId}`
}

function toUserItem(clientPrincipal, tableId) {
    return {
        RowKey: entGen.String(clientPrincipal.userId),
        PartitionKey: entGen.String(userPartitionKey),

        userId: entGen.String(clientPrincipal.userId),
        tableId: entGen.String(tableId),
        userDetails: entGen.String(clientPrincipal.userDetails),
        provider: entGen.String(clientPrincipal.identityProvider),
    }
}

function toTableUserItem(clientPrincipal) {
    return {
        RowKey: entGen.String(clientPrincipal.userId),
        PartitionKey: entGen.String(partitionKeyName),

        type: entGen.String('user'),
        userId: entGen.String(clientPrincipal.userId),
        name: entGen.String(clientPrincipal.userDetails),
        provider: entGen.String(clientPrincipal.identityProvider),
    }
}

function toCanvasItem(canvas) {
    const { diagramId, canvasId } = canvas
    return {
        RowKey: entGen.String(canvasKey(diagramId, canvasId)),
        PartitionKey: entGen.String(partitionKeyName),

        type: entGen.String('canvas'),
        diagramId: entGen.String(diagramId),
        canvasId: entGen.String(canvasId),
        canvas: entGen.String(JSON.stringify(canvas))
    }
}

function toCanvas(item) {
    const canvas = JSON.parse(item.canvas)
    canvas.etag = item['odata.etag']
    canvas.timestamp = item.Timestamp
    return canvas
}

function toDiagramInfoItem(diagramInfo) {
    const { diagramId, name, accessed } = diagramInfo
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
