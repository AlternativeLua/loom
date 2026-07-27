type Box = {
  read value: number,
}
const function describe(x: number | string | Box): string
  local _match
  if x == 0 or x == 100 then
    _match = "zero or one hundred"
  elseif typeof(x) == "number" and x >= 1 and x <= 10 then
    _match = "small range"
  elseif typeof(x) == "number" then
    const n = x
    _match = "number"
  elseif typeof(x) == "string" then
    const s = x
    _match = s
  elseif typeof(x) == "table" then
    const value = x.value
    _match = "box value"
  else
    _match = "other"
  end
  return _match
end
const function describeGuarded(n: number): string
  local _match_1
  if n > 0 then
    const m = n
    _match_1 = "positive"
  else
    _match_1 = "non-positive"
  end
  return _match_1
end
const function describeAndPattern(x: number | string): string
  local _match_2
  if typeof(x) == "number" and x > 0 then
    const n = x
    _match_2 = "positive number"
  elseif typeof(x) == "number" then
    const n = x
    _match_2 = "non-positive number"
  else
    _match_2 = "string"
  end
  return _match_2
end
const _subject = {1, 2, 3}
local _match_3
if typeof(_subject) == "table" then
  const head = _subject[1]
  const rest = table.move(_subject, 2, #_subject, 1, {})
  _match_3 = head
else
  _match_3 = 0
end
const firstAndRest = _match_3
const onlyWildcard = 42