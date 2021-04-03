const azure = require('azure-storage');
var table = require('../shared/table.js');

const entGen = azure.TableUtilities.entityGenerator;
const baseTableName = 'diagrams'
const partitionKeyName = 'dep'

const diagramKeyKey = 'diagram'
const diagramDataKey = 'DiagramData'
//const rootCanvasId = 'root'



exports.newDiagram = async (context, clientInfo, diagram) => {
    const { diagramId, name } = diagram.diagramData
    const canvasData = diagram?.canvases[0]
    if (!diagramId || !name || !canvasData) {
        throw new Error('missing parameters: ');
    }

    const diagramData = { diagramId: diagramId, name: name, accessed: Date.now() }

    const tableName = getTableName(clientInfo)
    if (clientInfo.token === '12345') {
        await table.createTableIfNotExists(tableName)
    }

    const batch = new azure.TableBatch()
    batch.insertEntity(toDiagramDataItem(diagramData))
    batch.insertEntity(toCanvasDataItem(canvasData))

    await table.executeBatch(tableName, batch)

    const entity = await table.retrieveEntity(tableName, partitionKeyName, diagramKey(diagramId))
    return toDiagramInfo(entity)
}

exports.setCanvas = async (context, clientInfo, canvasData) => {
    if (!canvasData) {
        throw new Error('missing parameters');
    }

    const { diagramId } = canvasData
    const diagramData = { diagramId: diagramId, accessed: Date.now() }

    const tableName = getTableName(clientInfo)

    const batch = new azure.TableBatch()
    batch.mergeEntity(toDiagramDataItem(diagramData))
    batch.insertOrReplaceEntity(toCanvasDataItem(canvasData))

    await table.executeBatch(tableName, batch)

    const entity = await table.retrieveEntity(tableName, partitionKeyName, diagramKey(diagramId))
    return toDiagramInfo(entity)
}

exports.getAllDiagramsData = async (context, clientInfo) => {
    const tableName = getTableName(clientInfo)

    var tableQuery = new azure.TableQuery()
        .where('type == ?string?', 'diagram');

    const items = await table.queryEntities(tableName, tableQuery, null)
    context.log(`queried: ${items.length}`)

    return items.map(i => toDiagramInfo(i))
}

exports.getDiagram = async (context, clientInfo, diagramId) => {
    const tableName = getTableName(clientInfo)

    let tableQuery = new azure.TableQuery()
        .where('diagramId == ?string?', diagramId);

    const items = await table.queryEntities(tableName, tableQuery, null)


    const diagram = { diagramData: { diagramId: diagramId }, canvases: [] }

    items.forEach(i => {
        if (i.type === 'diagram') {
            diagram.diagramData = toDiagramInfo(i)
        } else if (i.type === 'canvas') {
            diagram.canvases.push(toCanvasData(i))
        }
    })

    return diagram
}

exports.deleteDiagram = async (context, clientInfo, parameters) => {
    const { diagramId } = parameters
    if (!diagramId) {
        throw new Error('Missing parameter')
    }

    const tableName = getTableName(clientInfo)

    let tableQuery = new azure.TableQuery()
        .where('diagramId == ?string?', diagramId);

    const items = await table.queryEntities(tableName, tableQuery, null)
    context.log(`queried: ${items.length}`)

    const batch = new azure.TableBatch()
    items.forEach(item => batch.deleteEntity(item))

    await table.executeBatch(tableName, batch)
    return
}

exports.updateDiagram = async (context, clientInfo, diagram) => {
    const { diagramId } = diagram.diagramData

    if (!diagramId) {
        throw new Error('missing parameters: ');
    }

    const tableName = getTableName(clientInfo)

    const diagramData = { ...diagram.diagramData, accessed: Date.now() }

    const batch = new azure.TableBatch()
    batch.mergeEntity(toDiagramDataItem(diagramData))
    diagram.canvases?.forEach(canvasData => batch.insertOrReplaceEntity(toCanvasDataItem(canvasData)))

    await table.executeBatch(tableName, batch)

    const entity = await table.retrieveEntity(tableName, partitionKeyName, diagramKey(diagramId))
    return toDiagramInfo(entity)
}


// -----------------------------------------------------------------

function getTableName(clientInfo) {
    return baseTableName + clientInfo.token
}

function canvasKey(diagramId, canvasId) {
    return `${diagramId}.${canvasId}`
}

function diagramKey(diagramId) {
    return `${diagramId}`
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
