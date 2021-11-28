import assert from "assert";
import Result, { isError, orDefault } from "./Result";

// Function, which returns a Result<T>, which is either ok result value (type string) or an Error type result
// Since return value is a Result<T, E=Error> type
function getValue(key: string): Result<string> {
  if (key !== "ok") {
    return new RangeError("invalid");
  }
  return "ok";
}

describe("Test Result<T> type using isError(value) type guard for narrowing", () => {
  test("ok return value", () => {
    const value = getValue("ok");
    if (isError(value)) {
      // Since value is ok, this path will not occur, but value would be Error type if it did
      const errorValue: Error = value;
      expect(errorValue.message).toBeFalsy();
      assert.fail();
      return;
    }

    // value is expected typ string, i.e. toUpperCase() works
    const stringValue: string = value;
    expect(stringValue.toUpperCase()).toEqual("OK");
  });

  test("error return value", () => {
    const value = getValue("error");
    if (isError(value)) {
      // Value is an Error type (i.e. message exists), verify and exit function
      expect(value.message).toEqual("invalid");
      return;
    }

    // Since it was an error, this path will not occur, but if there where a value, it would be a string
    const stringValue: string = value;
    expect(stringValue.toUpperCase()).toEqual("OK");
    assert.fail();
  });
});

describe("Test orDefault(value) for Result<T> value ", () => {
  test("using orDefault(value) for ok return value", () => {
    // Using orDefault to force value to be expected type or default value
    const value: string = orDefault(getValue("ok"), "some");
    expect(value).toEqual("ok");
  });

  test("using orDefault(value) for error return value", () => {
    // Using orDefault to force value to be expected type or default value
    const value: string = orDefault(getValue("error"), "some");
    expect(value).toBe("some");
  });
});

describe("Test Result<T> type using instanceof operator(value) for narrowing", () => {
  test("ok return value determined with instanceof operator", () => {
    const value = getValue("ok");
    if (value instanceof Error) {
      // Since value is ok, this path will not occurs, but value would be Error type if it did
      const errorValue: Error = value;
      expect(errorValue.message).toBeFalsy();
      assert.fail();
      return;
    }

    // value is expected typ string, i.e. toUpperCase() works
    const stringValue: string = value;
    expect(stringValue.toUpperCase()).toEqual("OK");
  });

  test("error return value determined instanceof", () => {
    // Getting error return Error type
    const value = getValue("error");
    if (value instanceof Error) {
      // Value is an Error type (i.e. message exists), verify and exit function
      expect(value.message).toEqual("invalid");
      return;
    }

    // Since it was an error, this path will not occur, but if there where a value, it would be a string
    const stringValue: string = value;
    expect(stringValue.toUpperCase()).toEqual("OK");
    assert.fail();
  });
});
