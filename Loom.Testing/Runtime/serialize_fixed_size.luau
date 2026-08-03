for _, want in { {id=0,position=Vector3.new(0,0,0)}, {id=255,position=Vector3.new(-32768,32767,-1)} } do
  local p = MyData_serialize_binary(want)
  assert(buffer.len(p.buffer) == 7, "expected 7 bytes, got " .. buffer.len(p.buffer))
  assert(p.blobs == nil, "no blobs expected")
  local r = MyData_deserialize_binary(p)
  assert(r.ok and r.value.id == want.id and r.value.position == want.position, "roundtrip")
end
local bad = MyData_deserialize_binary({ buffer = buffer.create(3) })
assert(not bad.ok and bad.error.kind == "truncated", "short buffer")
assert(not MyData_deserialize_binary({}).ok, "missing buffer")
