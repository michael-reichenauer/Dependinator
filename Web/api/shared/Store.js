const azure = require('azure-storage');

const tableService = azure.createTableService();
const entGen = azure.TableUtilities.entityGenerator;
const baseTableName = 'diagrams'
const partitionKeyName = 'dep'

const diagramKeyKey = 'diagram'
const diagramDataKey = 'DiagramData'
//const rootCanvasId = 'root'



exports.newDiagram = async (context, clientInfo, parameters) => {
    const { diagramId, name, canvasData } = parameters
    if (!diagramId || !name || !canvasData) {
        throw new Error('missing parameters: ');
    }

    const diagramData = { diagramId: diagramId, name: name, accessed: Date.now() }

    const tableName = getTableName(clientInfo)
    await createTableIfNotExists(tableName)

    const batch = new azure.TableBatch()
    batch.insertEntity(makeDiagramData(diagramData))
    batch.insertEntity(makeCanvasData(canvasData))

    await executeBatch(tableName, batch)
}

exports.getAllDiagramsInfos = async (context, clientInfo) => {
    const tableName = getTableName(clientInfo)

    var tableQuery = new azure.TableQuery()
        .where('type == ?string?', 'diagram');

    const items = await queryEntities(tableName, tableQuery, null)
    context.log(`queried: ${items.length}`)

    return items.map(i => ({
        etag: i['odata.etag'],
        timestamp: i.Timestamp,
        diagramId: i.diagramId,
        name: i.name,
        accessed: i.accessed,
    }))
}


function getTableName(clientInfo) {
    return baseTableName + clientInfo.clientPrincipal.userId
}

function makeCanvasData(canvasData) {
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

function makeDiagramData(diagramData) {
    const { diagramId, name, accessed } = diagramData
    return {
        RowKey: entGen.String(diagramKey(diagramId)),
        PartitionKey: entGen.String(partitionKeyName),

        type: entGen.String('diagram'),
        diagramId: entGen.String(diagramId),
        name: entGen.String(name),
        accessed: entGen.Int64(accessed)
    }
}


function canvasKey(diagramId, canvasId) {
    return `${diagramKeyKey}.${diagramId}.${canvasId}`
}

function diagramKey(diagramId) {
    return `${diagramKeyKey}.${diagramId}.${diagramDataKey}`
}

function createTableIfNotExists(tableName) {
    return new Promise(function (resolve, reject) {
        tableService.createTableIfNotExists(tableName, function (error, result) {
            if (error) {
                reject(error);
            }
            else {
                resolve(result);
            }
        })
    });
}

function executeBatch(tableName, batch) {
    return new Promise(function (resolve, reject) {
        tableService.executeBatch(tableName, batch, function (error, result) {
            if (error) {
                reject(error);
            }
            else {
                resolve(result);
            }
        })
    })
}

function insertEntity(tableName, item) {
    return new Promise(function (resolve, reject) {
        tableService.insertEntity(tableName, item, function (error, result) {
            if (error) {
                reject(error);
            }
            else {
                resolve(result);
            }
        })
    })
}

function retrieveEntity(tableName, partitionKey, rowKey) {
    return new Promise(function (resolve, reject) {
        tableService.retrieveEntity(tableName, partitionKey, rowKey, function (error, result, response) {
            if (error) {
                reject(error);
            }
            else {
                resolve(response.body);
            }
        })
    })
}

function queryEntities(tableName, tableQuery, continuationToken) {
    return new Promise(function (resolve, reject) {
        tableService.queryEntities(tableName, tableQuery, continuationToken, function (error, result, response) {
            if (error) {
                reject(error);
            }
            else {
                resolve(response.body.value);
            }
        })
    })
}

function deleteTableIfExists(tableName,) {
    return new Promise(function (resolve, reject) {
        tableService.deleteTableIfExists(tableName, function (error, result) {
            if (error) {
                reject(error);
            }
            else {
                resolve();
            }
        })
    })
}