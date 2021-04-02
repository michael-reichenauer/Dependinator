exports.getClientPrincipal = req => {

    const header = req.headers["x-ms-client-principal"];
    let clientPrincipal

    if (header) {
        const encoded = Buffer.from(header, "base64");
        const decoded = encoded.toString("ascii");
        clientPrincipal = JSON.parse(decoded)
    }

    if (!clientPrincipal) {
        clientPrincipal = {
            "identityProvider": "local",
            "userId": 'local',
            "userDetails": 'local',
            "userRoles": ["anonymous", "authenticated"]
        }
    }
    return clientPrincipal
}