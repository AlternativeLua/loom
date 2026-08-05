for _, want in { {values={}}, {values={1,2,3,255}} } do
  local p = Scores_serialize_binary(want)
  assert(buffer.len(p.buffer) == 4 + #want.values, "size " .. buffer.len(p.buffer))
  local r = Scores_deserialize_binary(p)
  assert(r.ok and #r.value.values == #want.values, "count")
  for i, v in want.values do assert(r.value.values[i] == v, "element " .. i) end
end
local b = buffer.create(4)
buffer.writeu32(b, 0, 1000)
local bad = Scores_deserialize_binary({ buffer = b })
assert(not bad.ok and bad.error.kind == "invalid_length", "hostile count")
