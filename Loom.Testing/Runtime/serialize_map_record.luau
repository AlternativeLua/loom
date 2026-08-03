-- A map has no length operator, so the count comes from the same walk that sizes it. Order is not
-- preserved and need not be: reading pairs back in any order rebuilds the same value.
local cases = {
  {},
  { solo = 1 },
  { alpha = 1.5, beta = -2.25, [""] = 0, ["a longer key"] = 100 },
}

for _, want in cases do
  local p = Scores_serialize_binary({ entries = want })
  local r = Scores_deserialize_binary(p)
  assert(r.ok, "ok")

  local seen = 0
  for k, v in want do
    assert(math.abs(r.value.entries[k] - v) < 0.001, "value for key " .. k)
    seen += 1
  end
  for k in r.value.entries do
    seen -= 1
  end
  assert(seen == 0, "entry count differs")
end
