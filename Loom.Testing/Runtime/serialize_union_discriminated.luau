for _, want in { {action={kind="LogOut"},color="red"}, {action={kind="Click",x=42},color="blue"} } do
  local p = Envelope_serialize_binary(want)
  local r = Envelope_deserialize_binary(p)
  assert(r.ok, "ok")
  assert(r.value.action.kind == want.action.kind, "discriminant restored")
  assert(r.value.action.x == want.action.x, "variant payload")
  assert(r.value.color == want.color, "literal union")
end
assert(buffer.len(Envelope_serialize_binary({action={kind="LogOut"},color="red"}).buffer) == 1, "empty variant is one byte")
local b = buffer.create(1)
buffer.writebits(b, 1, 2, 3)
assert(Envelope_deserialize_binary({ buffer = b }).error.kind == "invalid_tag", "bad colour tag")
