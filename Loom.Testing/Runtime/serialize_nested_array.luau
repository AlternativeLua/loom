-- Each nesting level needs its own loop counter and its own length prefix; an inner loop clobbering
-- the outer's counter, or an unmeasured inner width, both surface here as a wrong shape or a short buffer.
local cases = {
  { rows = {} },
  { rows = { {} } },
  { rows = { { "a" }, {}, { "", "bb", "ccc" } } },
}

for _, want in cases do
  local p = Grid_serialize_binary(want)
  local r = Grid_deserialize_binary(p)
  assert(r.ok, "ok")
  assert(#r.value.rows == #want.rows, "outer count")
  for i, row in want.rows do
    assert(type(r.value.rows[i]) == "table", "row " .. i .. " must be a table")
    assert(#r.value.rows[i] == #row, "inner count " .. i)
    for j, cell in row do
      assert(r.value.rows[i][j] == cell, "cell " .. i .. "," .. j)
    end
  end
end
