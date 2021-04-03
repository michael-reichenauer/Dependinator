//var store = require('../shared/Store.js');
//var auth = require('../shared/auth.js');
var table = require('../shared/table.js');
var clientInfo = require('../shared/clientInfo.js');

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