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
    if (clientInfo.token === '12345') {
        await createTableIfNotExists(tableName)
    }

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

exports.getDiagram = async (context, clientInfo, diagramId) => {
    const tableName = getTableName(clientInfo)

    var tableQuery = new azure.TableQuery()
        .where('diagramId == ?string?', diagramId);

    const items = await queryEntities(tableName, tableQuery, null)
    context.log(`queried: ${items.length}`)

    const diagram = { diagramData: { diagramId: diagramId }, canvases: [] }

    items.forEach(i => {
        if (i.type === 'diagram') {
            diagram.diagramData.etag = i['odata.etag']
            diagram.diagramData.timestamp = i.Timestamp
            diagram.diagramData.name = i.name
            diagram.diagramData.accessed = i.accessed
        } else if (i.type === 'canvas') {
            const canvasData = JSON.parse(i.canvasData)
            canvasData.etag = i['odata.etag']
            canvasData.timestamp = i.Timestamp
            diagram.canvases.push(canvasData)
        }
    })

    return diagram
}


function getTableName(clientInfo) {
    return baseTableName + clientInfo.token
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