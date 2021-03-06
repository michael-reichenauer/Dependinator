export class Timing {
    constructor(label, isStart) {
        this.label = label
        this.start = performance.now()
        this.current = this.start
        if (isStart) {
            console.log(`${this.label}:`, 'Start')
        }
    }

    log(...properties) {
        const now = performance.now()
        let currentInterval = now - this.current
        let totalInterval = now - this.start
        currentInterval = currentInterval > 5 ? currentInterval.toFixed(0) : currentInterval.toFixed(1)
        totalInterval = totalInterval > 5 ? totalInterval.toFixed(0) : totalInterval.toFixed(1)

        if (this.label != null) {
            console.log(`${this.label}:`, ...properties, `${currentInterval} ms (${totalInterval} ms)`)
        } else {
            console.log(...properties, `${currentInterval} ms (${totalInterval} m)`)
        }
        this.current = now
        return this
    }
}

export const timing = (label, isStart) => {
    if (label === undefined) {
        const stack = new Error().stack
        const caller = stack.split('\n')[2].trim().split(' ')[1];
        label = caller
    }
    return new Timing(label, isStart)
}