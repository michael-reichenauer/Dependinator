const azure = require('azure-storage');
const crypto = require("crypto")
const bcrypt = require("bcryptjs")
var table = require('../shared/table.js');
var clientInfo = require('../shared/clientInfo.js');
var auth = require('../shared/auth.js');

const entGen = azure.TableUtilities.entityGenerator;
const baseTableName = 'diagrams'
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
    const { user, wDek } = data
    if (!user || !user.username || !user.password || !wDek) {
        throw new Error('Missing parameter')
    }
    const { username, password } = user

    const userId = toUserId(username)

    // Hash the password using bcrypt
    const salt = await bcryptGenSalt(saltRounds)
    const passwordHash = await bcryptHash(password, salt)

    const tableId = makeRandomId()

    await table.createTableIfNotExists(usersTableName)
    await table.insertEntity(usersTableName, toUserItem(userId, passwordHash, wDek, tableId))
}

exports.connectUser = async (context, data) => {
    //context.log('connectUser', context, data)
    const { username, password } = data
    if (!username || !password) {
        throw new Error(invalidUserError)
    }

    const userId = toUserId(username)

    const entity = await table.retrieveEntity(usersTableName, userPartitionKey, userId)
    // context.log('entity', entity)

    const isMatch = await bcryptCompare(password, entity.passwordHash)
    if (!isMatch) {
        throw new Error(invalidUserError)
    }

    if (!entity.tableId) {
        throw new Error(invalidUserError)
    }

    // context.log('got user', userId, entity)
    const tableName = baseTableName + entity.tableId
    await table.createTableIfNotExists(tableName)
    return { token: entity.tableId, wDek: entity.wDek }
}



exports.tryReadBatch = async (context, body) => {
    const tableName = getTableName(context)
    // context.log('body', body, tableName)
    const queries = body
    keys = queries.map(query => query.key)
    if (keys.length === 0) {
        return []
    }

    // Read all requested rows
    const rkq = ' (RowKey == ?string?' + ' || RowKey == ?string?'.repeat(keys.length - 1) + ')'
    let tableQuery = new azure.TableQuery()
        .where('PartitionKey == ?string? && ' + rkq,
            dataPartitionKey, ...keys);
    const items = await table.queryEntities(tableName, tableQuery, null)

    // Replace not modified values with status=notModified 
    const entities = items.map(item => toEntity(item))
    const responses = entities.map(entity => {
        if (queries.find(query => query.key === entity.key && query.IfNoneMatch === entity.etag)) {
            return { key: entity.key, etag: entity.etag, status: 'notModified' }
        }
        return entity
    })

    return responses
}


exports.writeBatch = async (context, body) => {
    const entities = body
    const tableName = getTableName(context)
    // context.log('entities:', entities, tableName)

    // Write all entities
    const entityItems = entities.map(entity => toEntityItem(entity))
    const batch = new azure.TableBatch()
    entityItems.forEach(entity => batch.insertOrReplaceEntity(entity))

    // Extract etags for written entities
    const tableResponses = await table.executeBatch(tableName, batch)
    const responses = tableResponses.map((rsp, i) => {
        if (!rsp.response || !rsp.response.isSuccessful) {
            return {
                key: entities[i].key,
                status: 'error'
            }
        }

        return {
            key: entities[i].key,
            etag: rsp.entity['.metadata'].etag
        }
    })

    return responses
}

exports.removeBatch = async (context, body) => {
    const keys = body
    const tableName = getTableName(context)
    // context.log('keys:', keys, tableName)

    const entityItems = keys.map(key => toDeleteEntityItem(key))
    const batch = new azure.TableBatch()
    entityItems.forEach(entity => batch.deleteEntity(entity))

    await table.executeBatch(tableName, batch)

    return ''
}

// exports.clearAllData = async (context) => {
//     const req = context.req
//     const clientPrincipal = auth.getClientPrincipal(req)

//     const userId = clientPrincipal.userId
//     if (!userId) {
//         throw new Error('No user id')
//     }

//     const tableName = getTableName(context)
//     await table.deleteTableIfExists(tableName)

//     try {
//         await table.deleteEntity(usersTableName, toUserItem(clientPrincipal, ''))
//     } catch (error) {
//         // No user, so done
//     }
// }



// // // -----------------------------------------------------------------

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

// async function delay(time) {
//     return new Promise(res => {
//         setTimeout(res, time)
//     })
// }


function getTableName(context) {
    const info = clientInfo.getInfo(context)
    if (!info.token || info.token == null || info.token === 'null') {
        throw new Error('Invalid token')
    }

    return baseTableName + info.token
}


function toUserItem(userId, passwordHash, wDek, tableId) {
    return {
        RowKey: entGen.String(userId),
        PartitionKey: entGen.String(userPartitionKey),

        passwordHash: entGen.String(passwordHash),
        tableId: entGen.String(tableId),
        wDek: entGen.String(wDek),
    }
}

function toEntityItem(entity) {
    const { key, value } = entity

    const item = {
        RowKey: entGen.String(key),
        PartitionKey: entGen.String(dataPartitionKey),

        value: entGen.String(JSON.stringify(value)),
    }

    return item
}

function toDeleteEntityItem(key) {
    const item = {
        RowKey: entGen.String(key),
        PartitionKey: entGen.String(dataPartitionKey),
    }

    return item
}

function toEntity(item) {
    let valueText = '{}'
    if (item.value) {
        valueText = item.value
    }
    const value = JSON.parse(valueText)
    return { key: item.RowKey, etag: item['odata.etag'], value: value }
}


function sha256(message) {
    return crypto.createHash("sha256")
        .update(message)
        .digest("hex");
}

function toUserId(name) {
    return sha256(name.toLowerCase()).substr(0, 32)
}