const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writeu32 = buffer.writeu32
const buffer_writestring = buffer.writestring
const buffer_writef32 = buffer.writef32
const buffer_len = buffer.len
const buffer_readu32 = buffer.readu32
const buffer_readstring = buffer.readstring
const buffer_readf32 = buffer.readf32
const buffer_writebits = buffer.writebits
const buffer_copy = buffer.copy
const buffer_readbits = buffer.readbits
type Scores = {
  read entries: Record<string, number>,
}
const function Scores_serialize_binary(value: Scores): Loom.Serialized
  local size = 0
  size += 4
  local entries_measured = 0
  for entries_k, entries_v in value.entries do
    entries_measured += 1
    size += 4 + #entries_k
    size += 4
  end
  const b = buffer_create(size)
  local entries_written = 0
  for entries_key in value.entries do
    entries_written += 1
  end
  buffer_writeu32(b, 0, entries_written)
  local offset = 4
  for entries_k_out, entries_v_out in value.entries do
    const entries_k_length = #entries_k_out
    buffer_writeu32(b, offset, entries_k_length)
    offset += 4
    buffer_writestring(b, offset, entries_k_out)
    offset += entries_k_length
    buffer_writef32(b, offset, entries_v_out)
    offset += 4
  end
  return { buffer = b }
end
const function Scores_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Scores, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 4 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const entries_count = buffer_readu32(b, 0)
  local offset = 4
  const entries = {}
  for i = 1, entries_count do
    const read_0 = buffer_readu32(b, offset)
    offset += 4
    const entries_k_length = read_0
    if buffer_len(b) < offset + entries_k_length then
      return { ok = false, error = { kind = "invalid_length", field = "entries[k]" } }
    end
    const entries_k = buffer_readstring(b, offset, entries_k_length)
    offset += entries_k_length
    const read_1 = buffer_readf32(b, offset)
    offset += 4
    entries[entries_k] = read_1
  end
  return { ok = true, value = { entries = entries } }
end
const function Scores_diff_binary_write(baseline: Scores, value: Scores, b: buffer)
  local offset = 0
  const entries_removed = {}
  const entries_added = {}
  const entries_changed = {}
  for entries_current_key, entries_current_entry in value.entries do
    const entries_baseline_entry = baseline.entries[entries_current_key]
    if entries_baseline_entry == nil then
      table.insert(entries_added, entries_current_key)
    else
      if not Loom.deep_equal(entries_baseline_entry, entries_current_entry) then
        table.insert(entries_changed, entries_current_key)
      end
    end
  end
  for entries_baseline_key in baseline.entries do
    if value.entries[entries_baseline_key] == nil then
      table.insert(entries_removed, entries_baseline_key)
    end
  end
  buffer_writeu32(b, offset, #entries_removed)
  offset += 4
  for i = 1, #entries_removed do
    const entries_key_i = entries_removed[i]
    const entries_k_length = #entries_key_i
    buffer_writeu32(b, offset, entries_k_length)
    offset += 4
    buffer_writestring(b, offset, entries_key_i)
    offset += entries_k_length
  end
  buffer_writeu32(b, offset, #entries_added)
  offset += 4
  for i_2 = 1, #entries_added do
    const entries_key_i_2 = entries_added[i_2]
    const entries_k_length_2 = #entries_key_i_2
    buffer_writeu32(b, offset, entries_k_length_2)
    offset += 4
    buffer_writestring(b, offset, entries_key_i_2)
    offset += entries_k_length_2
    buffer_writef32(b, offset, value.entries[entries_key_i_2])
    offset += 4
  end
  buffer_writeu32(b, offset, #entries_changed)
  offset += 4
  const entries_entry_bits = offset * 8
  offset += (#entries_changed + 7) // 8
  for i_3 = 1, #entries_changed do
    const entries_key_i_3 = entries_changed[i_3]
    const entries_k_length_3 = #entries_key_i_3
    buffer_writeu32(b, offset, entries_k_length_3)
    offset += 4
    buffer_writestring(b, offset, entries_key_i_3)
    offset += entries_k_length_3
    const entries_v_changed = not Loom.deep_equal(baseline.entries[entries_key_i_3], value.entries[entries_key_i_3])
    buffer_writebits(b, entries_entry_bits + (i_3 - 1), 1, if entries_v_changed then 1 else 0)
    if entries_v_changed then
      buffer_writef32(b, offset, value.entries[entries_key_i_3])
      offset += 4
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
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0)
  const scratch, written = Scores_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Scores_apply_diff_binary_read(baseline: Scores, diff: Loom.Serialized): Loom.Result<Scores, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 0 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 0
  const entries = {}
  for entries_copy_key, entries_copy_value in baseline.entries do
    entries[entries_copy_key] = entries_copy_value
  end
  const read_0 = buffer_readu32(b, offset)
  offset += 4
  const entries_removed_count = read_0
  for i = 1, entries_removed_count do
    const read_1 = buffer_readu32(b, offset)
    offset += 4
    const entries_k_length = read_1
    if buffer_len(b) < offset + entries_k_length then
      return { ok = false, error = { kind = "invalid_length", field = "entries[k]" } }
    end
    const entries_k = buffer_readstring(b, offset, entries_k_length)
    offset += entries_k_length
    const entries_removed_key = entries_k
    entries[entries_removed_key] = nil
  end
  const read_2 = buffer_readu32(b, offset)
  offset += 4
  const entries_added_count = read_2
  for i_2 = 1, entries_added_count do
    const read_3 = buffer_readu32(b, offset)
    offset += 4
    const entries_k_2_length = read_3
    if buffer_len(b) < offset + entries_k_2_length then
      return { ok = false, error = { kind = "invalid_length", field = "entries[k]" } }
    end
    const entries_k_2 = buffer_readstring(b, offset, entries_k_2_length)
    offset += entries_k_2_length
    const entries_added_key = entries_k_2
    const read_4 = buffer_readf32(b, offset)
    offset += 4
    entries[entries_added_key] = read_4
  end
  const read_5 = buffer_readu32(b, offset)
  offset += 4
  const entries_changed_count = read_5
  const entries_entry_bits = offset * 8
  offset += (entries_changed_count + 7) // 8
  for i_3 = 1, entries_changed_count do
    const read_6 = buffer_readu32(b, offset)
    offset += 4
    const entries_k_3_length = read_6
    if buffer_len(b) < offset + entries_k_3_length then
      return { ok = false, error = { kind = "invalid_length", field = "entries[k]" } }
    end
    const entries_k_3 = buffer_readstring(b, offset, entries_k_3_length)
    offset += entries_k_3_length
    const entries_changed_key = entries_k_3
    const entries_v_changed = buffer_readbits(b, entries_entry_bits + (i_3 - 1), 1) == 1
    local entries_v = nil
    if entries_v_changed then
      const read_7 = buffer_readf32(b, offset)
      offset += 4
      entries_v = read_7
    else
      entries_v = baseline.entries[entries_changed_key]
    end
    entries[entries_changed_key] = entries_v
  end
  return { ok = true, value = { entries = entries } }
end
const function Scores_apply_diff_binary(baseline: Scores, diff: Loom.Serialized): Loom.Result<Scores, Loom.DeserializeError>
  const ok, result = pcall(Scores_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Scores_serializer: Loom.Serializer<Scores> = { serialize = Scores_serialize_binary, deserialize = Scores_deserialize_binary, diff = Scores_diff_binary, applyDiff = Scores_apply_diff_binary }