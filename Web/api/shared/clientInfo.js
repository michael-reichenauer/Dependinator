var auth = require('../shared/auth.js');

exports.getInfo = (context, req) => {
    const clientPrincipal = auth.getClientPrincipal(req)

    if (!req.headers['xtoken']) {
        throw new Error('Invalid token')
    }

    const userAgent = req.headers["user-agent"];
    const forwardedHost = req.headers["x-forwarded-host"];
    const forwardedFor = req.headers["x-forwarded-for"];
    const clientIp = req.headers["client-ip"];
    const host = req.headers["host"];

    return {
        token: req.headers['xtoken'],
        clientPrincipal: clientPrincipal,
        userAgent: userAgent,
        clientIp: clientIp,
        forwardedHost: forwardedHost,
        forwardedFor: forwardedFor,
        host: host,
    }
}