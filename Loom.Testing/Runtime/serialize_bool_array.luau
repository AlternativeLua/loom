-- Bits are the point: eight entries per byte rather than one byte each.
local cases = { {}, { true }, { false, true, false }, {} }
for i = 1, 20 do cases[4][i] = i % 3 == 0 end

for _, want in cases do
  local p = Flags_serialize_binary({ values = want })
  local expected = 4 + (#want + 7) // 8
  assert(buffer.len(p.buffer) == expected, "size " .. buffer.len(p.buffer) .. " ~= " .. expected)

  local r = Flags_deserialize_binary(p)
  assert(r.ok, "ok")
  assert(#r.value.values == #want, "count")
  for j, v in want do
    assert(r.value.values[j] == v, "entry " .. j)
  end
end
