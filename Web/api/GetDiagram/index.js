var store = require('../shared/Store.js');

module.exports = async function (context, req) {
    const { diagramId } = req.query
    if (!diagramId) {
        throw new Error('Invalid args')
    }

    //context.log('parameters', req.body)
    const diagram = await store.getDiagram(context, diagramId)

    context.res = { status: 200, body: diagram };
}