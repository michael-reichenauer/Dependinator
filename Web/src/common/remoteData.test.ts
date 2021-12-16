import { di } from "./di";
import { IRemoteData, IRemoteDataKey, NotModifiedError } from "./remoteData";

interface data {
  id: string;
  data: string;
}

describe("Test IRemoteData", () => {
  test("Test", async () => {
    const remote: IRemoteData = di(IRemoteDataKey);

    await remote.writeBatch([{ id: "0", data: "aa", timestamp: 10 }]);

    // Get value if not specify timestamp
    await expect(remote.tryRead<data>({ id: "0" })).resolves.toHaveProperty(
      "data",
      "aa"
    );
    // Get value if  IfNoneMatch=0
    await expect(
      remote.tryRead<data>({ id: "0", IfNoneMatch: 0 })
    ).resolves.toHaveProperty("data", "aa");

    // Get NotModified if IfNoneMatch = 10
    await expect(
      remote.tryRead<data>({ id: "0", IfNoneMatch: 10 })
    ).resolves.toBeInstanceOf(NotModifiedError);

    // Get RangeError if key is wrong
    await expect(remote.tryRead<data>({ id: "1" })).resolves.toBeInstanceOf(
      RangeError
    );
  });
});
