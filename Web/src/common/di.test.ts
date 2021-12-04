import { di, diKey, singleton } from "./di";

export const IAKey = diKey<IA>();
export interface IA {
  aId: string;
}

export const IBKey = diKey<IB>();
export interface IB {
  bId: string;
}

@singleton(IAKey)
class A implements IA {
  aId: string;
  constructor() {
    this.aId = "aa";
  }
}

@singleton(IBKey)
class B implements IB {
  a: IA;
  bId: string;
  constructor(a: IA = di(IAKey)) {
    this.a = a;
    this.bId = "bb" + a.aId;
  }
}

describe("Test DI", () => {
  test("DI", () => {
    // Verify that referencing IB will create B and get A injected
    const b: IB = di(IBKey);
    expect(b.bId).toEqual("bbaa");

    // Verify that B is singleton
    expect(di(IBKey)).toBe(di(IBKey));

    // Verify tha IA can be referenced
    const a: IA = di(IAKey);
    expect(a.aId).toEqual("aa");
    expect(di(IAKey)).toBe(di(IAKey));
    expect(di(IAKey)).not.toBe(di(IBKey));
  });
});
