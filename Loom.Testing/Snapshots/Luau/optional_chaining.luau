type Inner = {
  read c: number,
}
type Outer = {
  read b: Inner?,
}
const a: Outer? = nil
const x = if a ~= nil then if a.b ~= nil then a.b.c else nil else nil