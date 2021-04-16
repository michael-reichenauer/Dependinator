const platform = require('platform');

console.log('platform', platform)

export const platformInfo = {
    name: platform.name,
    os: platform.os.family,
    product: platform.product
}