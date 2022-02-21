import { ILocalDB, LocalDB } from "./LocalDB";
import { expectValue, isError } from "../Result";

describe("Test LocalData", () => {
  test("Test", () => {
    const local: ILocalDB = new LocalDB();

    // Write one entity '0' and verify that it can be read with correct key
    local.write({
      key: "0",
      etag: "1",
      value: "aa",
      version: 0,
    });

    expect(expectValue(local.tryReadValue<string>("0"))).toEqual("aa");
    expect(local.getUnsyncedKeys().length).toEqual(1);
    expect(isError(local.tryReadValue<string>("1"))).toEqual(true);

    // Write another entity '1' and verify that both values can be read
    local.write({
      key: "1",
      etag: "1",
      value: "bb",
      version: 0,
    });
    expect(expectValue(local.tryReadValue<string>("0"))).toEqual("aa");
    expect(expectValue(local.tryReadValue<string>("1"))).toEqual("bb");
    expect(local.getUnsyncedKeys().length).toEqual(2);

    // Read a batch of entities and verify that 2 entities exist
    const entities = local.tryReadBatch(["0", "1", "2"]);
    expect(expectValue(entities[0]).value).toEqual("aa");
    expect(expectValue(entities[1]).value).toEqual("bb");
    expect(isError(entities[2])).toEqual(true);

    // Remove '0' and confirm removed value
    local.preRemoveBatch(["0"]);
    local.confirmRemoved(["0"]);
    expect(isError(local.tryReadValue<string>("0"))).toEqual(true);
    expect(expectValue(local.tryReadValue<string>("1"))).toEqual("bb");

    // Rewrite '0'
    local.write({
      key: "0",
      etag: "",
      value: "aa",
      version: 0,
    });

    // Remove '0' and '1', and verify that values are removed, but not confirmed yet
    local.preRemoveBatch(["0", "1"]);
    expect(local.getRemovedKeys()).toEqual(expect.arrayContaining(["0", "1"]));

    expect(isError(local.tryReadValue<string>("0"))).toEqual(true);
    expect(isError(local.tryReadValue<string>("1"))).toEqual(true);

    // Confirm removed (after sync) and verify
    local.confirmRemoved(["0", "1"]);
    expect(local.getRemovedKeys()).toEqual([]);

    // Write some values, first is synced, second is not synced
    local.write({
      key: "0",
      etag: "1",
      syncedEtag: "1",
      value: "aa",
      version: 0,
    });
    local.write({
      key: "1",
      etag: "2",
      syncedEtag: "1",
      value: "bb",
      version: 0,
    });
    expect(local.getAllEntities().length).toEqual(2);
    expect(local.getUnsyncedKeys().length).toEqual(1); // only last is unsynced

    // Clear local database and verify empty
    local.clear();
    expect(isError(local.tryReadValue<string>("0"))).toEqual(true);
    expect(isError(local.tryReadValue<string>("1"))).toEqual(true);
  });
});
