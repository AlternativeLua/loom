type Foo = {
  read DoFoo: (Foo) -> (),
}
type Bar = {
  read foo: Foo?,
}
const bar = (nil :: never) :: Bar?
local _optionalResult
if bar ~= nil then
  const _optional = bar.foo
  if _optional ~= nil then
    _optionalResult = _optional:DoFoo()
  else
    _optionalResult = nil
  end
else
  _optionalResult = nil
end
const _ = _optionalResult