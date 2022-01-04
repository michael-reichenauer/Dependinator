// The Result type, which can be either the expected value or an Error value. The caller can use
import assert from "assert";

// Narrowing using either the isError(result) function or the instanceof Error operator
type Result<T, E = Error> = T | E;
export default Result;

type ClassType<T> = { new (...args: any[]): T };

// isError(result) returns true if result is an Error and can be used in narrowing by the caller
// isError(result, ErrorType), with specified error type returns true if result is an
// the specified error type and can be used in narrowing by the caller
export function isError<T, E>(
  result: Result<T, E>,
  errorClass?: ClassType<E>
): result is E {
  if (errorClass === undefined) {
    // No specified error type, assuming Error
    return result instanceof Error;
  }

  return result instanceof errorClass;
}

// orDefault() returns the result or the default value if result is an Error
export function orDefault<T, E>(result: Result<T, E>, defaultValue: T): T {
  if (result instanceof Error) {
    return defaultValue;
  }

  return result as T;
}

export function expectValue<T, E>(result: Result<T, E>): T {
  if (isError(result)) {
    assert.fail(`Expected value, but was error ${result}`);
  }

  return result;
}
