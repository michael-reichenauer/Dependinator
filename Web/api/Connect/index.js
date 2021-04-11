var store = require('../shared/Store.js');

module.exports = async function (context, req) {
    try {
        const connectData = await store.connect(context)

        context.res = { status: 200, body: connectData };
    } catch (err) {
        context.log.error('error:', err);
        context.res = { status: 400, body: `error: '${err.message}'` };

    }
}