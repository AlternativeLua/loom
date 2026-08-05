-- Entries needing bits share a block sized from a count the walk produced, so an off-by-one in either
-- the count or the block origin shows up as pairs reading each other's bits.
local cases = {
  { flags = {}, entries = {}, lists = {} },
  {
    flags = { a = true, b = false, c = true },
    entries = { [1] = { level = 3, label = "three" }, [7] = { level = 0, label = "" } },
    lists = { xs = {}, ys = { 1, 2, 3 } },
  },
}

for _, want in cases do
  local p = Registry_serialize_binary(want)
  local r = Registry_deserialize_binary(p)
  assert(r.ok, "ok")

  for k, v in want.flags do
    assert(r.value.flags[k] == v, "flag " .. k)
  end
  for k, entry in want.entries do
    assert(r.value.entries[k] ~= nil, "entry " .. k .. " missing")
    assert(r.value.entries[k].level == entry.level, "entry level " .. k)
    assert(r.value.entries[k].label == entry.label, "entry label " .. k)
  end
  for k, list in want.lists do
    assert(#r.value.lists[k] == #list, "list count " .. k)
    for i, n in list do
      assert(math.abs(r.value.lists[k][i] - n) < 0.001, "list value " .. k .. "," .. i)
    end
  end
end
