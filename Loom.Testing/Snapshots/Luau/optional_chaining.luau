type Inner = {
  read c: number,
}
type Outer = {
  read b: Inner?,
}
const a: Outer? = nil
local _optionalResult
if a ~= nil then
  const _optional = a.b
  if _optional ~= nil then
    _optionalResult = _optional.c
  else
    _optionalResult = nil
  end
else
  _optionalResult = nil
end
const x = _optionalResult