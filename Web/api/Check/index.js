module.exports = async function (context, req) {
    try {
        context.log('JavaScript HTTP trigger function processed a request.');

    } catch (err) {
        context.log.error('error:', err);
        context.res = { status: 400, body: `error: '${err.message}', ${err.stack}` };
    }
}