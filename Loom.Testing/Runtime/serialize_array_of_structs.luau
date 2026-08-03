local part = MakeInstance("Part")
local bag = Bag_serialize_binary({ owners = { part, MakeInstance("Part") } })
assert(#bag.blobs == 2, "one blob per element")
local br = Bag_deserialize_binary(bag)
assert(br.ok and #br.value.owners == 2 and br.value.owners[1] == part, "blob array roundtrip")
assert(Bag_deserialize_binary({ buffer = bag.buffer, blobs = { part } }).error.kind == "missing_blob", "short blob array")

for _, want in { {leaves={}}, {leaves={{v=1},{v=255}}} } do
  local p = Branch_serialize_binary(want)
  local r = Branch_deserialize_binary(p)
  assert(r.ok, "ok")
  assert(#r.value.leaves == #want.leaves, "count")
  for i, leaf in want.leaves do
    assert(type(r.value.leaves[i]) == "table", "element must be a struct")
    assert(r.value.leaves[i].v == leaf.v, "element field " .. i)
  end
end
