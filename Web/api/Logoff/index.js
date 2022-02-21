var store = require('../shared/Store.js');

module.exports = async function (context, req) {
    try {
        store.verifyApiKey(context)
        const { data, cookies } = await store.logoff(context, req.body)

        context.res = { status: 200, body: data, cookies: cookies };
    } catch (err) {
        context.log.error('error:', err);
        context.res = { status: 400, body: `error: '${err.message}'` };

    }
}