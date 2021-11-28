var store = require('../shared/Store.js');

module.exports = async function (context, req) {
    try {
        console.log('api: checking')
        store.verifyApiKey(context)
        console.log('api: verified')

        context.res = { status: 200, body: '' };
        console.log('api: result')
    } catch (err) {
        context.log.error('error:', err);
        context.res = { status: 400, body: `error: '${err.message}'` };
    }
}