import LocalData, { ILocalData } from "./LocalData";
import { expectValue, isError, orDefault } from "./Result";

interface data {
  id: string;
  data: string;
}

describe("Test LocalData", () => {
  test("Test", () => {
    const local: ILocalData = new LocalData();
    expect(local.count()).toEqual(0);

    local.write({ id: "0", data: "aa" });
    expect(local.count()).toEqual(1);
    expect(local.keys()).toEqual(["0"]);
    expect(expectValue(local.tryRead<data>("0")).data).toEqual("aa");
    expect(isError(local.tryRead<data>("1"))).toEqual(true);

    local.write({ id: "1", data: "bb" });
    expect(local.count()).toEqual(2);
    expect(local.keys()).toEqual(["0", "1"]);
    expect(expectValue(local.tryRead<data>("0")).data).toEqual("aa");
    expect(expectValue(local.tryRead<data>("1")).data).toEqual("bb");

    const all = local.tryReadBatch<data>(["0", "1", "2"]);
    expect(expectValue(all[0]).data).toEqual("aa");
    expect(expectValue(all[1]).data).toEqual("bb");
    expect(isError(all[2])).toEqual(true);

    local.remove("0");
    expect(local.count()).toEqual(1);
    expect(local.keys()).toEqual(["1"]);
    expect(isError(local.tryRead<data>("0"))).toEqual(true);
    expect(expectValue(local.tryRead<data>("1")).data).toEqual("bb");

    local.write({ id: "0", data: "aa" });
    expect(local.count()).toEqual(2);
    expect(local.keys().sort()).toEqual(["0", "1"]);

    local.removeBatch(["0", "1"]);
    expect(local.count()).toEqual(0);
    expect(isError(local.tryRead<data>("0"))).toEqual(true);
    expect(isError(local.tryRead<data>("1"))).toEqual(true);

    local.write({ id: "0", data: "aa" });
    local.write({ id: "1", data: "bb" });
    expect(local.count()).toEqual(2);

    local.clear();
    expect(local.count()).toEqual(0);
    expect(isError(local.tryRead<data>("0"))).toEqual(true);
    expect(isError(local.tryRead<data>("1"))).toEqual(true);
  });
});
