const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writeu8 = buffer.writeu8
const buffer_writeu32 = buffer.writeu32
const buffer_writestring = buffer.writestring
const buffer_len = buffer.len
const buffer_readu8 = buffer.readu8
const buffer_readu32 = buffer.readu32
const buffer_readstring = buffer.readstring
const buffer_writebits = buffer.writebits
const buffer_copy = buffer.copy
const buffer_readbits = buffer.readbits
const buffer_writef32 = buffer.writef32
const buffer_readf32 = buffer.readf32
type Entry = {
  read level: number,
  read label: string,
}
const function Entry_serialize_binary(value: Entry): Loom.Serialized
  const b = buffer_create(1 + 4 + #value.label)
  buffer_writeu8(b, 0, value.level)
  const label_value = value.label
  const label_length = #label_value
  buffer_writeu32(b, 1, label_length)
  local offset = 5
  buffer_writestring(b, offset, label_value)
  offset += label_length
  return { buffer = b }
end
const function Entry_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Entry, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 5 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const label_length = buffer_readu32(b, 1)
  local offset = 5
  if buffer_len(b) < offset + label_length then
    return { ok = false, error = { kind = "invalid_length", field = "label" } }
  end
  const label = buffer_readstring(b, offset, label_length)
  offset += label_length
  return { ok = true, value = { level = buffer_readu8(b, 0), label = label } }
end
const function Entry_diff_binary_write(baseline: Entry, value: Entry, b: buffer)
  local offset = 1
  const level_changed = not Loom.deep_equal(baseline.level, value.level)
  buffer_writebits(b, 0, 1, if level_changed then 1 else 0)
  if level_changed then
    buffer_writeu8(b, offset, value.level)
    offset += 1
  end
  const label_changed = not Loom.deep_equal(baseline.label, value.label)
  buffer_writebits(b, 1, 1, if label_changed then 1 else 0)
  if label_changed then
    const label_value = value.label
    const label_length = #label_value
    buffer_writeu32(b, offset, label_length)
    offset += 4
    buffer_writestring(b, offset, label_value)
    offset += label_length
  end
  return offset
end
const function Entry_diff_binary_attempt(baseline: Entry, value: Entry, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Entry_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Entry_diff_binary_attempt(baseline, value, size * 2)
end
const function Entry_diff_binary(baseline: Entry, value: Entry): Loom.Serialized
  const baseline_s = Entry_serialize_binary(baseline)
  const value_s = Entry_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written = Entry_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Entry_apply_diff_binary_read(baseline: Entry, diff: Loom.Serialized): Loom.Result<Entry, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 1
  const level_changed = buffer_readbits(b, 0, 1) == 1
  local level = nil
  if level_changed then
    const read_0 = buffer_readu8(b, offset)
    offset += 1
    level = read_0
  else
    level = baseline.level
  end
  const label_changed = buffer_readbits(b, 1, 1) == 1
  local label = nil
  if label_changed then
    const read_1 = buffer_readu32(b, offset)
    offset += 4
    const label_2_length = read_1
    if buffer_len(b) < offset + label_2_length then
      return { ok = false, error = { kind = "invalid_length", field = "label" } }
    end
    const label_2 = buffer_readstring(b, offset, label_2_length)
    offset += label_2_length
    label = label_2
  else
    label = baseline.label
  end
  return { ok = true, value = { level = level, label = label } }
end
const function Entry_apply_diff_binary(baseline: Entry, diff: Loom.Serialized): Loom.Result<Entry, Loom.DeserializeError>
  const ok, result = pcall(Entry_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Entry_serializer: Loom.Serializer<Entry> = { serialize = Entry_serialize_binary, deserialize = Entry_deserialize_binary, diff = Entry_diff_binary, applyDiff = Entry_apply_diff_binary }
type Registry = {
  read flags: Record<string, boolean>,
  read entries: Record<number, Entry>,
  read lists: Record<string, { number }>,
}
const function Registry_serialize_binary(value: Registry): Loom.Serialized
  local size = 0
  size += 4
  local flags_measured = 0
  for flags_k, flags_v in value.flags do
    flags_measured += 1
    size += 4 + #flags_k
  end
  size += (flags_measured + 7) // 8
  size += 4
  local entries_measured = 0
  for entries_k, entries_v in value.entries do
    entries_measured += 1
    size += 4
    size += 1
    size += 4 + #entries_v.label
  end
  size += 4
  local lists_measured = 0
  for lists_k, lists_v in value.lists do
    lists_measured += 1
    size += 4 + #lists_k
    size += 4 + #lists_v * 4
  end
  const b = buffer_create(size)
  local flags_written = 0
  for flags_key in value.flags do
    flags_written += 1
  end
  buffer_writeu32(b, 0, flags_written)
  local offset = 4
  const flags_bits = offset * 8
  offset += (flags_written + 7) // 8
  local flags_index = 1
  for flags_k_out, flags_v_out in value.flags do
    const flags_k_length = #flags_k_out
    buffer_writeu32(b, offset, flags_k_length)
    offset += 4
    buffer_writestring(b, offset, flags_k_out)
    offset += flags_k_length
    buffer_writebits(b, flags_bits + (flags_index - 1), 1, if flags_v_out then 1 else 0)
    flags_index += 1
  end
  local entries_written = 0
  for entries_key in value.entries do
    entries_written += 1
  end
  buffer_writeu32(b, offset, entries_written)
  offset += 4
  for entries_k_out, entries_v_out in value.entries do
    buffer_writef32(b, offset, entries_k_out)
    offset += 4
    buffer_writeu8(b, offset, entries_v_out.level)
    offset += 1
    const label_value = entries_v_out.label
    const label_length = #label_value
    buffer_writeu32(b, offset, label_length)
    offset += 4
    buffer_writestring(b, offset, label_value)
    offset += label_length
  end
  local lists_written = 0
  for lists_key in value.lists do
    lists_written += 1
  end
  buffer_writeu32(b, offset, lists_written)
  offset += 4
  for lists_k_out, lists_v_out in value.lists do
    const lists_k_length = #lists_k_out
    buffer_writeu32(b, offset, lists_k_length)
    offset += 4
    buffer_writestring(b, offset, lists_k_out)
    offset += lists_k_length
    buffer_writeu32(b, offset, #lists_v_out)
    offset += 4
    for i = 1, #lists_v_out do
      const lists_v_item = lists_v_out[i]
      buffer_writef32(b, offset, lists_v_item)
      offset += 4
    end
  end
  return { buffer = b }
end
const function Registry_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Registry, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 12 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const flags_count = buffer_readu32(b, 0)
  local offset = 4
  const flags_bits = offset * 8
  offset += (flags_count + 7) // 8
  const flags = {}
  for i = 1, flags_count do
    const read_0 = buffer_readu32(b, offset)
    offset += 4
    const flags_k_length = read_0
    if buffer_len(b) < offset + flags_k_length then
      return { ok = false, error = { kind = "invalid_length", field = "flags[k]" } }
    end
    const flags_k = buffer_readstring(b, offset, flags_k_length)
    offset += flags_k_length
    flags[flags_k] = buffer_readbits(b, flags_bits + (i - 1), 1) == 1
  end
  const read_1 = buffer_readu32(b, offset)
  offset += 4
  const entries_count = read_1
  const entries = {}
  for i_2 = 1, entries_count do
    const read_2 = buffer_readf32(b, offset)
    offset += 4
    const read_3 = buffer_readu8(b, offset)
    offset += 1
    const read_4 = buffer_readu32(b, offset)
    offset += 4
    const label_length = read_4
    if buffer_len(b) < offset + label_length then
      return { ok = false, error = { kind = "invalid_length", field = "entries[v].label" } }
    end
    const label = buffer_readstring(b, offset, label_length)
    offset += label_length
    entries[read_2] = { level = read_3, label = label }
  end
  const read_5 = buffer_readu32(b, offset)
  offset += 4
  const lists_count = read_5
  const lists = {}
  for i_3 = 1, lists_count do
    const read_6 = buffer_readu32(b, offset)
    offset += 4
    const lists_k_length = read_6
    if buffer_len(b) < offset + lists_k_length then
      return { ok = false, error = { kind = "invalid_length", field = "lists[k]" } }
    end
    const lists_k = buffer_readstring(b, offset, lists_k_length)
    offset += lists_k_length
    const read_7 = buffer_readu32(b, offset)
    offset += 4
    const lists_v_count = read_7
    if buffer_len(b) < offset + lists_v_count * 4 then
      return { ok = false, error = { kind = "invalid_length", field = "lists[v]" } }
    end
    const lists_v = {}
    for i_4 = 1, lists_v_count do
      const read_8 = buffer_readf32(b, offset)
      offset += 4
      lists_v[i_4] = read_8
    end
    lists[lists_k] = lists_v
  end
  return { ok = true, value = { flags = flags, entries = entries, lists = lists } }
end
const function Registry_diff_binary_write(baseline: Registry, value: Registry, b: buffer)
  local offset = 0
  const flags_removed = {}
  const flags_added = {}
  const flags_changed = {}
  for flags_current_key, flags_current_entry in value.flags do
    const flags_baseline_entry = baseline.flags[flags_current_key]
    if flags_baseline_entry == nil then
      table.insert(flags_added, flags_current_key)
    else
      if not Loom.deep_equal(flags_baseline_entry, flags_current_entry) then
        table.insert(flags_changed, flags_current_key)
      end
    end
  end
  for flags_baseline_key in baseline.flags do
    if value.flags[flags_baseline_key] == nil then
      table.insert(flags_removed, flags_baseline_key)
    end
  end
  buffer_writeu32(b, offset, #flags_removed)
  offset += 4
  for i = 1, #flags_removed do
    const flags_key_i = flags_removed[i]
    const flags_k_length = #flags_key_i
    buffer_writeu32(b, offset, flags_k_length)
    offset += 4
    buffer_writestring(b, offset, flags_key_i)
    offset += flags_k_length
  end
  buffer_writeu32(b, offset, #flags_added)
  offset += 4
  for i_2 = 1, #flags_added do
    const flags_key_i_2 = flags_added[i_2]
    const flags_k_length_2 = #flags_key_i_2
    buffer_writeu32(b, offset, flags_k_length_2)
    offset += 4
    buffer_writestring(b, offset, flags_key_i_2)
    offset += flags_k_length_2
    buffer_writebits(b, 0, 1, if value.flags[flags_key_i_2] then 1 else 0)
  end
  buffer_writeu32(b, offset, #flags_changed)
  offset += 4
  const flags_entry_bits = offset * 8
  offset += (#flags_changed * 2 + 7) // 8
  for i_3 = 1, #flags_changed do
    const flags_key_i_3 = flags_changed[i_3]
    const flags_k_length_3 = #flags_key_i_3
    buffer_writeu32(b, offset, flags_k_length_3)
    offset += 4
    buffer_writestring(b, offset, flags_key_i_3)
    offset += flags_k_length_3
    const flags_v_changed = not Loom.deep_equal(baseline.flags[flags_key_i_3], value.flags[flags_key_i_3])
    buffer_writebits(b, flags_entry_bits + (i_3 - 1) * 2, 1, if flags_v_changed then 1 else 0)
    if flags_v_changed then
      buffer_writebits(b, flags_entry_bits + (i_3 - 1) * 2 + 1, 1, if value.flags[flags_key_i_3] then 1 else 0)
    end
  end
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
  for i_4 = 1, #entries_removed do
    const entries_key_i_4 = entries_removed[i_4]
    buffer_writef32(b, offset, entries_key_i_4)
    offset += 4
  end
  buffer_writeu32(b, offset, #entries_added)
  offset += 4
  for i_5 = 1, #entries_added do
    const entries_key_i_5 = entries_added[i_5]
    buffer_writef32(b, offset, entries_key_i_5)
    offset += 4
    buffer_writeu8(b, offset, value.entries[entries_key_i_5].level)
    offset += 1
    const label_value = value.entries[entries_key_i_5].label
    const label_length = #label_value
    buffer_writeu32(b, offset, label_length)
    offset += 4
    buffer_writestring(b, offset, label_value)
    offset += label_length
  end
  buffer_writeu32(b, offset, #entries_changed)
  offset += 4
  const entries_entry_bits = offset * 8
  offset += (#entries_changed * 2 + 7) // 8
  for i_6 = 1, #entries_changed do
    const entries_key_i_6 = entries_changed[i_6]
    buffer_writef32(b, offset, entries_key_i_6)
    offset += 4
    const level_changed = not Loom.deep_equal(baseline.entries[entries_key_i_6].level, value.entries[entries_key_i_6].level)
    buffer_writebits(b, entries_entry_bits + (i_6 - 1) * 2, 1, if level_changed then 1 else 0)
    if level_changed then
      buffer_writeu8(b, offset, value.entries[entries_key_i_6].level)
      offset += 1
    end
    const label_changed = not Loom.deep_equal(baseline.entries[entries_key_i_6].label, value.entries[entries_key_i_6].label)
    buffer_writebits(b, entries_entry_bits + (i_6 - 1) * 2 + 1, 1, if label_changed then 1 else 0)
    if label_changed then
      const label_value_2 = value.entries[entries_key_i_6].label
      const label_length_2 = #label_value_2
      buffer_writeu32(b, offset, label_length_2)
      offset += 4
      buffer_writestring(b, offset, label_value_2)
      offset += label_length_2
    end
  end
  const lists_removed = {}
  const lists_added = {}
  const lists_changed = {}
  for lists_current_key, lists_current_entry in value.lists do
    const lists_baseline_entry = baseline.lists[lists_current_key]
    if lists_baseline_entry == nil then
      table.insert(lists_added, lists_current_key)
    else
      if not Loom.deep_equal(lists_baseline_entry, lists_current_entry) then
        table.insert(lists_changed, lists_current_key)
      end
    end
  end
  for lists_baseline_key in baseline.lists do
    if value.lists[lists_baseline_key] == nil then
      table.insert(lists_removed, lists_baseline_key)
    end
  end
  buffer_writeu32(b, offset, #lists_removed)
  offset += 4
  for i_7 = 1, #lists_removed do
    const lists_key_i_7 = lists_removed[i_7]
    const lists_k_length = #lists_key_i_7
    buffer_writeu32(b, offset, lists_k_length)
    offset += 4
    buffer_writestring(b, offset, lists_key_i_7)
    offset += lists_k_length
  end
  buffer_writeu32(b, offset, #lists_added)
  offset += 4
  for i_8 = 1, #lists_added do
    const lists_key_i_8 = lists_added[i_8]
    const lists_k_length_2 = #lists_key_i_8
    buffer_writeu32(b, offset, lists_k_length_2)
    offset += 4
    buffer_writestring(b, offset, lists_key_i_8)
    offset += lists_k_length_2
    buffer_writeu32(b, offset, #value.lists[lists_key_i_8])
    offset += 4
    for i_9 = 1, #value.lists[lists_key_i_8] do
      const lists_v_item = value.lists[lists_key_i_8][i_9]
      buffer_writef32(b, offset, lists_v_item)
      offset += 4
    end
  end
  buffer_writeu32(b, offset, #lists_changed)
  offset += 4
  const lists_entry_bits = offset * 8
  offset += (#lists_changed + 7) // 8
  for i_10 = 1, #lists_changed do
    const lists_key_i_10 = lists_changed[i_10]
    const lists_k_length_3 = #lists_key_i_10
    buffer_writeu32(b, offset, lists_k_length_3)
    offset += 4
    buffer_writestring(b, offset, lists_key_i_10)
    offset += lists_k_length_3
    const lists_v_length_changed = #baseline.lists[lists_key_i_10] ~= #value.lists[lists_key_i_10]
    buffer_writebits(b, lists_entry_bits + (i_10 - 1), 1, if lists_v_length_changed then 1 else 0)
    if lists_v_length_changed then
      buffer_writeu32(b, offset, #value.lists[lists_key_i_10])
      offset += 4
      for i_11 = 1, #value.lists[lists_key_i_10] do
        const lists_v_item_2 = value.lists[lists_key_i_10][i_11]
        buffer_writef32(b, offset, lists_v_item_2)
        offset += 4
      end
    else
      const lists_v_bits = offset * 8
      offset += (#value.lists[lists_key_i_10] + 7) // 8
      for i_12 = 1, #value.lists[lists_key_i_10] do
        const lists_v_element_changed = not Loom.deep_equal(baseline.lists[lists_key_i_10][i_12], value.lists[lists_key_i_10][i_12])
        buffer_writebits(b, lists_v_bits + (i_12 - 1), 1, if lists_v_element_changed then 1 else 0)
        if lists_v_element_changed then
          buffer_writef32(b, offset, value.lists[lists_key_i_10][i_12])
          offset += 4
        end
      end
    end
  end
  return offset
end
const function Registry_diff_binary_attempt(baseline: Registry, value: Registry, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Registry_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Registry_diff_binary_attempt(baseline, value, size * 2)
end
const function Registry_diff_binary(baseline: Registry, value: Registry): Loom.Serialized
  const baseline_s = Registry_serialize_binary(baseline)
  const value_s = Registry_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0)
  const scratch, written = Registry_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Registry_apply_diff_binary_read(baseline: Registry, diff: Loom.Serialized): Loom.Result<Registry, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 0 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 0
  const flags = {}
  for flags_copy_key, flags_copy_value in baseline.flags do
    flags[flags_copy_key] = flags_copy_value
  end
  const read_0 = buffer_readu32(b, offset)
  offset += 4
  const flags_removed_count = read_0
  for i = 1, flags_removed_count do
    const read_1 = buffer_readu32(b, offset)
    offset += 4
    const flags_k_length = read_1
    if buffer_len(b) < offset + flags_k_length then
      return { ok = false, error = { kind = "invalid_length", field = "flags[k]" } }
    end
    const flags_k = buffer_readstring(b, offset, flags_k_length)
    offset += flags_k_length
    const flags_removed_key = flags_k
    flags[flags_removed_key] = nil
  end
  const read_2 = buffer_readu32(b, offset)
  offset += 4
  const flags_added_count = read_2
  for i_2 = 1, flags_added_count do
    const read_3 = buffer_readu32(b, offset)
    offset += 4
    const flags_k_2_length = read_3
    if buffer_len(b) < offset + flags_k_2_length then
      return { ok = false, error = { kind = "invalid_length", field = "flags[k]" } }
    end
    const flags_k_2 = buffer_readstring(b, offset, flags_k_2_length)
    offset += flags_k_2_length
    const flags_added_key = flags_k_2
    flags[flags_added_key] = buffer_readbits(b, 0, 1) == 1
  end
  const read_4 = buffer_readu32(b, offset)
  offset += 4
  const flags_changed_count = read_4
  const flags_entry_bits = offset * 8
  offset += (flags_changed_count * 2 + 7) // 8
  for i_3 = 1, flags_changed_count do
    const read_5 = buffer_readu32(b, offset)
    offset += 4
    const flags_k_3_length = read_5
    if buffer_len(b) < offset + flags_k_3_length then
      return { ok = false, error = { kind = "invalid_length", field = "flags[k]" } }
    end
    const flags_k_3 = buffer_readstring(b, offset, flags_k_3_length)
    offset += flags_k_3_length
    const flags_changed_key = flags_k_3
    const flags_v_changed = buffer_readbits(b, flags_entry_bits + (i_3 - 1) * 2, 1) == 1
    local flags_v = nil
    if flags_v_changed then
      flags_v = buffer_readbits(b, flags_entry_bits + (i_3 - 1) * 2 + 1, 1) == 1
    else
      flags_v = baseline.flags[flags_changed_key]
    end
    flags[flags_changed_key] = flags_v
  end
  const entries = {}
  for entries_copy_key, entries_copy_value in baseline.entries do
    entries[entries_copy_key] = entries_copy_value
  end
  const read_6 = buffer_readu32(b, offset)
  offset += 4
  const entries_removed_count = read_6
  for i_4 = 1, entries_removed_count do
    const read_7 = buffer_readf32(b, offset)
    offset += 4
    const entries_removed_key = read_7
    entries[entries_removed_key] = nil
  end
  const read_8 = buffer_readu32(b, offset)
  offset += 4
  const entries_added_count = read_8
  for i_5 = 1, entries_added_count do
    const read_9 = buffer_readf32(b, offset)
    offset += 4
    const entries_added_key = read_9
    const read_10 = buffer_readu8(b, offset)
    offset += 1
    const read_11 = buffer_readu32(b, offset)
    offset += 4
    const label_length = read_11
    if buffer_len(b) < offset + label_length then
      return { ok = false, error = { kind = "invalid_length", field = "entries[v].label" } }
    end
    const label = buffer_readstring(b, offset, label_length)
    offset += label_length
    entries[entries_added_key] = { level = read_10, label = label }
  end
  const read_12 = buffer_readu32(b, offset)
  offset += 4
  const entries_changed_count = read_12
  const entries_entry_bits = offset * 8
  offset += (entries_changed_count * 2 + 7) // 8
  for i_6 = 1, entries_changed_count do
    const read_13 = buffer_readf32(b, offset)
    offset += 4
    const entries_changed_key = read_13
    const level_changed = buffer_readbits(b, entries_entry_bits + (i_6 - 1) * 2, 1) == 1
    local level = nil
    if level_changed then
      const read_14 = buffer_readu8(b, offset)
      offset += 1
      level = read_14
    else
      level = baseline.entries[entries_changed_key].level
    end
    const label_changed = buffer_readbits(b, entries_entry_bits + (i_6 - 1) * 2 + 1, 1) == 1
    local label_2 = nil
    if label_changed then
      const read_15 = buffer_readu32(b, offset)
      offset += 4
      const label_3_length = read_15
      if buffer_len(b) < offset + label_3_length then
        return { ok = false, error = { kind = "invalid_length", field = "entries[v].label" } }
      end
      const label_3 = buffer_readstring(b, offset, label_3_length)
      offset += label_3_length
      label_2 = label_3
    else
      label_2 = baseline.entries[entries_changed_key].label
    end
    entries[entries_changed_key] = { level = level, label = label_2 }
  end
  const lists = {}
  for lists_copy_key, lists_copy_value in baseline.lists do
    lists[lists_copy_key] = lists_copy_value
  end
  const read_16 = buffer_readu32(b, offset)
  offset += 4
  const lists_removed_count = read_16
  for i_7 = 1, lists_removed_count do
    const read_17 = buffer_readu32(b, offset)
    offset += 4
    const lists_k_length = read_17
    if buffer_len(b) < offset + lists_k_length then
      return { ok = false, error = { kind = "invalid_length", field = "lists[k]" } }
    end
    const lists_k = buffer_readstring(b, offset, lists_k_length)
    offset += lists_k_length
    const lists_removed_key = lists_k
    lists[lists_removed_key] = nil
  end
  const read_18 = buffer_readu32(b, offset)
  offset += 4
  const lists_added_count = read_18
  for i_8 = 1, lists_added_count do
    const read_19 = buffer_readu32(b, offset)
    offset += 4
    const lists_k_2_length = read_19
    if buffer_len(b) < offset + lists_k_2_length then
      return { ok = false, error = { kind = "invalid_length", field = "lists[k]" } }
    end
    const lists_k_2 = buffer_readstring(b, offset, lists_k_2_length)
    offset += lists_k_2_length
    const lists_added_key = lists_k_2
    const read_20 = buffer_readu32(b, offset)
    offset += 4
    const lists_v_count = read_20
    if buffer_len(b) < offset + lists_v_count * 4 then
      return { ok = false, error = { kind = "invalid_length", field = "lists[v]" } }
    end
    const lists_v = {}
    for i_9 = 1, lists_v_count do
      const read_21 = buffer_readf32(b, offset)
      offset += 4
      lists_v[i_9] = read_21
    end
    lists[lists_added_key] = lists_v
  end
  const read_22 = buffer_readu32(b, offset)
  offset += 4
  const lists_changed_count = read_22
  const lists_entry_bits = offset * 8
  offset += (lists_changed_count + 7) // 8
  for i_10 = 1, lists_changed_count do
    const read_23 = buffer_readu32(b, offset)
    offset += 4
    const lists_k_3_length = read_23
    if buffer_len(b) < offset + lists_k_3_length then
      return { ok = false, error = { kind = "invalid_length", field = "lists[k]" } }
    end
    const lists_k_3 = buffer_readstring(b, offset, lists_k_3_length)
    offset += lists_k_3_length
    const lists_changed_key = lists_k_3
    const lists_v_length_changed = buffer_readbits(b, lists_entry_bits + (i_10 - 1), 1) == 1
    local lists_v_2 = nil
    if lists_v_length_changed then
      const read_24 = buffer_readu32(b, offset)
      offset += 4
      const lists_v_3_count = read_24
      if buffer_len(b) < offset + lists_v_3_count * 4 then
        return { ok = false, error = { kind = "invalid_length", field = "lists[v]" } }
      end
      const lists_v_3 = {}
      for i_11 = 1, lists_v_3_count do
        const read_25 = buffer_readf32(b, offset)
        offset += 4
        lists_v_3[i_11] = read_25
      end
      lists_v_2 = lists_v_3
    else
      const lists_v_count_2 = #baseline.lists[lists_changed_key]
      const lists_v_bits = offset * 8
      offset += (lists_v_count_2 + 7) // 8
      lists_v_2 = {}
      for i_12 = 1, lists_v_count_2 do
        const lists_v_element_changed = buffer_readbits(b, lists_v_bits + (i_12 - 1), 1) == 1
        local lists_v_element = nil
        if lists_v_element_changed then
          const read_26 = buffer_readf32(b, offset)
          offset += 4
          lists_v_element = read_26
        else
          lists_v_element = baseline.lists[lists_changed_key][i_12]
        end
        lists_v_2[i_12] = lists_v_element
      end
    end
    lists[lists_changed_key] = lists_v_2
  end
  return { ok = true, value = { flags = flags, entries = entries, lists = lists } }
end
const function Registry_apply_diff_binary(baseline: Registry, diff: Loom.Serialized): Loom.Result<Registry, Loom.DeserializeError>
  const ok, result = pcall(Registry_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Registry_serializer: Loom.Serializer<Registry> = { serialize = Registry_serialize_binary, deserialize = Registry_deserialize_binary, diff = Registry_diff_binary, applyDiff = Registry_apply_diff_binary }