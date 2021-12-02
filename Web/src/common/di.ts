export {};

function cleanseAssertionOperators(parsedName: string): string {
  return parsedName.replace(/[?!]/g, "");
}

export function nameof2<T>(c: T) {
  console.log("p:", (c as any).toString());
  return "test";
}

export function nameof<T extends Object>(
  nameFunction: ((obj: T, r: any) => any) | { new (...params: any[]): T }
): string {
  const fnStr = nameFunction.toString();
  console.log("fnStr", fnStr);

  // ES6 class name:
  // "class ClassName { ..."
  if (
    fnStr.startsWith("class ") &&
    // Theoretically could, for some ill-advised reason, be "class => class.prop".
    !fnStr.startsWith("class =>")
  ) {
    return cleanseAssertionOperators(
      fnStr.substring("class ".length, fnStr.indexOf(" {"))
    );
  }

  // ES6 prop selector:
  // "x => x.prop"
  if (fnStr.includes("=>")) {
    return cleanseAssertionOperators(fnStr.substring(fnStr.indexOf(".") + 1));
  }

  // Invalid function.
  throw new Error("ts-simple-nameof: Invalid function.");
}
