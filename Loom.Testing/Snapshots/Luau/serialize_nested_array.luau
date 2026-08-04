const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writeu32 = buffer.writeu32
const buffer_writestring = buffer.writestring
const buffer_len = buffer.len
const buffer_readu32 = buffer.readu32
const buffer_readstring = buffer.readstring
const buffer_writebits = buffer.writebits
const buffer_copy = buffer.copy
const buffer_readbits = buffer.readbits
type Grid = {
  read rows: { { string } },
}
const function Grid_serialize_binary(value: Grid): Loom.Serialized
  local size = 0
  size += 4
  for i = 1, #value.rows do
    size += 4
    for i_2 = 1, #value.rows[i] do
      size += 4 + #value.rows[i][i_2]
    end
  end
  const b = buffer_create(size)
  buffer_writeu32(b, 0, #value.rows)
  local offset = 4
  for i_3 = 1, #value.rows do
    const rows_item = value.rows[i_3]
    buffer_writeu32(b, offset, #rows_item)
    offset += 4
    for i_4 = 1, #rows_item do
      const rows_element_item = rows_item[i_4]
      const rows_element_element_length = #rows_element_item
      buffer_writeu32(b, offset, rows_element_element_length)
      offset += 4
      buffer_writestring(b, offset, rows_element_item)
      offset += rows_element_element_length
    end
  end
  return { buffer = b }
end
const function Grid_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Grid, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 4 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const rows_count = buffer_readu32(b, 0)
  local offset = 4
  const rows = {}
  for i = 1, rows_count do
    const read_0 = buffer_readu32(b, offset)
    offset += 4
    const rows_element_count = read_0
    const rows_element = {}
    for i_2 = 1, rows_element_count do
      const read_1 = buffer_readu32(b, offset)
      offset += 4
      const rows_element_element_length = read_1
      if buffer_len(b) < offset + rows_element_element_length then
        return { ok = false, error = { kind = "invalid_length", field = "rows[][]" } }
      end
      const rows_element_element = buffer_readstring(b, offset, rows_element_element_length)
      offset += rows_element_element_length
      rows_element[i_2] = rows_element_element
    end
    rows[i] = rows_element
  end
  return { ok = true, value = { rows = rows } }
end
const function Grid_diff_binary_write(baseline: Grid, value: Grid, b: buffer)
  local offset = 1
  const rows_length_changed = #baseline.rows ~= #value.rows
  buffer_writebits(b, 0, 1, if rows_length_changed then 1 else 0)
  if rows_length_changed then
    buffer_writeu32(b, offset, #value.rows)
    offset += 4
    for i = 1, #value.rows do
      const rows_item = value.rows[i]
      buffer_writeu32(b, offset, #rows_item)
      offset += 4
      for i_2 = 1, #rows_item do
        const rows_element_item = rows_item[i_2]
        const rows_element_element_length = #rows_element_item
        buffer_writeu32(b, offset, rows_element_element_length)
        offset += 4
        buffer_writestring(b, offset, rows_element_item)
        offset += rows_element_element_length
      end
    end
  else
    const rows_bits = offset * 8
    offset += (#value.rows + 7) // 8
    for i_3 = 1, #value.rows do
      const rows_element_length_changed = #baseline.rows[i_3] ~= #value.rows[i_3]
      buffer_writebits(b, rows_bits + (i_3 - 1), 1, if rows_element_length_changed then 1 else 0)
      if rows_element_length_changed then
        buffer_writeu32(b, offset, #value.rows[i_3])
        offset += 4
        for i_4 = 1, #value.rows[i_3] do
          const rows_element_item_2 = value.rows[i_3][i_4]
          const rows_element_element_length_2 = #rows_element_item_2
          buffer_writeu32(b, offset, rows_element_element_length_2)
          offset += 4
          buffer_writestring(b, offset, rows_element_item_2)
          offset += rows_element_element_length_2
        end
      else
        const rows_element_bits = offset * 8
        offset += (#value.rows[i_3] + 7) // 8
        for i_5 = 1, #value.rows[i_3] do
          const rows_element_element_changed = not Loom.deep_equal(baseline.rows[i_3][i_5], value.rows[i_3][i_5])
          buffer_writebits(b, rows_element_bits + (i_5 - 1), 1, if rows_element_element_changed then 1 else 0)
          if rows_element_element_changed then
            const rows_element_element_value = value.rows[i_3][i_5]
            const rows_element_element_length_3 = #rows_element_element_value
            buffer_writeu32(b, offset, rows_element_element_length_3)
            offset += 4
            buffer_writestring(b, offset, rows_element_element_value)
            offset += rows_element_element_length_3
          end
        end
      end
    end
  end
  return offset
end
const function Grid_diff_binary_attempt(baseline: Grid, value: Grid, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Grid_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Grid_diff_binary_attempt(baseline, value, size * 2)
end
const function Grid_diff_binary(baseline: Grid, value: Grid): Loom.Serialized
  const baseline_s = Grid_serialize_binary(baseline)
  const value_s = Grid_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written = Grid_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Grid_apply_diff_binary_read(baseline: Grid, diff: Loom.Serialized): Loom.Result<Grid, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 1
  const rows_length_changed = buffer_readbits(b, 0, 1) == 1
  local rows = nil
  if rows_length_changed then
    const read_0 = buffer_readu32(b, offset)
    offset += 4
    const rows_2_count = read_0
    const rows_2 = {}
    for i = 1, rows_2_count do
      const read_1 = buffer_readu32(b, offset)
      offset += 4
      const rows_element_count = read_1
      const rows_element = {}
      for i_2 = 1, rows_element_count do
        const read_2 = buffer_readu32(b, offset)
        offset += 4
        const rows_element_element_length = read_2
        if buffer_len(b) < offset + rows_element_element_length then
          return { ok = false, error = { kind = "invalid_length", field = "rows[][]" } }
        end
        const rows_element_element = buffer_readstring(b, offset, rows_element_element_length)
        offset += rows_element_element_length
        rows_element[i_2] = rows_element_element
      end
      rows_2[i] = rows_element
    end
    rows = rows_2
  else
    const rows_count = #baseline.rows
    const rows_bits = offset * 8
    offset += (rows_count + 7) // 8
    rows = {}
    for i_3 = 1, rows_count do
      const rows_element_length_changed = buffer_readbits(b, rows_bits + (i_3 - 1), 1) == 1
      local rows_element_2 = nil
      if rows_element_length_changed then
        const read_3 = buffer_readu32(b, offset)
        offset += 4
        const rows_element_3_count = read_3
        const rows_element_3 = {}
        for i_4 = 1, rows_element_3_count do
          const read_4 = buffer_readu32(b, offset)
          offset += 4
          const rows_element_element_2_length = read_4
          if buffer_len(b) < offset + rows_element_element_2_length then
            return { ok = false, error = { kind = "invalid_length", field = "rows[][]" } }
          end
          const rows_element_element_2 = buffer_readstring(b, offset, rows_element_element_2_length)
          offset += rows_element_element_2_length
          rows_element_3[i_4] = rows_element_element_2
        end
        rows_element_2 = rows_element_3
      else
        const rows_element_count_2 = #baseline.rows[i_3]
        const rows_element_bits = offset * 8
        offset += (rows_element_count_2 + 7) // 8
        rows_element_2 = {}
        for i_5 = 1, rows_element_count_2 do
          const rows_element_element_changed = buffer_readbits(b, rows_element_bits + (i_5 - 1), 1) == 1
          local rows_element_element_3 = nil
          if rows_element_element_changed then
            const read_5 = buffer_readu32(b, offset)
            offset += 4
            const rows_element_element_4_length = read_5
            if buffer_len(b) < offset + rows_element_element_4_length then
              return { ok = false, error = { kind = "invalid_length", field = "rows[][]" } }
            end
            const rows_element_element_4 = buffer_readstring(b, offset, rows_element_element_4_length)
            offset += rows_element_element_4_length
            rows_element_element_3 = rows_element_element_4
          else
            rows_element_element_3 = baseline.rows[i_3][i_5]
          end
          rows_element_2[i_5] = rows_element_element_3
        end
      end
      rows[i_3] = rows_element_2
    end
  end
  return { ok = true, value = { rows = rows } }
end
const function Grid_apply_diff_binary(baseline: Grid, diff: Loom.Serialized): Loom.Result<Grid, Loom.DeserializeError>
  const ok, result = pcall(Grid_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Grid_serializer: Loom.Serializer<Grid> = { serialize = Grid_serialize_binary, deserialize = Grid_deserialize_binary, diff = Grid_diff_binary, applyDiff = Grid_apply_diff_binary }