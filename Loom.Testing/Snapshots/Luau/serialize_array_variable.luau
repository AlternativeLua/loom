const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writeu32 = buffer.writeu32
const buffer_writestring = buffer.writestring
const buffer_writei16 = buffer.writei16
const buffer_len = buffer.len
const buffer_readu32 = buffer.readu32
const buffer_readstring = buffer.readstring
const buffer_readi16 = buffer.readi16
const buffer_writebits = buffer.writebits
const buffer_copy = buffer.copy
const buffer_readbits = buffer.readbits
type Roster = {
  read names: { string },
  read positions: { Vector3 },
}
const function Roster_serialize_binary(value: Roster): Loom.Serialized
  local size = 4 + #value.positions * 6
  size += 4
  for i = 1, #value.names do
    size += 4 + #value.names[i]
  end
  const b = buffer_create(size)
  buffer_writeu32(b, 0, #value.names)
  local offset = 4
  for i_2 = 1, #value.names do
    const names_item = value.names[i_2]
    const names_element_length = #names_item
    buffer_writeu32(b, offset, names_element_length)
    offset += 4
    buffer_writestring(b, offset, names_item)
    offset += names_element_length
  end
  buffer_writeu32(b, offset, #value.positions)
  offset += 4
  for i_3 = 1, #value.positions do
    const positions_item = value.positions[i_3]
    buffer_writei16(b, offset, positions_item.X)
    offset += 2
    buffer_writei16(b, offset, positions_item.Y)
    offset += 2
    buffer_writei16(b, offset, positions_item.Z)
    offset += 2
  end
  return { buffer = b }
end
const function Roster_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Roster, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 8 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const names_count = buffer_readu32(b, 0)
  local offset = 4
  const names = {}
  for i = 1, names_count do
    const read_0 = buffer_readu32(b, offset)
    offset += 4
    const names_element_length = read_0
    if buffer_len(b) < offset + names_element_length then
      return { ok = false, error = { kind = "invalid_length", field = "names[]" } }
    end
    const names_element = buffer_readstring(b, offset, names_element_length)
    offset += names_element_length
    names[i] = names_element
  end
  const read_1 = buffer_readu32(b, offset)
  offset += 4
  const positions_count = read_1
  if buffer_len(b) < offset + positions_count * 6 then
    return { ok = false, error = { kind = "invalid_length", field = "positions" } }
  end
  const positions = {}
  for i_2 = 1, positions_count do
    const read_2 = buffer_readi16(b, offset)
    offset += 2
    const read_3 = buffer_readi16(b, offset)
    offset += 2
    const read_4 = buffer_readi16(b, offset)
    offset += 2
    positions[i_2] = Vector3.new(read_2, read_3, read_4)
  end
  return { ok = true, value = { names = names, positions = positions } }
end
const function Roster_diff_binary_write(baseline: Roster, value: Roster, b: buffer)
  local offset = 1
  const names_length_changed = #baseline.names ~= #value.names
  buffer_writebits(b, 0, 1, if names_length_changed then 1 else 0)
  if names_length_changed then
    buffer_writeu32(b, offset, #value.names)
    offset += 4
    for i = 1, #value.names do
      const names_item = value.names[i]
      const names_element_length = #names_item
      buffer_writeu32(b, offset, names_element_length)
      offset += 4
      buffer_writestring(b, offset, names_item)
      offset += names_element_length
    end
  else
    const names_bits = offset * 8
    offset += (#value.names + 7) // 8
    for i_2 = 1, #value.names do
      const names_element_changed = not Loom.deep_equal(baseline.names[i_2], value.names[i_2])
      buffer_writebits(b, names_bits + (i_2 - 1), 1, if names_element_changed then 1 else 0)
      if names_element_changed then
        const names_element_value = value.names[i_2]
        const names_element_length_2 = #names_element_value
        buffer_writeu32(b, offset, names_element_length_2)
        offset += 4
        buffer_writestring(b, offset, names_element_value)
        offset += names_element_length_2
      end
    end
  end
  const positions_length_changed = #baseline.positions ~= #value.positions
  buffer_writebits(b, 1, 1, if positions_length_changed then 1 else 0)
  if positions_length_changed then
    buffer_writeu32(b, offset, #value.positions)
    offset += 4
    for i_3 = 1, #value.positions do
      const positions_item = value.positions[i_3]
      buffer_writei16(b, offset, positions_item.X)
      offset += 2
      buffer_writei16(b, offset, positions_item.Y)
      offset += 2
      buffer_writei16(b, offset, positions_item.Z)
      offset += 2
    end
  else
    const positions_bits = offset * 8
    offset += (#value.positions + 7) // 8
    for i_4 = 1, #value.positions do
      const positions_element_changed = not Loom.deep_equal(baseline.positions[i_4], value.positions[i_4])
      buffer_writebits(b, positions_bits + (i_4 - 1), 1, if positions_element_changed then 1 else 0)
      if positions_element_changed then
        const positions_element_value = value.positions[i_4]
        buffer_writei16(b, offset, positions_element_value.X)
        offset += 2
        buffer_writei16(b, offset, positions_element_value.Y)
        offset += 2
        buffer_writei16(b, offset, positions_element_value.Z)
        offset += 2
      end
    end
  end
  return offset
end
const function Roster_diff_binary_attempt(baseline: Roster, value: Roster, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Roster_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Roster_diff_binary_attempt(baseline, value, size * 2)
end
const function Roster_diff_binary(baseline: Roster, value: Roster): Loom.Serialized
  const baseline_s = Roster_serialize_binary(baseline)
  const value_s = Roster_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written = Roster_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Roster_apply_diff_binary_read(baseline: Roster, diff: Loom.Serialized): Loom.Result<Roster, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 1
  const names_length_changed = buffer_readbits(b, 0, 1) == 1
  local names = nil
  if names_length_changed then
    const read_0 = buffer_readu32(b, offset)
    offset += 4
    const names_2_count = read_0
    const names_2 = {}
    for i = 1, names_2_count do
      const read_1 = buffer_readu32(b, offset)
      offset += 4
      const names_element_length = read_1
      if buffer_len(b) < offset + names_element_length then
        return { ok = false, error = { kind = "invalid_length", field = "names[]" } }
      end
      const names_element = buffer_readstring(b, offset, names_element_length)
      offset += names_element_length
      names_2[i] = names_element
    end
    names = names_2
  else
    const names_count = #baseline.names
    const names_bits = offset * 8
    offset += (names_count + 7) // 8
    names = {}
    for i_2 = 1, names_count do
      const names_element_changed = buffer_readbits(b, names_bits + (i_2 - 1), 1) == 1
      local names_element_2 = nil
      if names_element_changed then
        const read_2 = buffer_readu32(b, offset)
        offset += 4
        const names_element_3_length = read_2
        if buffer_len(b) < offset + names_element_3_length then
          return { ok = false, error = { kind = "invalid_length", field = "names[]" } }
        end
        const names_element_3 = buffer_readstring(b, offset, names_element_3_length)
        offset += names_element_3_length
        names_element_2 = names_element_3
      else
        names_element_2 = baseline.names[i_2]
      end
      names[i_2] = names_element_2
    end
  end
  const positions_length_changed = buffer_readbits(b, 1, 1) == 1
  local positions = nil
  if positions_length_changed then
    const read_3 = buffer_readu32(b, offset)
    offset += 4
    const positions_2_count = read_3
    if buffer_len(b) < offset + positions_2_count * 6 then
      return { ok = false, error = { kind = "invalid_length", field = "positions" } }
    end
    const positions_2 = {}
    for i_3 = 1, positions_2_count do
      const read_4 = buffer_readi16(b, offset)
      offset += 2
      const read_5 = buffer_readi16(b, offset)
      offset += 2
      const read_6 = buffer_readi16(b, offset)
      offset += 2
      positions_2[i_3] = Vector3.new(read_4, read_5, read_6)
    end
    positions = positions_2
  else
    const positions_count = #baseline.positions
    const positions_bits = offset * 8
    offset += (positions_count + 7) // 8
    positions = {}
    for i_4 = 1, positions_count do
      const positions_element_changed = buffer_readbits(b, positions_bits + (i_4 - 1), 1) == 1
      local positions_element = nil
      if positions_element_changed then
        const read_7 = buffer_readi16(b, offset)
        offset += 2
        const read_8 = buffer_readi16(b, offset)
        offset += 2
        const read_9 = buffer_readi16(b, offset)
        offset += 2
        positions_element = Vector3.new(read_7, read_8, read_9)
      else
        positions_element = baseline.positions[i_4]
      end
      positions[i_4] = positions_element
    end
  end
  return { ok = true, value = { names = names, positions = positions } }
end
const function Roster_apply_diff_binary(baseline: Roster, diff: Loom.Serialized): Loom.Result<Roster, Loom.DeserializeError>
  const ok, result = pcall(Roster_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Roster_serializer: Loom.Serializer<Roster> = { serialize = Roster_serialize_binary, deserialize = Roster_deserialize_binary, diff = Roster_diff_binary, applyDiff = Roster_apply_diff_binary }