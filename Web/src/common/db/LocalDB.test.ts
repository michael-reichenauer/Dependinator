import { ILocalDB, LocalDB } from "./LocalDB";
import { expectValue, isError } from "../Result";

describe("Test LocalData", () => {
  test("Test", () => {
    const local: ILocalDB = new LocalDB();

    local.write({
      key: "0",
      stamp: 0,
      synced: 0,
      version: 0,
      value: "aa",
    });

    expect(expectValue(local.tryReadValue<string>("0"))).toEqual("aa");
    expect(isError(local.tryReadValue<string>("1"))).toEqual(true);

    local.write({
      key: "1",
      stamp: 0,
      synced: 0,
      version: 0,
      value: "bb",
    });
    expect(expectValue(local.tryReadValue<string>("0"))).toEqual("aa");
    expect(expectValue(local.tryReadValue<string>("1"))).toEqual("bb");

    const entities = local.tryReadBatch(["0", "1", "2"]);
    expect(expectValue(entities[0]).value).toEqual("aa");
    expect(expectValue(entities[1]).value).toEqual("bb");
    expect(isError(entities[2])).toEqual(true);

    local.removeBatch(["0"], true);
    expect(isError(local.tryReadValue<string>("0"))).toEqual(true);
    expect(expectValue(local.tryReadValue<string>("1"))).toEqual("bb");

    local.write({
      key: "0",
      stamp: 0,
      synced: 0,
      version: 0,
      value: "aa",
    });

    local.removeBatch(["0", "1"], false);
    expect(local.getRemovedKeys()).toEqual(expect.arrayContaining(["0", "1"]));

    expect(isError(local.tryReadValue<string>("0"))).toEqual(true);
    expect(isError(local.tryReadValue<string>("1"))).toEqual(true);

    local.write({
      key: "0",
      stamp: 0,
      synced: 0,
      version: 0,
      value: "aa",
    });
    local.write({
      key: "1",
      stamp: 0,
      synced: 0,
      version: 0,
      value: "bb",
    });

    local.clear();
    expect(isError(local.tryReadValue<string>("0"))).toEqual(true);
    expect(isError(local.tryReadValue<string>("1"))).toEqual(true);
  });
});
