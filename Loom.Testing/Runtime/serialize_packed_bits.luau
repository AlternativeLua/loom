for _, want in { {kind="spawn",alive=true,ready=false,health=0}, {kind="spawn",alive=false,ready=true,health=100} } do
  local p = Flags_serialize_binary(want)
  assert(buffer.len(p.buffer) == 2, "expected 2 bytes, got " .. buffer.len(p.buffer))
  local r = Flags_deserialize_binary(p)
  assert(r.ok, "ok")
  assert(r.value.kind == "spawn" and r.value.alive == want.alive and r.value.ready == want.ready, "bools")
  assert(r.value.health == want.health, "health " .. tostring(r.value.health))
end
