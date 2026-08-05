local want = { names = { "", "abc", string.rep("z", 100) }, positions = { Vector3.new(1,2,3), Vector3.new(-1,-2,-3) } }
local p = Roster_serialize_binary(want)
local r = Roster_deserialize_binary(p)
assert(r.ok, "ok")
for i, n in want.names do assert(r.value.names[i] == n, "name " .. i) end
for i, v in want.positions do assert(r.value.positions[i] == v, "pos " .. i) end
assert(#r.value.names == 3 and #r.value.positions == 2, "counts")
local empty = Roster_serialize_binary({ names = {}, positions = {} })
assert(buffer.len(empty.buffer) == 8, "empty size " .. buffer.len(empty.buffer))
assert(Roster_deserialize_binary(empty).ok, "empty roundtrip")
