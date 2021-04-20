

exports.getClientPrincipal = req => {
    const header = req.headers["x-ms-client-principal"];
    let clientPrincipal

    if (header) {
        const encoded = Buffer.from(header, "base64");
        const decoded = encoded.toString("ascii");
        clientPrincipal = JSON.parse(decoded)
    }

    if (!clientPrincipal) {
        throw new Error('No user')
    }
    return clientPrincipal
}