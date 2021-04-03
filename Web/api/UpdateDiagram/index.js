var store = require('../shared/Store.js');

module.exports = async function (context, req) {
    const diagramInfo = await store.updateDiagram(context, info, req.body)

    context.res = { status: 200, body: diagramInfo };
}