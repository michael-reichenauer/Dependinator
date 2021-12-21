import { di } from "./di";
import { IRemoteDB, IRemoteDBKey, NotModifiedError } from "./remoteDB";

describe("Test IRemoteData", () => {
  test("Test", async () => {
    const remote: IRemoteDB = di(IRemoteDBKey);

    await remote.writeBatch([
      { key: "0", value: "aa", timestamp: 10, version: 1 },
    ]);

    // Get value if not specify timestamp
    await expect(remote.tryRead<string>({ key: "0" })).resolves.toHaveProperty(
      "value",
      "aa"
    );
    // Get value if  IfNoneMatch=0
    await expect(
      remote.tryRead<string>({ key: "0", IfNoneMatch: 0 })
    ).resolves.toHaveProperty("value", "aa");

    // Get NotModified if IfNoneMatch = 10
    await expect(
      remote.tryRead<string>({ key: "0", IfNoneMatch: 10 })
    ).resolves.toBeInstanceOf(NotModifiedError);

    // Get RangeError if key is wrong
    await expect(remote.tryRead<string>({ key: "1" })).resolves.toBeInstanceOf(
      RangeError
    );
  });
});
