var store = require('../shared/Store.js');

module.exports = async function (context, req) {
    const diagramInfo = await store.setCanvas(context, req.body)

    context.res = { status: 200, body: diagramInfo };
}