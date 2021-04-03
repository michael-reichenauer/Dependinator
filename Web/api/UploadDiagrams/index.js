var store = require('../shared/Store.js');
var clientInfo = require('../shared/clientInfo.js');

module.exports = async function (context, req) {
    const info = clientInfo.getInfo(context, req)

    await store.uploadDiagrams(context, info, req.body)
    context.res = { status: 200, body: "" };
}