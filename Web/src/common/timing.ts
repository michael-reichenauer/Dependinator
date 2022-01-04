// Utility function to log code timing
export default function timing(label?: string, isStart: boolean = false) {
  if (label === undefined) {
    const stack = new Error().stack;
    const caller = stack?.split("\n")[2].trim().split(" ");

    if (caller?.length === 3) {
      label = caller[1];
    } else {
      label = "()=>";
    }

    return new Timing(label, isStart);
  }

  return new Timing(label, isStart);
}

class Timing {
  label?: string;
  start: number;
  current: number;

  constructor(label: string, isStart: boolean) {
    this.label = label;
    this.start = performance.now();
    this.current = this.start;
    if (isStart) {
      console.log(`${this.label}:`, "Start");
    }
  }

  log(...properties: any[]) {
    const now = performance.now();
    let ci: number = now - this.current;
    let ti: number = now - this.start;
    let currentInterval = ci > 5 ? ci.toFixed(0) : ci.toFixed(1);
    let totalInterval = ti > 5 ? ti.toFixed(0) : ti.toFixed(1);

    if (this.label != null) {
      console.log(
        `${this.label}:`,
        ...properties,
        `${currentInterval} ms (${totalInterval} ms)`
      );
    } else {
      console.log(...properties, `${currentInterval} ms (${totalInterval} m)`);
    }
    this.current = now;
    return this;
  }
}
