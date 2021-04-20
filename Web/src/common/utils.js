
// Some handy utility functions

const humanizeDuration = require("humanize-duration");


// Returns a duration as a nice human readable string
export const durationString = duration => {
    return humanizeDuration(duration)
}

// Returns a random number between min and max
export const random = (min, max) => {
    min = Math.ceil(min);
    max = Math.floor(max) + 1;
    return Math.floor(Math.random() * (max - min) + min);
}

// Returns the distance between 2 points
export const distance = (x1, y1, x2, y2) => {
    return Math.sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2))
}

// Async sleep/delay
export async function delay(time) {
    return new Promise(res => {
        setTimeout(res, time)
    })
}

// Returns the sha 256 hash of the string
export async function sha256Hash(text) {
    // encode as UTF-8
    const msgBuffer = new TextEncoder().encode(text);

    // hash the message
    const hashBuffer = await crypto.subtle.digest('SHA-256', msgBuffer);

    // convert ArrayBuffer to Array
    const hashArray = Array.from(new Uint8Array(hashBuffer));

    // convert bytes to hex string                  
    const hashHex = hashArray.map(b => b.toString(16).padStart(2, '0')).join('');
    return hashHex;
}

// Returns if build is developer mode (running on local machine)
export const isDeveloperMode = !process.env.NODE_ENV || process.env.NODE_ENV === 'development';

