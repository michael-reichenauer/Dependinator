import LocalData, { ILocalData } from "./LocalData";
import { orDefault } from "./Result";

describe("Test LocalData", () => {
  test("Test", () => {
    const local: ILocalData = new LocalData();
    expect(local.count()).toEqual(0);

    local.write("0", "aa");
    expect(local.count()).toEqual(1);
    expect(local.keys()).toEqual(["0"]);
    expect(orDefault(local.tryRead("0"), "")).toEqual("aa");
    expect(orDefault(local.tryRead("1"), "")).toEqual("");

    local.write("1", "bb");
    expect(local.count()).toEqual(2);
    expect(local.keys()).toEqual(["0", "1"]);
    expect(orDefault(local.tryRead("0"), "")).toEqual("aa");
    expect(orDefault(local.tryRead("1"), "")).toEqual("bb");

    const all = local.tryReadBatch(["0", "1", "2"]);
    expect(orDefault(all[0], "")).toEqual("aa");
    expect(orDefault(all[1], "")).toEqual("bb");
    expect(orDefault(all[2], "")).toEqual("");

    local.remove("0");
    expect(local.count()).toEqual(1);
    expect(local.keys()).toEqual(["1"]);
    expect(orDefault(local.tryRead("0"), "")).toEqual("");
    expect(orDefault(local.tryRead("1"), "")).toEqual("bb");

    local.write("0", "aa");
    expect(local.count()).toEqual(2);
    expect(local.keys().sort()).toEqual(["0", "1"]);

    local.removeBatch(["0", "1"]);
    expect(local.count()).toEqual(0);
    expect(orDefault(local.tryRead("0"), "")).toEqual("");
    expect(orDefault(local.tryRead("1"), "")).toEqual("");

    local.write("0", "aa");
    local.write("1", "bb");
    expect(local.count()).toEqual(2);

    local.clear();
    expect(local.count()).toEqual(0);
    expect(orDefault(local.tryRead("0"), "")).toEqual("");
    expect(orDefault(local.tryRead("1"), "")).toEqual("");
  });
});
