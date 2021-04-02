const azure = require('azure-storage');

const tableService = azure.createTableService();
const baseTableName = 'diagrams'
const entGen = azure.TableUtilities.entityGenerator;
const partitionKeyName = 'dep'

const diagramKeyKey = 'diagram'
const diagramDataKey = 'DiagramData'
//const rootCanvasId = 'root'



exports.newDiagram = async (context, info, parameters) => {
    const { diagramId, name, canvasData } = parameters
    if (!diagramId || !name || !canvasData) {
        throw new Error('missing parameters: ');
    }

    const diagramData = { diagramId: diagramId, name: name, accessed: Date.now() }

    const tableName = baseTableName + info.clientPrincipal.userId
    await createTableIfNotExists(tableName)

    const batch = new azure.TableBatch()
    batch.insertEntity(makeDiagramData(diagramData))
    batch.insertEntity(makeCanvasData(canvasData))

    await executeBatch(tableName, batch)
}


function makeCanvasData(canvasData) {
    const { diagramId, canvasId } = canvasData
    return {
        RowKey: entGen.String(canvasKey(diagramId, canvasId)),
        PartitionKey: entGen.String(partitionKeyName),

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

        diagramId: entGen.String(diagramId),
        name: entGen.String(name),
        accessed: entGen.String(accessed)
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