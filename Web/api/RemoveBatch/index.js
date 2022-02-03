var store = require('../shared/Store.js');


module.exports = async function (context, req) {
    try {
        store.verifyApiKey(context)
        store.verifyToken(context)

        const entities = await store.removeBatch(context, req.body)

        context.res = { status: 200, body: entities };
    } catch (err) {
        context.log.error('error:', err);
        context.res = { status: 400, body: `error: '${err.message}'` };
    }
}