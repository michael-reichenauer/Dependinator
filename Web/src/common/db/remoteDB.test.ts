import { di, registerSingleton } from "../di";
import {
  IRemoteDB,
  IRemoteDBKey,
  NotModifiedError,
  RemoteEntity,
} from "./RemoteDB";
import Result, { isError } from "../Result";
import { IApiKey, Query } from "../Api";
import { ApiMock } from "./ApiMock";

beforeAll(() => {
  // Mock api to use local store for tests
  registerSingleton(IApiKey, ApiMock);
});

describe("Test IRemoteData", () => {
  test("Test1", async () => {
    const remote: IRemoteDB = di(IRemoteDBKey);

    // Read one value function to make testing easier
    const remoteTryRead = async (
      query: Query
    ): Promise<Result<RemoteEntity>> => {
      const entities = await remote.tryReadBatch([query]);
      if (isError(entities)) {
        return entities;
      }
      return entities[0];
    };

    // Write one entity
    const rsp = await remote.writeBatch([
      { key: "0", value: "aa", version: 1 },
    ]);
    expect(rsp).not.toBeInstanceOf(Error);

    // Extract the response etag needed in a query below
    const rspEtag = isError(rsp) ? undefined : rsp[0].etag;

    // Get value if not specify IfNoneMatch
    await expect(remoteTryRead({ key: "0" })).resolves.toHaveProperty(
      "value",
      "aa"
    );
    // Get value if  IfNoneMatch=dd (i.e. differs from server value (is modified))
    await expect(
      remoteTryRead({ key: "0", IfNoneMatch: "dd" })
    ).resolves.toHaveProperty("value", "aa");

    // Get NotModified if IfNoneMatch = written etag (same as server value, i.e. not modified)
    await expect(
      remoteTryRead({ key: "0", IfNoneMatch: rspEtag })
    ).resolves.toBeInstanceOf(NotModifiedError);

    // Get Error if key is wrong
    await expect(remoteTryRead({ key: "1" })).resolves.toBeInstanceOf(Error);
  });
});
