var store = require('../shared/Store.js');
var auth = require('../shared/auth.js');
var clientInfo = require('../shared/clientInfo.js');

module.exports = async function (context, req) {
    try {
        const info = clientInfo.getInfo(context, req)

        if (!req.body) {
            throw new Error('Invalid request')
        }

        //context.log('parameters', req.body)
        await store.newDiagram(context, info, req.body)

        context.res = { status: 200, body: "" };
    } catch (err) {
        context.log(`## NewDiagram error: '${err}'`);
        context.res = { status: 400, body: "Error: " + err.message }
    }
}

