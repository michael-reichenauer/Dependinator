import { nameof, nameof2 } from "./di";
import { ApplicationDto } from "./../application/diagram/StoreDtos";

export {};

interface ISome {
  id: string;
}

class Some implements ISome {
  id: string = "dd";
}

describe("Test DI", () => {
  test("DI", () => {
    // expect(nameof((t: ISome, r: Some) => t.id)).toEqual("Error");
    //expect(nameof2<ApplicationDto>(Some)).toEqual("ISome");
  });
});
