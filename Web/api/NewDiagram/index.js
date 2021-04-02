var store = require('../shared/Store.js');
var clientInfo = require('../shared/clientInfo.js');

module.exports = async function (context, req) {
    const info = clientInfo.getInfo(context, req)

    if (!req.body) {
        throw new Error('Invalid request')
    }

    //context.log('parameters', req.body)
    await store.newDiagram(context, info, req.body)

    context.res = { status: 200, body: "" };
}

