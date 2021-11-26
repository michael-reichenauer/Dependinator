import assert from "assert";

export default function trust(
  value: unknown,
  message?: string | Error
): asserts value {
  if (message instanceof Error) {
    console.assert(value, message.toString());
  } else {
    console.assert(value, message);
  }
  assert(value, message);
}
