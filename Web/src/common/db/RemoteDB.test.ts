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
import { IDataCryptKey } from "./../DataCrypt";
import { DataCryptMock } from "./../DataCryptMock";
import { IKeyVaultKey, IKeyVaultConfigureKey } from "./../keyVault";

beforeAll(async () => {
  // Mock Api and DataCrypt for tests
  registerSingleton(IApiKey, ApiMock);
  registerSingleton(IDataCryptKey, DataCryptMock);

  // Simulate logging in to create mock DEK used for encryption
  const user = { username: "test", password: "testPass" };
  const wDek = await di(IDataCryptKey).generateWrappedDataEncryptionKey(user);
  const dek = await di(IDataCryptKey).unwrapDataEncryptionKey(wDek, user);
  di(IKeyVaultConfigureKey).setDek(dek);
});

afterAll(() => {
  // Simulate logout and reset DEK
  di(IKeyVaultConfigureKey).setDek(null);
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
      { key: "0", etag: "", localEtag: "", value: "aa", version: 1 },
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
