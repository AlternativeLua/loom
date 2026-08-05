for _, want in { {id=1,target=nil}, {id=2,target=-1234} } do
  local p = Entity_serialize_binary(want)
  local expected = if want.target == nil then 2 else 4
  assert(buffer.len(p.buffer) == expected, "size " .. buffer.len(p.buffer) .. " ~= " .. expected)
  local r = Entity_deserialize_binary(p)
  assert(r.ok and r.value.id == want.id and r.value.target == want.target, "roundtrip")
end
local b = buffer.create(2)
buffer.writebits(b, 0, 1, 1)
local bad = Entity_deserialize_binary({ buffer = b })
assert(not bad.ok and bad.error.kind == "truncated", "short optional payload")
