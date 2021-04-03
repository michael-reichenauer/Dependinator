var store = require('../shared/Store.js');
var clientInfo = require('../shared/clientInfo.js');

module.exports = async function (context, req) {
    const info = clientInfo.getInfo(context, req)

    //context.log('parameters', req.body)
    const diagrams = await store.downloadAllDiagrams(context, info)

    context.res = { status: 200, body: diagrams };
}