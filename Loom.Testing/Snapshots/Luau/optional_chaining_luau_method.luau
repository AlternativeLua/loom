type Foo = {
  read DoFoo: (Foo) -> (),
}
type Bar = {
  read foo: Foo?,
}
const bar = (nil :: never) :: Bar?
const _ = if bar ~= nil then if bar.foo ~= nil then bar.foo:DoFoo() else nil else nil