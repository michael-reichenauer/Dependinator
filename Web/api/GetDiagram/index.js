var store = require('../shared/Store.js');
//var auth = require('../shared/auth.js');
const azure = require('azure-storage');
var table = require('../shared/table.js');
var clientInfo = require('../shared/clientInfo.js');

const entGen = azure.TableUtilities.entityGenerator;
const baseTableName = 'diagrams'
const partitionKeyName = 'dep'

module.exports = async function (context, req) {
    try {
        const { diagramId } = req.query
        if (!diagramId) {
            throw new Error('Invalid args')
        }

        //  const clientPrincipal = auth.getClientPrincipal(req)

        //context.log('parameters', req.body)
        //const diagram = await store.getDiagram(context, diagramId)
        const diagram = await getDiagram(context, diagramId)
        context.log('diagram', diagram)

        context.res = { status: 200, body: diagram };
        //context.res = { status: 200, body: 'diagram' };
    } catch (err) {
        context.log.error('error:', err);
        context.res = { status: 400, body: `error: '${err.message}', ${err.stack}` };
    }
}

const getDiagram = async (context, diagramId) => {
    const tableName = getTableName(context)
    //if (clientInfo.token === '12345') {
    await table.createTableIfNotExists(tableName)
    // }


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

function getTableName(context) {
    const info = clientInfo.getInfo(context)
    return baseTableName + info.token
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
