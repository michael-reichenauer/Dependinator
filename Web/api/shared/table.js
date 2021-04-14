const azure = require('azure-storage');

const tableService = azure.createTableService();


exports.createTableIfNotExists = (tableName) => {
    return new Promise(function (resolve, reject) {
        tableService.createTableIfNotExists(tableName, function (error, result) {
            if (error) {
                reject(error);
            }
            else {
                resolve(result);
            }
        })
    });
}

exports.executeBatch = (tableName, batch) => {
    return new Promise(function (resolve, reject) {
        tableService.executeBatch(tableName, batch, function (error, result) {
            if (error) {
                reject(error);
            }
            else {
                resolve(result);
            }
        })
    })
}

exports.insertEntity = (tableName, item) => {
    return new Promise(function (resolve, reject) {
        tableService.insertEntity(tableName, item, function (error, result) {
            if (error) {
                reject(error);
            }
            else {
                resolve(result);
            }
        })
    })
}

exports.deleteEntity = (tableName, item) => {
    return new Promise(function (resolve, reject) {
        tableService.deleteEntity(tableName, item, function (error, result) {
            if (error) {
                reject(error);
            }
            else {
                resolve(result);
            }
        })
    })
}

exports.insertOrReplaceEntity = (tableName, item) => {
    return new Promise(function (resolve, reject) {
        tableService.insertOrReplaceEntity(tableName, item, function (error, result) {
            if (error) {
                reject(error);
            }
            else {
                resolve(result);
            }
        })
    })
}


exports.retrieveEntity = (tableName, partitionKey, rowKey) => {
    return new Promise(function (resolve, reject) {
        tableService.retrieveEntity(tableName, partitionKey, rowKey, function (error, result, response) {
            if (error) {
                reject(error);
            }
            else {
                resolve(response.body);
            }
        })
    })
}


exports.queryEntities = (tableName, tableQuery, continuationToken) => {
    return new Promise(function (resolve, reject) {
        tableService.queryEntities(tableName, tableQuery, continuationToken, function (error, result, response) {
            if (error) {
                reject(error);
            }
            else {
                resolve(response.body.value);
            }
        })
    })
}

exports.deleteTableIfExists = (tableName) => {
    return new Promise(function (resolve, reject) {
        tableService.deleteTableIfExists(tableName, function (error, result) {
            if (error) {
                reject(error);
            }
            else {
                resolve();
            }
        })
    })
}