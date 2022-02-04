import LocalStore, { ILocalStore } from "./LocalStore";
import { expectValue, isError } from "./Result";

describe("Test LocalData", () => {
  test("Test", () => {
    const local: ILocalStore = new LocalStore();
    expect(local.count()).toEqual(0);

    local.write("0", "aa");
    expect(local.count()).toEqual(1);
    expect(local.keys()).toEqual(["0"]);
    expect(expectValue(local.tryRead<string>("0"))).toEqual("aa");
    expect(isError(local.tryRead<string>("1"))).toEqual(true);

    local.write("1", "bb");
    expect(local.count()).toEqual(2);
    expect(local.keys()).toEqual(["0", "1"]);
    expect(expectValue(local.tryRead<string>("0"))).toEqual("aa");
    expect(expectValue(local.tryRead<string>("1"))).toEqual("bb");

    const values = local.tryReadBatch(["0", "1", "2"]);
    expect(expectValue(values[0])).toEqual("aa");
    expect(expectValue(values[1])).toEqual("bb");
    expect(isError(values[2])).toEqual(true);

    local.remove("0");
    expect(local.count()).toEqual(1);
    expect(local.keys()).toEqual(["1"]);
    expect(isError(local.tryRead<string>("0"))).toEqual(true);
    expect(expectValue(local.tryRead<string>("1"))).toEqual("bb");

    local.write("0", "aa");
    expect(local.count()).toEqual(2);
    expect(local.keys().sort()).toEqual(["0", "1"]);

    local.removeBatch(["0", "1"]);
    expect(local.count()).toEqual(0);
    expect(isError(local.tryRead<string>("0"))).toEqual(true);
    expect(isError(local.tryRead<string>("1"))).toEqual(true);

    local.write("0", "aa");
    local.write("1", "bb");
    expect(local.count()).toEqual(2);

    local.clear();
    expect(local.count()).toEqual(0);
    expect(isError(local.tryRead<string>("0"))).toEqual(true);
    expect(isError(local.tryRead<string>("1"))).toEqual(true);
  });
});
