var store = require('../shared/Store.js');

module.exports = async function (context, req) {
    try {
        const diagramInfo = await store.setCanvas(context, req.body)

        context.res = { status: 200, body: diagramInfo };
    } catch (err) {
        context.log.error('error:', err);
        context.res = { status: 400, body: `error: '${err.message}', ${err.stack}` };
    }
}