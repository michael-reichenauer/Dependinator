exports.getInfo = (context) => {
    const req = context.req

    const userAgent = req.headers["user-agent"];
    const forwardedHost = req.headers["x-forwarded-host"];
    const forwardedFor = req.headers["x-forwarded-for"];
    const clientIp = req.headers["client-ip"];
    const host = req.headers["host"];
    const cookie = req.headers["cookie"]

    return {
        userAgent: userAgent,
        clientIp: clientIp,
        forwardedHost: forwardedHost,
        forwardedFor: forwardedFor,
        host: host,
        cookie: cookie
    }
}