var store = require('../shared/Store.js');


module.exports = async function (context, req) {
    try {
        store.verifyApiKey(context)
        const { diagramId } = req.query
        if (!diagramId) {
            throw new Error('Invalid args')
        }

        const diagram = await store.getDiagram(context, diagramId)

        context.res = { status: 200, body: diagram };
    } catch (err) {
        context.log.error('error:', err);
        if (err.message === 'NOTFOUND') {
            context.res = { status: 404, body: `error: 'Not found'` };
        } else {
            context.res = { status: 400, body: `error: '${err.message}', ${err.stack}` };
        }
    }
}
