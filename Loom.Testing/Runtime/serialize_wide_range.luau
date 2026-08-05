-- A range spanning a whole 32-bit word is the widest writebits can carry; the boundary is where a
-- clamped bit count would silently drop the top half of every value.
for _, want in { 0, 1, 65535, 2147483647, 4294967295 } do
  local p = Counter_serialize_binary({ total = want })
  local r = Counter_deserialize_binary(p)
  assert(r.ok, "ok")
  assert(r.value.total == want, "expected " .. want .. ", got " .. tostring(r.value.total))
end
