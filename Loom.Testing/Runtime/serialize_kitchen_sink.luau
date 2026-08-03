local part = MakeInstance("Part")
local cases = {
  { tag="sink", inner={flag=true,code=7}, label=nil, points={}, payload={kind="Ping"}, owner=part, frame=CFrame.identity },
  { tag="sink", inner={flag=false,code=200}, label="hi", points={Vector3.new(1,2,3)}, payload={kind="Chat",message="yo",volume=55}, owner=part, frame=CFrame.new(1,2,3,0.1,0.2,0.3,0.92) },
}
for _, want in cases do
  local p = KitchenSink_serialize_binary(want)
  local r = KitchenSink_deserialize_binary(p)
  assert(r.ok, "ok")
  assert(r.value.tag == "sink", "literal")
  assert(r.value.inner.flag == want.inner.flag and r.value.inner.code == want.inner.code, "nested")
  assert(r.value.label == want.label, "optional string")
  assert(#r.value.points == #want.points, "array count")
  for i, v in want.points do assert(r.value.points[i] == v, "point " .. i) end
  assert(r.value.payload.kind == want.payload.kind, "union tag")
  assert(r.value.payload.message == want.payload.message, "variant string")
  assert(r.value.payload.volume == want.payload.volume, "variant ranged")
  assert(r.value.owner == part, "blob")
end
