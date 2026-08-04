const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writeu32 = buffer.writeu32
const buffer_writeu8 = buffer.writeu8
const buffer_len = buffer.len
const buffer_readu32 = buffer.readu32
const buffer_readu8 = buffer.readu8
const buffer_writebits = buffer.writebits
const buffer_copy = buffer.copy
const buffer_readbits = buffer.readbits
type Scores = {
  read values: { number },
}
const function Scores_serialize_binary(value: Scores): Loom.Serialized
  const b = buffer_create(4 + #value.values)
  buffer_writeu32(b, 0, #value.values)
  local offset = 4
  for i = 1, #value.values do
    const values_item = value.values[i]
    buffer_writeu8(b, offset, values_item)
    offset += 1
  end
  return { buffer = b }
end
const function Scores_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Scores, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 4 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const values_count = buffer_readu32(b, 0)
  local offset = 4
  if buffer_len(b) < offset + values_count then
    return { ok = false, error = { kind = "invalid_length", field = "values" } }
  end
  const values = {}
  for i = 1, values_count do
    const read_0 = buffer_readu8(b, offset)
    offset += 1
    values[i] = read_0
  end
  return { ok = true, value = { values = values } }
end
const function Scores_diff_binary_write(baseline: Scores, value: Scores, b: buffer)
  local offset = 1
  const values_length_changed = #baseline.values ~= #value.values
  buffer_writebits(b, 0, 1, if values_length_changed then 1 else 0)
  if values_length_changed then
    buffer_writeu32(b, offset, #value.values)
    offset += 4
    for i = 1, #value.values do
      const values_item = value.values[i]
      buffer_writeu8(b, offset, values_item)
      offset += 1
    end
  else
    const values_bits = offset * 8
    offset += (#value.values + 7) // 8
    for i_2 = 1, #value.values do
      const values_element_changed = not Loom.deep_equal(baseline.values[i_2], value.values[i_2])
      buffer_writebits(b, values_bits + (i_2 - 1), 1, if values_element_changed then 1 else 0)
      if values_element_changed then
        buffer_writeu8(b, offset, value.values[i_2])
        offset += 1
      end
    end
  end
  return offset
end
const function Scores_diff_binary_attempt(baseline: Scores, value: Scores, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Scores_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Scores_diff_binary_attempt(baseline, value, size * 2)
end
const function Scores_diff_binary(baseline: Scores, value: Scores): Loom.Serialized
  const baseline_s = Scores_serialize_binary(baseline)
  const value_s = Scores_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written = Scores_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Scores_apply_diff_binary_read(baseline: Scores, diff: Loom.Serialized): Loom.Result<Scores, Loom.DeserializeError>
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
    if buffer_len(b) < offset + values_2_count then
      return { ok = false, error = { kind = "invalid_length", field = "values" } }
    end
    const values_2 = {}
    for i = 1, values_2_count do
      const read_1 = buffer_readu8(b, offset)
      offset += 1
      values_2[i] = read_1
    end
    values = values_2
  else
    const values_count = #baseline.values
    const values_bits = offset * 8
    offset += (values_count + 7) // 8
    values = {}
    for i_2 = 1, values_count do
      const values_element_changed = buffer_readbits(b, values_bits + (i_2 - 1), 1) == 1
      local values_element = nil
      if values_element_changed then
        const read_2 = buffer_readu8(b, offset)
        offset += 1
        values_element = read_2
      else
        values_element = baseline.values[i_2]
      end
      values[i_2] = values_element
    end
  end
  return { ok = true, value = { values = values } }
end
const function Scores_apply_diff_binary(baseline: Scores, diff: Loom.Serialized): Loom.Result<Scores, Loom.DeserializeError>
  const ok, result = pcall(Scores_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Scores_serializer: Loom.Serializer<Scores> = { serialize = Scores_serialize_binary, deserialize = Scores_deserialize_binary, diff = Scores_diff_binary, applyDiff = Scores_apply_diff_binary }