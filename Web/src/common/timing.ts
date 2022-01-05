export default function timing() {
  const t = new Timing();
  return () => t.toString();
}

class Timing {
  start: number;
  current: number;

  constructor() {
    this.start = performance.now();
    this.current = this.start;
  }

  toString() {
    const now = performance.now();
    let ci: number = now - this.current;
    let ti: number = now - this.start;
    let currentInterval = ci > 5 ? ci.toFixed(0) : ci.toFixed(1);
    let totalInterval = ti > 5 ? ti.toFixed(0) : ti.toFixed(1);
    this.current = now;
    return `${currentInterval} ms (${totalInterval} m)`;
  }
}
