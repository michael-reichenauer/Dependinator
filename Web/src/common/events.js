export function getCommonEvent(event) {
    // check for iPad, Android touch events
    if (typeof event.originalEvent !== "undefined") {
        if (event.originalEvent.touches && event.originalEvent.touches.length) {
            return event.originalEvent.touches[0]
        } else if (event.originalEvent.changedTouches && event.originalEvent.changedTouches.length) {
            return event.originalEvent.changedTouches[0]
        }
    }
    return event
}
