var store = require('../shared/Store.js');

module.exports = async function (context, req) {
    await store.deleteDiagram(context, req.body)

    context.res = { status: 200, body: "" };
}