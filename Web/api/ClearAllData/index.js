var store = require('../shared/Store.js');

module.exports = async function (context, req) {
    try {
        await store.clearAllData(context)

        context.res = { status: 200, body: "" };
    } catch (err) {
        context.log.error('error:', err);
        context.res = { status: 400, body: `error: '${err.message}', ${err.stack}` };
    }
}