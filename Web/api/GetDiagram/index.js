//var store = require('../shared/Store.js');
var auth = require('../shared/auth.js');

module.exports = async function (context, req) {
    try {
        const { diagramId } = req.query
        if (!diagramId) {
            throw new Error('Invalid args')
        }

        const clientPrincipal = auth.getClientPrincipal(req)

        //context.log('parameters', req.body)
        //const diagram = await store.getDiagram(context, diagramId)

        //context.res = { status: 200, body: diagram };
        context.res = { status: 200, body: 'diagram' };
    } catch (err) {
        context.log.error('error:', err);
        context.res = { status: 400, body: `error: '${err.message}', ${err.stack}` };
    }
}