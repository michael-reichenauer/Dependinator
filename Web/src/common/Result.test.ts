import assert from "assert";
import Result, { orDefault } from "./Result";

function getValue(index: number): Result<string> {
  if (index < 0) {
    return new RangeError("Not found");
  }
  return index.toString();
}

describe("Test Result type", () => {
  test("ok", () => {
    // Getting ok value returns expected typ
    const value = getValue(1);
    if (value instanceof Error) {
      assert.fail();
      return;
    }

    // value is string, i.e. toUpperCase exists
    expect(value.toUpperCase()).toEqual("1");
  });

  test("error", () => {
    // Getting error return Error type
    const value = getValue(-1);
    if (value instanceof Error) {
      // Value is an Error (i.e. message exists)
      expect(value.message).toEqual("Not found");
      return;
    }
    // No value
    assert.fail();
  });

  test("orDefault ok value", () => {
    // Using orDefault to force value to be expected type or default value
    const value: string = orDefault(getValue(1), "some");
    expect(value).toBe("1");
  });

  test("orDefault error value", () => {
    // Using orDefault to force value to be expected type or default value
    const value: string = orDefault(getValue(-1), "some");
    expect(value).toBe("some");
  });
});
