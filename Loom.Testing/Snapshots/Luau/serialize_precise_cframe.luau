const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writef32 = buffer.writef32
const buffer_writebits = buffer.writebits
const buffer_len = buffer.len
const buffer_readf32 = buffer.readf32
const buffer_readbits = buffer.readbits
const buffer_copy = buffer.copy
type Anchor = {
  read exact: CFrame,
  read approximate: CFrame,
}
const function Anchor_serialize_binary(value: Anchor): Loom.Serialized
  const b = buffer_create(44)
  buffer_writef32(b, 4, value.exact.X)
  buffer_writef32(b, 8, value.exact.Y)
  buffer_writef32(b, 12, value.exact.Z)
  const qx, qy, qz, qw = Loom.cframe_to_quaternion(value.exact)
  buffer_writef32(b, 16, qx)
  buffer_writef32(b, 20, qy)
  buffer_writef32(b, 24, qz)
  buffer_writef32(b, 28, qw)
  buffer_writef32(b, 32, value.approximate.X)
  buffer_writef32(b, 36, value.approximate.Y)
  buffer_writef32(b, 40, value.approximate.Z)
  buffer_writebits(b, 0, 32, Loom.pack_quaternion(Loom.cframe_to_quaternion(value.approximate)))
  return { buffer = b }
end
const function Anchor_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Anchor, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 44 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  return { ok = true, value = { exact = CFrame.new(buffer_readf32(b, 4), buffer_readf32(b, 8), buffer_readf32(b, 12), buffer_readf32(b, 16), buffer_readf32(b, 20), buffer_readf32(b, 24), buffer_readf32(b, 28)), approximate = CFrame.new(buffer_readf32(b, 32), buffer_readf32(b, 36), buffer_readf32(b, 40), Loom.unpack_quaternion(buffer_readbits(b, 0, 32))) } }
end
const function Anchor_diff_binary_write(baseline: Anchor, value: Anchor, b: buffer)
  local offset = 5
  const exact_changed = not Loom.deep_equal(baseline.exact, value.exact)
  buffer_writebits(b, 0, 1, if exact_changed then 1 else 0)
  if exact_changed then
    buffer_writef32(b, offset, value.exact.X)
    offset += 4
    buffer_writef32(b, offset, value.exact.Y)
    offset += 4
    buffer_writef32(b, offset, value.exact.Z)
    offset += 4
    const qx, qy, qz, qw = Loom.cframe_to_quaternion(value.exact)
    buffer_writef32(b, offset, qx)
    offset += 4
    buffer_writef32(b, offset, qy)
    offset += 4
    buffer_writef32(b, offset, qz)
    offset += 4
    buffer_writef32(b, offset, qw)
    offset += 4
  end
  const approximate_changed = not Loom.deep_equal(baseline.approximate, value.approximate)
  buffer_writebits(b, 1, 1, if approximate_changed then 1 else 0)
  if approximate_changed then
    buffer_writef32(b, offset, value.approximate.X)
    offset += 4
    buffer_writef32(b, offset, value.approximate.Y)
    offset += 4
    buffer_writef32(b, offset, value.approximate.Z)
    offset += 4
    buffer_writebits(b, 2, 32, Loom.pack_quaternion(Loom.cframe_to_quaternion(value.approximate)))
  end
  return offset
end
const function Anchor_diff_binary_attempt(baseline: Anchor, value: Anchor, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Anchor_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Anchor_diff_binary_attempt(baseline, value, size * 2)
end
const function Anchor_diff_binary(baseline: Anchor, value: Anchor): Loom.Serialized
  const baseline_s = Anchor_serialize_binary(baseline)
  const value_s = Anchor_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 5
  const scratch, written = Anchor_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Anchor_apply_diff_binary_read(baseline: Anchor, diff: Loom.Serialized): Loom.Result<Anchor, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 5 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 5
  const exact_changed = buffer_readbits(b, 0, 1) == 1
  local exact = nil
  if exact_changed then
    const read_0 = buffer_readf32(b, offset)
    offset += 4
    const read_1 = buffer_readf32(b, offset)
    offset += 4
    const read_2 = buffer_readf32(b, offset)
    offset += 4
    const read_3 = buffer_readf32(b, offset)
    offset += 4
    const read_4 = buffer_readf32(b, offset)
    offset += 4
    const read_5 = buffer_readf32(b, offset)
    offset += 4
    const read_6 = buffer_readf32(b, offset)
    offset += 4
    exact = CFrame.new(read_0, read_1, read_2, read_3, read_4, read_5, read_6)
  else
    exact = baseline.exact
  end
  const approximate_changed = buffer_readbits(b, 1, 1) == 1
  local approximate = nil
  if approximate_changed then
    const read_7 = buffer_readf32(b, offset)
    offset += 4
    const read_8 = buffer_readf32(b, offset)
    offset += 4
    const read_9 = buffer_readf32(b, offset)
    offset += 4
    approximate = CFrame.new(read_7, read_8, read_9, Loom.unpack_quaternion(buffer_readbits(b, 2, 32)))
  else
    approximate = baseline.approximate
  end
  return { ok = true, value = { exact = exact, approximate = approximate } }
end
const function Anchor_apply_diff_binary(baseline: Anchor, diff: Loom.Serialized): Loom.Result<Anchor, Loom.DeserializeError>
  const ok, result = pcall(Anchor_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Anchor_serializer: Loom.Serializer<Anchor> = { serialize = Anchor_serialize_binary, deserialize = Anchor_deserialize_binary, diff = Anchor_diff_binary, applyDiff = Anchor_apply_diff_binary }