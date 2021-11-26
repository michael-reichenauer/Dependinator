import LocalData from "./LocalData";

describe("Test LocalData", () => {
  test("count", () => {
    const local = new LocalData();

    expect(local.count()).toEqual(0);

    local.write("0", "aa");
    expect(local.count()).toEqual(1);
    local.write("1", "bb");
    expect(local.count()).toEqual(2);
    // for (var i = 0, len = localStorage.length; i < len; i++) {
    //   var key = localStorage.key(i);
    //   console.log("key", i, key, local.readData(local.key(i)));
    // }

    //expect(local.key(2)).toEqual("b");
  });
});
