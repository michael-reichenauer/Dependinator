import assert from "assert";

// The container registry tha contains mapping from interface symbol to registered classes
const registry = new Map<Symbol, registryItem>();

// Items registered in the container registry
interface registryItem {
  instance?: any; // the singleton instance after first reference
  factory?: () => any; // the instance factory registered using @singleton(interfaceKey)
}

// Specified a class type with a constructor (when registering classes)
type Class = { new (...args: any[]): any };

// A typed symbol used to define interface keys, (TInterface is not used directly, but indirectly
// when registering class in decorator and when resolving instance using di<T>(key))
// eslint-disable-next-line
class InterfaceKey<TInterface> {
  id = Symbol();
}

// Every interface that is used in DI must have a defined DiKey<TInterface>, which can be
// specified when registering classes and resolving instances
export function diKey<TInterface>(): InterfaceKey<TInterface> {
  return new InterfaceKey<TInterface>();
}

// The @singleton(key) decorator used when registering classes
export function singleton<TInterface>(key: InterfaceKey<TInterface>): any {
  return function <TClass extends Class>(targetClass: TClass) {
    registerSingleton(key, targetClass);
  };
}

// The resolver for class instances, when specifying interface types defined using diKey() function
// Class instance is created at first reference
export function di<TInterface>(key: InterfaceKey<TInterface>): TInterface {
  assert(
    registry.has(key.id),
    `DI key has not been registered. Implementation must specify a @singleton(interfaceKey) decorator`
  );

  const item = registry.get(key.id);
  if (item?.instance === undefined) {
    // First resolve, lets use factory to create instance
    assert(
      item?.factory,
      `DI class instance factory has not been registered. Implementation must specify a @singleton(interfaceKey) decorator`
    );

    item.instance = item.factory();

    item.factory = undefined;
    if (!item?.instance) {
      assert.fail(
        `DI class instance factory did not create an instance. Implementation must specify a @singleton(interfaceKey) decorator`
      );
    }
  }

  return item.instance;
}

// Registers a class as a single instance
function registerSingleton<TInterface>(
  key: InterfaceKey<TInterface>,
  classType: Class
) {
  const item: registryItem = {
    factory: () => new classType(),
  };

  registry.set(key.id, item);
}

// function cleanseAssertionOperators(parsedName: string): string {
//   return parsedName.replace(/[?!]/g, "");
// }

// export function nameof<T>(classType: { new (...params: any[]): T }): string {
//   const fnStr = classType.toString();
//   console.log("fn:", fnStr);

//   if (
//     fnStr.startsWith("class ") &&
//     // Theoretically could, for some ill-advised reason, be "class => class.prop".
//     !fnStr.startsWith("class =>")
//   ) {
//     return cleanseAssertionOperators(
//       fnStr.substring("class ".length, fnStr.indexOf(" {"))
//     );
//   }

//   throw new Error("Invalid");
// }
