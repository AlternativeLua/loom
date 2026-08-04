const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writebits = buffer.writebits
const buffer_writef32 = buffer.writef32
const buffer_writeu32 = buffer.writeu32
const buffer_writestring = buffer.writestring
const buffer_len = buffer.len
const buffer_readbits = buffer.readbits
const buffer_readf32 = buffer.readf32
const buffer_readu32 = buffer.readu32
const buffer_readstring = buffer.readstring
const buffer_copy = buffer.copy
type Cell = {
  read content: number | string,
}
const function Cell_serialize_binary(value: Cell): Loom.Serialized
  const content_value = value.content
  local content_tag = 0
  if typeof(content_value) == "string" then
    content_tag = 1
  end
  local size = 1
  if content_tag == 0 then
    size += 4
  elseif content_tag == 1 then
    size += 4 + #value.content
  end
  const b = buffer_create(size)
  buffer_writebits(b, 0, 1, content_tag)
  local offset = 1
  if content_tag == 0 then
    buffer_writef32(b, offset, value.content)
    offset += 4
  elseif content_tag == 1 then
    const content_value = value.content
    const content_length = #content_value
    buffer_writeu32(b, offset, content_length)
    offset += 4
    buffer_writestring(b, offset, content_value)
    offset += content_length
  end
  return { buffer = b }
end
const function Cell_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Cell, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const content_tag = buffer_readbits(b, 0, 1)
  local content = nil
  local offset = 1
  if content_tag == 0 then
    if buffer_len(b) < offset + 4 then
      return { ok = false, error = { kind = "truncated", field = "content" } }
    end
    const read_0 = buffer_readf32(b, offset)
    offset += 4
    content = read_0
  elseif content_tag == 1 then
    const read_1 = buffer_readu32(b, offset)
    offset += 4
    const content_2_length = read_1
    if buffer_len(b) < offset + content_2_length then
      return { ok = false, error = { kind = "invalid_length", field = "content" } }
    end
    const content_2 = buffer_readstring(b, offset, content_2_length)
    offset += content_2_length
    content = content_2
  else
    return { ok = false, error = { kind = "invalid_tag", field = "content" } }
  end
  return { ok = true, value = { content = content } }
end
const function Cell_diff_binary_write(baseline: Cell, value: Cell, b: buffer)
  local offset = 1
  const content_tag_b_value = baseline.content
  local content_tag_b = 0
  if typeof(content_tag_b_value) == "string" then
    content_tag_b = 1
  end
  const content_tag_c_value = value.content
  local content_tag_c = 0
  if typeof(content_tag_c_value) == "string" then
    content_tag_c = 1
  end
  const content_tag_changed = content_tag_b ~= content_tag_c
  buffer_writebits(b, 0, 1, if content_tag_changed then 1 else 0)
  if content_tag_changed then
    const content_value = value.content
    local content_tag = 0
    if typeof(content_value) == "string" then
      content_tag = 1
    end
    buffer_writebits(b, 1, 1, content_tag)
    if content_tag == 0 then
      buffer_writef32(b, offset, value.content)
      offset += 4
    elseif content_tag == 1 then
      const content_value = value.content
      const content_length = #content_value
      buffer_writeu32(b, offset, content_length)
      offset += 4
      buffer_writestring(b, offset, content_value)
      offset += content_length
    end
  else
    if content_tag_c == 0 then
      const content_changed = not Loom.deep_equal(baseline.content, value.content)
      buffer_writebits(b, 1, 1, if content_changed then 1 else 0)
      if content_changed then
        buffer_writef32(b, offset, value.content)
        offset += 4
      end
    elseif content_tag_c == 1 then
      const content_changed_2 = not Loom.deep_equal(baseline.content, value.content)
      buffer_writebits(b, 1, 1, if content_changed_2 then 1 else 0)
      if content_changed_2 then
        const content_value_2 = value.content
        const content_length_2 = #content_value_2
        buffer_writeu32(b, offset, content_length_2)
        offset += 4
        buffer_writestring(b, offset, content_value_2)
        offset += content_length_2
      end
    end
  end
  return offset
end
const function Cell_diff_binary_attempt(baseline: Cell, value: Cell, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Cell_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Cell_diff_binary_attempt(baseline, value, size * 2)
end
const function Cell_diff_binary(baseline: Cell, value: Cell): Loom.Serialized
  const baseline_s = Cell_serialize_binary(baseline)
  const value_s = Cell_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written = Cell_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Cell_apply_diff_binary_read(baseline: Cell, diff: Loom.Serialized): Loom.Result<Cell, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 1
  const content_tag_changed = buffer_readbits(b, 0, 1) == 1
  local content = nil
  if content_tag_changed then
    const content_2_tag = buffer_readbits(b, 1, 1)
    local content_2 = nil
    if content_2_tag == 0 then
      if buffer_len(b) < offset + 4 then
        return { ok = false, error = { kind = "truncated", field = "content" } }
      end
      const read_0 = buffer_readf32(b, offset)
      offset += 4
      content_2 = read_0
    elseif content_2_tag == 1 then
      const read_1 = buffer_readu32(b, offset)
      offset += 4
      const content_3_length = read_1
      if buffer_len(b) < offset + content_3_length then
        return { ok = false, error = { kind = "invalid_length", field = "content" } }
      end
      const content_3 = buffer_readstring(b, offset, content_3_length)
      offset += content_3_length
      content_2 = content_3
    else
      return { ok = false, error = { kind = "invalid_tag", field = "content" } }
    end
    content = content_2
  else
    const content_tag_b_value = baseline.content
    local content_tag_b = 0
    if typeof(content_tag_b_value) == "string" then
      content_tag_b = 1
    end
    if content_tag_b == 0 then
      const content_changed = buffer_readbits(b, 1, 1) == 1
      local content_4 = nil
      if content_changed then
        const read_2 = buffer_readf32(b, offset)
        offset += 4
        content_4 = read_2
      else
        content_4 = baseline.content
      end
      content = content_4
    elseif content_tag_b == 1 then
      const content_changed_2 = buffer_readbits(b, 1, 1) == 1
      local content_5 = nil
      if content_changed_2 then
        const read_3 = buffer_readu32(b, offset)
        offset += 4
        const content_6_length = read_3
        if buffer_len(b) < offset + content_6_length then
          return { ok = false, error = { kind = "invalid_length", field = "content" } }
        end
        const content_6 = buffer_readstring(b, offset, content_6_length)
        offset += content_6_length
        content_5 = content_6
      else
        content_5 = baseline.content
      end
      content = content_5
    end
  end
  return { ok = true, value = { content = content } }
end
const function Cell_apply_diff_binary(baseline: Cell, diff: Loom.Serialized): Loom.Result<Cell, Loom.DeserializeError>
  const ok, result = pcall(Cell_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Cell_serializer: Loom.Serializer<Cell> = { serialize = Cell_serialize_binary, deserialize = Cell_deserialize_binary, diff = Cell_diff_binary, applyDiff = Cell_apply_diff_binary }