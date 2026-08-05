type Inner = {
  read value: number,
}
const a: { { Inner }? }? = nil
local _optionalResult
if a ~= nil then
  _optionalResult = a[0]
else
  _optionalResult = nil
end
local _optionalResult_1
if _optionalResult ~= nil then
  _optionalResult_1 = _optionalResult[0]
else
  _optionalResult_1 = nil
end
const x = _optionalResult_1