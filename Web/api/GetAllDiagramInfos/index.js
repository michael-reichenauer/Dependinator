var store = require('../shared/Store.js');
var clientInfo = require('../shared/clientInfo.js');

module.exports = async function (context, req) {
    try {
        const info = clientInfo.getInfo(context, req)

        //context.log('parameters', req.body)
        const diagramInfos = await store.getAllDiagramsInfos(context, info)

        context.res = { status: 200, body: diagramInfos };
    } catch (err) {
        context.log(`## GetAllDiagramInfos error: '${err}'`);
        context.res = { status: 400, body: "Error: " + err.message }
    }
}