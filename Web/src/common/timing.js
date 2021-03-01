export class Timing {
    constructor(label, isStart) {
        this.label = label
        this.start = Date.now()
        this.current = this.start
        if (isStart) {
            console.log(`${this.label}:`, 'Start')
        }
    }

    log(...properties) {
        const now = Date.now()

        if (this.label != null) {
            console.log(`${this.label}:`, ...properties, `${now - this.current} ms (${now - this.start} ms)`)
        } else {
            console.log(...properties, `${now - this.current} ms (${now - this.start} m)`)
        }
        this.current = now
        return this
    }
}

export const timing = (label, isStart) => {
    return new Timing(label, isStart)
}