const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writeu32 = buffer.writeu32
const buffer_writebits = buffer.writebits
const buffer_len = buffer.len
const buffer_readu32 = buffer.readu32
const buffer_readbits = buffer.readbits
const buffer_copy = buffer.copy
type Flags = {
  read values: { boolean },
}
const function Flags_serialize_binary(value: Flags): Loom.Serialized
  const b = buffer_create(4 + (#value.values + 7) // 8)
  buffer_writeu32(b, 0, #value.values)
  local offset = 4
  const values_bits = offset * 8
  offset += (#value.values + 7) // 8
  for i = 1, #value.values do
    const values_item = value.values[i]
    buffer_writebits(b, values_bits + (i - 1), 1, if values_item then 1 else 0)
  end
  return { buffer = b }
end
const function Flags_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Flags, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 4 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const values_count = buffer_readu32(b, 0)
  local offset = 4
  const values_bits = offset * 8
  offset += (values_count + 7) // 8
  const values = {}
  for i = 1, values_count do
    values[i] = buffer_readbits(b, values_bits + (i - 1), 1) == 1
  end
  return { ok = true, value = { values = values } }
end
const function Flags_diff_binary_write(baseline: Flags, value: Flags, b: buffer)
  local offset = 1
  const values_length_changed = #baseline.values ~= #value.values
  buffer_writebits(b, 0, 1, if values_length_changed then 1 else 0)
  if values_length_changed then
    buffer_writeu32(b, offset, #value.values)
    offset += 4
    const values_bits = offset * 8
    offset += (#value.values + 7) // 8
    for i = 1, #value.values do
      const values_item = value.values[i]
      buffer_writebits(b, values_bits + (i - 1), 1, if values_item then 1 else 0)
    end
  else
    const values_bits_2 = offset * 8
    offset += (#value.values * 2 + 7) // 8
    for i_2 = 1, #value.values do
      const values_element_changed = not Loom.deep_equal(baseline.values[i_2], value.values[i_2])
      buffer_writebits(b, values_bits_2 + (i_2 - 1) * 2, 1, if values_element_changed then 1 else 0)
      if values_element_changed then
        buffer_writebits(b, values_bits_2 + (i_2 - 1) * 2 + 1, 1, if value.values[i_2] then 1 else 0)
      end
    end
  end
  return offset
end
const function Flags_diff_binary_attempt(baseline: Flags, value: Flags, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Flags_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Flags_diff_binary_attempt(baseline, value, size * 2)
end
const function Flags_diff_binary(baseline: Flags, value: Flags): Loom.Serialized
  const baseline_s = Flags_serialize_binary(baseline)
  const value_s = Flags_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written = Flags_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Flags_apply_diff_binary_read(baseline: Flags, diff: Loom.Serialized): Loom.Result<Flags, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 1
  const values_length_changed = buffer_readbits(b, 0, 1) == 1
  local values = nil
  if values_length_changed then
    const read_0 = buffer_readu32(b, offset)
    offset += 4
    const values_2_count = read_0
    const values_2_bits = offset * 8
    offset += (values_2_count + 7) // 8
    const values_2 = {}
    for i = 1, values_2_count do
      values_2[i] = buffer_readbits(b, values_2_bits + (i - 1), 1) == 1
    end
    values = values_2
  else
    const values_count = #baseline.values
    const values_bits = offset * 8
    offset += (values_count * 2 + 7) // 8
    values = {}
    for i_2 = 1, values_count do
      const values_element_changed = buffer_readbits(b, values_bits + (i_2 - 1) * 2, 1) == 1
      local values_element = nil
      if values_element_changed then
        values_element = buffer_readbits(b, values_bits + (i_2 - 1) * 2 + 1, 1) == 1
      else
        values_element = baseline.values[i_2]
      end
      values[i_2] = values_element
    end
  end
  return { ok = true, value = { values = values } }
end
const function Flags_apply_diff_binary(baseline: Flags, diff: Loom.Serialized): Loom.Result<Flags, Loom.DeserializeError>
  const ok, result = pcall(Flags_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Flags_serializer: Loom.Serializer<Flags> = { serialize = Flags_serialize_binary, deserialize = Flags_deserialize_binary, diff = Flags_diff_binary, applyDiff = Flags_apply_diff_binary }