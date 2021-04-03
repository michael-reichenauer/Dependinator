var store = require('../shared/Store.js');

module.exports = async function (context, req) {
    const diagrams = await store.downloadAllDiagrams(context)

    context.res = { status: 200, body: diagrams };
}