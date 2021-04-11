var store = require('../shared/Store.js');

module.exports = async function (context, req) {
    try {
        const diagrams = await store.downloadAllDiagrams(context)

        context.res = { status: 200, body: diagrams };
    } catch (err) {
        context.log.error('error:', err);
        context.res = { status: 400, body: `error: '${err.message}'` };
    }
}