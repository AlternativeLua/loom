local sentinels = { Vector3.zero, Vector3.one, Vector3.xAxis, Vector3.yAxis, Vector3.zAxis }
for _, v in sentinels do
  local p = Entity_serialize_binary({ id = 9, position = v })
  assert(buffer.len(p.buffer) == 2, "sentinel size " .. buffer.len(p.buffer))
  local r = Entity_deserialize_binary(p)
  assert(r.ok and r.value.position == v and r.value.id == 9, "sentinel roundtrip")
end
local arbitrary = Vector3.new(5, -6, 7)
local p = Entity_serialize_binary({ id = 3, position = arbitrary })
assert(buffer.len(p.buffer) == 8, "escape size " .. buffer.len(p.buffer))
local r = Entity_deserialize_binary(p)
assert(r.ok and r.value.position == arbitrary, "escape roundtrip")
local b = buffer.create(8)
buffer.writebits(b, 0, 3, 6)
assert(Entity_deserialize_binary({ buffer = b }).error.kind == "invalid_tag", "reserved tag")
