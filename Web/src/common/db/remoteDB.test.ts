import { di } from "../di";
import {
  IRemoteDB,
  IRemoteDBKey,
  NotModifiedError,
  RemoteEntity,
} from "./RemoteDB";
import Result, { isError } from "../Result";
import { Query } from "../Api";

describe("Test IRemoteData", () => {
  test("Test1", async () => {
    //;RemoteApi.testDelay = 0;
    const remote: IRemoteDB = di(IRemoteDBKey);

    const remoteTryRead = async (
      query: Query
    ): Promise<Result<RemoteEntity>> => {
      const entities = await remote.tryReadBatch([query]);
      if (isError(entities)) {
        return entities;
      }
      return entities[0];
    };

    await expect(
      remote.writeBatch([{ key: "0", value: "aa", stamp: "10", version: 1 }])
    ).resolves.not.toBeInstanceOf(Error);

    // Get value if not specify timestamp
    await expect(remoteTryRead({ key: "0" })).resolves.toHaveProperty(
      "value",
      "aa"
    );
    // Get value if  IfNoneMatch=0
    await expect(
      remoteTryRead({ key: "0", IfNoneMatch: "" })
    ).resolves.toHaveProperty("value", "aa");

    // Get NotModified if IfNoneMatch = 10
    await expect(
      remoteTryRead({ key: "0", IfNoneMatch: "10" })
    ).resolves.toBeInstanceOf(NotModifiedError);

    // Get RangeError if key is wrong
    await expect(remoteTryRead({ key: "1" })).resolves.toBeInstanceOf(Error);
  });
});
