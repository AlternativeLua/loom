-- Each inner collection reserves a bit block at its own runtime offset, and a union entry's tag lives
-- in the outer block. Getting either origin wrong shows up as entries reading each other's bits.
local cases = {
  { masks = {}, shapes = {} },
  { masks = { {} }, shapes = { { kind = "Dot" } } },
  {
    masks = { { true, false, true }, {}, { false, false, false, false, false, false, false, false, true } },
    shapes = { { kind = "Dot" }, { kind = "Line", label = "edge" }, { kind = "Line", label = "" }, { kind = "Dot" } },
  },
}

for _, want in cases do
  local p = Canvas_serialize_binary(want)
  local r = Canvas_deserialize_binary(p)
  assert(r.ok, "ok")

  assert(#r.value.masks == #want.masks, "outer mask count")
  for i, mask in want.masks do
    assert(#r.value.masks[i] == #mask, "inner mask count " .. i)
    for j, bit in mask do
      assert(r.value.masks[i][j] == bit, "mask bit " .. i .. "," .. j)
    end
  end

  assert(#r.value.shapes == #want.shapes, "shape count")
  for i, shape in want.shapes do
    assert(r.value.shapes[i].kind == shape.kind, "shape tag " .. i)
    assert(r.value.shapes[i].label == shape.label, "shape label " .. i)
  end
end
