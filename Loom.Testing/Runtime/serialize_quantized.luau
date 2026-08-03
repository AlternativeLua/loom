-- A quantized value lands on its grid; error is bounded by half a step, and a grid that does not start
-- at zero has to survive the shift as well as the scale.
local cases = {
  { opacity = 0, offset = -100, health = 0 },
  { opacity = 1, offset = 100, health = 100 },
  { opacity = 0.5, offset = 0, health = 50 },
  { opacity = 0.37, offset = -12.5, health = 7 },
}

for _, want in cases do
  local r = Dials_deserialize_binary(Dials_serialize_binary(want))
  assert(r.ok, "ok")
  assert(math.abs(r.value.opacity - want.opacity) <= 0.005, "opacity " .. tostring(r.value.opacity))
  assert(math.abs(r.value.offset - want.offset) <= 0.25, "offset " .. tostring(r.value.offset))
  assert(r.value.health == want.health, "health is an integer grid, so it is exact")
end

-- 101 + 401 + 101 states is 7 + 9 + 7 bits, so three bytes.
assert(buffer.len(Dials_serialize_binary(cases[1]).buffer) == 3, "packed width")
