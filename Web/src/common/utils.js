
const humanizeDuration = require("humanize-duration");

export const durationString = duration => {
    return humanizeDuration(duration)
}

export const random = (min, max) => {
    min = Math.ceil(min);
    max = Math.floor(max) + 1;
    return Math.floor(Math.random() * (max - min) + min);
}


export const distance = (x1, y1, x2, y2) => {
    return Math.sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2))
}

export async function delay(time) {
    return new Promise(res => {
        setTimeout(res, time)
    })
}