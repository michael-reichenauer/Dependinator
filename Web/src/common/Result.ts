type Result<T, E = Error> = T | E;
export default Result;

export function orDefault<T, E>(value: Result<T, E>, defaultValue: T): T {
  if (value instanceof Error) {
    return defaultValue;
  }
  return value as T;
}
