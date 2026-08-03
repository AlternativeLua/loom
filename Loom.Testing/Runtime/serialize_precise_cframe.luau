-- Precise keeps all four quaternion components; Compressed drops the largest and rebuilds it, trading
-- exactness for twelve bytes. Both must survive a rotation that is not the identity.
local rotations = {
  CFrame.identity,
  CFrame.new(1, 2, 3, 0.2, 0.3, 0.4, 0.84),
  CFrame.new(-500, 0.25, 1000, -0.5, 0.5, -0.5, 0.5),
}

for _, cf in rotations do
  local r = Anchor_deserialize_binary(Anchor_serialize_binary({ exact = cf, approximate = cf }))
  assert(r.ok, "ok")
  assert(r.value.exact == cf, "precise rotation")
  assert(r.value.approximate == cf, "compressed rotation within tolerance")
  assert(math.abs(r.value.exact.X - cf.X) < 0.01, "precise position")
  assert(math.abs(r.value.approximate.X - cf.X) < 0.01, "compressed position")
end

-- Precise is position plus four components; Compressed is position plus a packed word.
assert(buffer.len(Anchor_serialize_binary({ exact = CFrame.identity, approximate = CFrame.identity }).buffer) == 28 + 16, "widths")
