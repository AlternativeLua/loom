-- Compositions rather than single shapes: each pairing resolves child values against a different base,
-- and every earlier bug in this area was one of them reaching for the wrong one.
local cases = {
  { maybe_map = nil, map_list = {}, sparse = {}, maybe_leaf = nil, maybe_names = nil },
  {
    maybe_map = { a = 1.5 },
    map_list = { { x = { n = 1 } }, {}, { y = { n = 255 }, z = { n = 0 } } },
    sparse = { present = "here", absent = nil },
    maybe_leaf = { n = 42 },
    maybe_names = { "", "two" },
  },
}

for index, want in cases do
  local r = Compositions_deserialize_binary(Compositions_serialize_binary(want))
  assert(r.ok, "case " .. index)
  local got = r.value

  if want.maybe_map == nil then
    assert(got.maybe_map == nil, "absent map stays absent")
  else
    assert(math.abs(got.maybe_map.a - 1.5) < 0.001, "map inside an optional")
  end

  assert(#got.map_list == #want.map_list, "map list count")
  for i, m in want.map_list do
    for k, leaf in m do
      assert(got.map_list[i][k].n == leaf.n, "struct in map in array at " .. i .. "," .. k)
    end
  end

  assert(got.sparse.present == want.sparse.present, "present value in map")
  assert(got.sparse.absent == nil, "absent value in map")

  if want.maybe_leaf == nil then
    assert(got.maybe_leaf == nil, "absent struct")
  else
    assert(got.maybe_leaf.n == want.maybe_leaf.n, "optional struct")
  end

  if want.maybe_names == nil then
    assert(got.maybe_names == nil, "absent array")
  else
    assert(#got.maybe_names == #want.maybe_names, "array in optional count")
    for i, n in want.maybe_names do
      assert(got.maybe_names[i] == n, "array in optional value " .. i)
    end
  end
end
