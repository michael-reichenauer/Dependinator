export {};
// import LocalStore, { Entity, ILocalStore } from "./LocalStore";
// import { expectValue, isError, orDefault } from "./Result";

// describe("Test LocalData", () => {
//   test("Test", () => {
//     const local: ILocalStore = new LocalStore();
//     expect(local.count()).toEqual(0);

//     local.write({ key: "0", timestamp: 0, synced: 0, version: 0, value: "aa" });
//     expect(local.count()).toEqual(1);
//     expect(local.keys()).toEqual(["0"]);
//     expect(expectValue(local.tryRead<string>("0")).value).toEqual("aa");
//     expect(isError(local.tryRead<string>("1"))).toEqual(true);

//     local.write({ key: "1", timestamp: 0, synced: 0, version: 0, value: "bb" });
//     expect(local.count()).toEqual(2);
//     expect(local.keys()).toEqual(["0", "1"]);
//     expect(expectValue(local.tryRead<string>("0")).value).toEqual("aa");
//     expect(expectValue(local.tryRead<string>("1")).value).toEqual("bb");

//     const all = local.tryReadBatch<string>(["0", "1", "2"]);
//     expect(expectValue(all[0]).value).toEqual("aa");
//     expect(expectValue(all[1]).value).toEqual("bb");
//     expect(isError(all[2])).toEqual(true);

//     local.remove("0");
//     expect(local.count()).toEqual(1);
//     expect(local.keys()).toEqual(["1"]);
//     expect(isError(local.tryRead<string>("0"))).toEqual(true);
//     expect(expectValue(local.tryRead<string>("1")).value).toEqual("bb");

//     local.write({ key: "0", timestamp: 0, synced: 0, version: 0, value: "aa" });
//     expect(local.count()).toEqual(2);
//     expect(local.keys().sort()).toEqual(["0", "1"]);

//     local.removeBatch(["0", "1"]);
//     expect(local.count()).toEqual(0);
//     expect(isError(local.tryRead<string>("0"))).toEqual(true);
//     expect(isError(local.tryRead<string>("1"))).toEqual(true);

//     local.write({ key: "0", timestamp: 0, synced: 0, version: 0, value: "aa" });
//     local.write({ key: "1", timestamp: 0, synced: 0, version: 0, value: "bb" });
//     expect(local.count()).toEqual(2);

//     local.clear();
//     expect(local.count()).toEqual(0);
//     expect(isError(local.tryRead<string>("0"))).toEqual(true);
//     expect(isError(local.tryRead<string>("1"))).toEqual(true);
//   });
// });
