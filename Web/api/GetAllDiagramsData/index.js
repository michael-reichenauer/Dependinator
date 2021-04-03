var store = require('../shared/Store.js');

module.exports = async function (context, req) {
    const diagramsData = await store.getAllDiagramsData(context)

    context.res = { status: 200, body: diagramsData };
}