const azure = require('azure-storage');
const crypto = require("crypto")
const bcrypt = require("bcryptjs")
var table = require('../shared/table.js');
var clientInfo = require('../shared/clientInfo.js');
var auth = require('../shared/auth.js');
const { brotliCompress } = require('zlib');

const entGen = azure.TableUtilities.entityGenerator;
const baseTableName = 'diagrams'
const partitionKeyName = 'dep'
const usersTableName = 'users'
const userPartitionKey = 'users'
const dataPartitionKey = 'data'
const standardApiKey = '0624bc00-fcf7-4f31-8f3e-3bdc3eba7ade'
const saltRounds = 10

const invalidUserError = 'Invalid user'
const invalidTokenError = 'Invalid token'
const invalidRequestError = 'Invalid request'

exports.verifyApiKey = context => {
    const req = context.req
    const apiKey = req.headers['x-api-key']
    if (apiKey !== standardApiKey) {
        throw new Error(invalidRequestError)
    }
}

exports.verifyToken = context => {
    const info = clientInfo.getInfo(context)
    if (!info.token || info.token == null || info.token === 'null') {
        throw new Error(invalidTokenError)
    }
}


exports.createUser = async (context, data) => {
    const { username, password } = data
    if (!username || !password) {
        throw new Error('Missing parameter')
    }
    const userDetails = username
    const userId = toUserId(username)

    // Hash the password using bcrypt
    const salt = await bcryptGenSalt(saltRounds)
    const passwordHash = await bcryptHash(password, salt)

    const user = {
        userId: userId,
        passwordHash: passwordHash,
        userDetails: userDetails,
        identityProvider: 'Custom',
    }
    const tableId = makeRandomId()

    await table.createTableIfNotExists(usersTableName)
    await table.insertEntity(usersTableName, toUserItem(user, tableId))
}

exports.connectUser = async (context, data) => {
    context.log('connect', context, data)
    const { username, password } = data
    if (!username || !password) {
        throw new Error(invalidUserError)
    }

    const userDetails = username
    const userId = toUserId(username)
    const user = {
        userId: userId,
        userDetails: userDetails,
        identityProvider: 'Custom',
    }
    context.log('user', user)

    const entity = await table.retrieveEntity(usersTableName, userPartitionKey, userId)
    if (entity.provider !== 'Custom') {
        // Only support custom identity provider users, other users use connect()
        throw new Error(invalidUserError)
    }
    context.log('entity', entity)

    const isMatch = await bcryptCompare(password, entity.passwordHash)
    if (!isMatch) {
        throw new Error(invalidUserError)
    }

    context.log('isMatch', isMatch)

    if (!entity.tableId) {
        throw new Error(invalidUserError)
    }

    context.log('tableId', entity.tableId)

    // context.log('got user', userId, entity)
    const tableName = baseTableName + entity.tableId
    await table.createTableIfNotExists(tableName)
    return { token: entity.tableId, provider: user.identityProvider, details: user.userDetails }
}



exports.tryReadBatch = async (context, body) => {
    const tableName = getTableName(context)
    context.log('body', body, tableName)
    keys = body.map(query => query.key)
    context.log('Keys', keys)
    if (keys.length === 0) {
        return []
    }


    const rkq = ' (RowKey == ?string?' + ' || RowKey == ?string?'.repeat(keys.length - 1) + ')'

    let tableQuery = new azure.TableQuery()
        .where('PartitionKey == ?string? && ' + rkq,
            dataPartitionKey, ...keys);

    const items = await table.queryEntities(tableName, tableQuery, null)
    context.log(`queried: ${items.length}`)
    context.log('table rsp, resp', items)

    const responses = items.map(item => toEntity(item))

    return responses
}


exports.writeBatch = async (context, body) => {
    const tableName = getTableName(context)
    context.log('body', body, tableName)

    const entityItems = body.map(entity => toEntityItem(entity))

    const batch = new azure.TableBatch()
    entityItems.forEach(entity => batch.insertOrReplaceEntity(entity))

    await table.executeBatch(tableName, batch)

    return {}
}


exports.connect = async (context) => {
    const req = context.req
    const user = auth.getClientPrincipal(req)

    const userId = user.userId
    if (!userId) {
        throw new Error('No user id')
    }

    try {
        const entity = await table.retrieveEntity(usersTableName, userPartitionKey, userId)
        if (entity.tableId) {
            // context.log('got user', userId, entity)
            const tableName = baseTableName + entity.tableId
            await table.createTableIfNotExists(tableName)
            await table.insertOrReplaceEntity(tableName, toTableUserItem(user))
            return { token: entity.tableId, provider: user.identityProvider, details: user.userDetails }
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
    await table.insertOrReplaceEntity(tableName, toTableUserItem(user))

    // Create a user in the users table
    await table.createTableIfNotExists(usersTableName)
    await table.insertOrReplaceEntity(usersTableName, toUserItem(user, tableId))

    return { token: tableId, provider: user.identityProvider, details: user.userDetails }
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

const bcryptGenSalt = (saltRounds) => {
    return new Promise(function (resolve, reject) {
        bcrypt.genSalt(saltRounds, function (err, salt) {
            if (err) {
                reject(err)
            } else {
                resolve(salt)
            }
        })
    })
}


const bcryptHash = (password, salt) => {
    return new Promise(function (resolve, reject) {
        bcrypt.hash(password, salt, function (err, hash) {
            if (err) {
                reject(err)
            } else {
                resolve(hash)
            }
        })
    })
}

const bcryptCompare = (password, hash) => {
    return new Promise(function (resolve, reject) {
        bcrypt.compare(password, hash, function (err, isMatch) {
            if (err) {
                reject(err)
            } else {
                resolve(isMatch)
            }
        })
    })
}


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

function toUserItem(user, tableId) {
    return {
        RowKey: entGen.String(user.userId),
        PartitionKey: entGen.String(userPartitionKey),

        userId: entGen.String(user.userId),
        tableId: entGen.String(tableId),
        userDetails: entGen.String(user.userDetails),
        provider: entGen.String(user.identityProvider),
        passwordHash: entGen.String(user.passwordHash)
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

function toEntityItem(entity) {
    const { key, stamp, value } = entity

    const item = {
        RowKey: entGen.String(key),
        PartitionKey: entGen.String(dataPartitionKey),

        stamp: entGen.String(stamp),
        value: entGen.String(JSON.stringify(value)),
    }

    return item
}

toEntity = (item) => {
    return { key: item.RowKey, stamp: item.stamp, value: JSON.parse(item.value ?? '{}') }
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

function sha256(message) {
    return crypto.createHash("sha256")
        .update(message)
        .digest("hex");
}

function toUserId(name) {
    return sha256(name.toLowerCase()).substr(0, 32)
}