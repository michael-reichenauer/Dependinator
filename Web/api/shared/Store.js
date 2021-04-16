const azure = require('azure-storage');
var table = require('../shared/table.js');
var clientInfo = require('../shared/clientInfo.js');
var auth = require('../shared/auth.js');

const entGen = azure.TableUtilities.entityGenerator;
const baseTableName = 'diagrams'
const partitionKeyName = 'dep'
const usersTableName = 'users'
const usersPartitionKey = 'users'
const devicesTableName = 'devices'
const devicesPartitionKey = 'devices'
const standardApiKey = '0624bc00-fcf7-4f31-8f3e-3bdc3eba7ade'


exports.verifyApiKey = context => {
    const req = context.req
    const apiKey = req.headers['x-api-key']
    if (apiKey !== standardApiKey) {
        throw new Error('Invalid api request')
    }
}


exports.registerDevice = async (context, body) => {
    const { name, type } = body
    if (!name) {
        throw new Error('No name')
    }
    const now = Date.now()
    const deviceId = makeRandomId()

    const deviceData = {
        deviceId: deviceId,
        userId: null,
        time: now,
        name: name,
        type: type,
    }

    await table.createTableIfNotExists(devicesTableName)
    await table.insertEntity(devicesTableName, toDeviceItem(deviceData))

    return deviceData
}

exports.getDevice = async (context, deviceId) => {
    if (!deviceId) {
        throw new Error('No id')
    }

    const item = await table.retrieveEntity(devicesTableName, deviceId)
    return toDeviceItem(item)
}

exports.connectCustom = async (context, data) => {
    const { connectorId, deviceId } = data
    if (!connectorId || !deviceId) {
        throw new Error('Missing parameters')
    }

    const connectorEntity = await table.retrieveEntity(devicesTableName, devicePartitionKey, connectorId)
    const deviceEntity = await table.retrieveEntity(devicesTableName, devicePartitionKey, connectorId)
    const connector = toDevice(connectorEntity)
    const device = toDevice(deviceEntity)

    if (!connector.userId) {
        // Connector has not yet a user
        if (device.userId) {
            // Device has a user, lets reuse that device user id and update connector device for future
            connector.userId = device.userId
            await table.insertOrReplaceEntity(devicesTableName, toDeviceItem(connector))
        } else {
            // No known user id, lets create a new user id
            await table.createTableIfNotExists(usersTableName)
            connector.userId = makeRandomId()
            const connectorUser = {
                userId: connector.userId,
                tableId: makeRandomId(),
                userDetails: 'custom',
                provider: 'custom'
            }

            await table.insertEntity(usersTableName, toUserItem(connectorUser, tableId))
        }
    }

    const connectorUserEntity = await table.retrieveEntity(usersTableName, userPartitionKey, connector.userId)
    const connectorUser = toUser(connectorUserEntity)

    device.userId = connectorUser.userId
    await table.insertOrReplaceEntity(devicesTableName, toDeviceItem(device))


    // // Create a new random diagrams table id to be used for the user
    // const tableId = makeRandomId()
    // const tableName = baseTableName + tableId

    // // Create the actual diagram table
    // await table.createTableIfNotExists(tableName)
    // await table.insertOrReplaceEntity(tableName, toTableUserItem(clientPrincipal))

    // // Create a user in the users table
    // await table.createTableIfNotExists(usersTableName)
    // await table.insertOrReplaceEntity(usersTableName, toUserItem(clientPrincipal, tableId))

    // return { token: tableId, provider: clientPrincipal.identityProvider, details: clientPrincipal.userDetails }
}


exports.connect = async (context) => {
    const req = context.req
    const clientPrincipal = auth.getClientPrincipal(req)

    const userId = clientPrincipal.userId
    if (!userId) {
        throw new Error('No user id')
    }

    try {
        const entity = await table.retrieveEntity(usersTableName, usersPartitionKey, userId)
        if (entity.tableId) {
            // context.log('got user', userId, entity)
            const tableName = baseTableName + entity.tableId
            await table.createTableIfNotExists(tableName)
            await table.insertOrReplaceEntity(tableName, toTableUserItem(clientPrincipal))
            return { token: entity.tableId, provider: clientPrincipal.identityProvider, details: clientPrincipal.userDetails }
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

    return { token: tableId, provider: clientPrincipal.identityProvider, details: clientPrincipal.userDetails }
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
        await table.deleteEntity(usersTableName, toUserItem(clientPrincipal, ''))
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

    const now = Date.now()
    const diagramInfo = { diagramId: diagramId, name: name, accessed: now, written: now }

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
    const now = Date.now()
    const diagramInfo = { diagramId: diagramId, accessed: now, written: now }

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

    const now = Date.now()
    const diagramInfo = { ...diagram.diagramInfo, accessed: now, written: now }

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
    const now = Date.now()
    const batch = new azure.TableBatch()
    diagrams.forEach(diagram => {
        const diagramInfo = { ...diagram.diagramInfo, accessed: now, written: now }
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
    if (!info.token || info.token == null || info.token === 'null') {
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


function toDeviceItem(data) {
    return {
        RowKey: entGen.String(data.deviceId),
        PartitionKey: entGen.String(devicesPartitionKey),

        userId: entGen.String(data.userId),
        time: entGen.Int64(data.time),
        name: entGen.String(data.name),
        type: entGen.String(data.type),
    }
}


function toDevice(item) {
    return {
        deviceId: item.RowKey,
        userId: item.userId,
        time: item.time,
        name: item.name,
        type: item.type
    }
}


function toUserItem(clientPrincipal, tableId) {
    return {
        RowKey: entGen.String(clientPrincipal.userId),
        PartitionKey: entGen.String(usersPartitionKey),

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
    const { diagramId, name, accessed, written } = diagramInfo
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
    if (written != null) {
        item.written = entGen.Int64(written)
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
        written: item.written,
    }
}
