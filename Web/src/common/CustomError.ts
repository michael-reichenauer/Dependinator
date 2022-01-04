export class CustomError extends Error {
  constructor(message?: string | Error, nested?: Error) {
    if (message instanceof Error) {
      super();
      this.name = this.constructor.name;

      if (this.stack) {
        if (message.stack) {
          this.stack = this.stack + "\ncaused by: \n" + message.stack;
        }
      } else if (message.stack) {
        this.stack = message.stack;
      }
    } else {
      super(message);
      this.name = this.constructor.name;
    }

    if (nested?.stack) {
      if (this.stack) {
        this.stack = this.stack + "\ncaused by: \n" + nested.stack;
      } else {
        this.stack = nested.stack;
      }
    }
  }
}
