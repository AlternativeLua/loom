const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_len = buffer.len
const buffer_writeu32 = buffer.writeu32
const buffer_writestring = buffer.writestring
const buffer_readu32 = buffer.readu32
const buffer_readstring = buffer.readstring
const buffer_writebits = buffer.writebits
const buffer_copy = buffer.copy
const buffer_readbits = buffer.readbits
type IShape<Kind> = {
  read kind: Kind & string,
}
type Dot = IShape<"Dot"> & {}
const function Dot_serialize_binary(value: Dot): Loom.Serialized
  return {}
end
const function Dot_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Dot, Loom.DeserializeError>
  return { ok = true, value = { kind = "Dot" } }
end
const function Dot_diff_binary_write(baseline: Dot, value: Dot, b: buffer)
  local offset = 0
  return offset
end
const function Dot_diff_binary_attempt(baseline: Dot, value: Dot, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Dot_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Dot_diff_binary_attempt(baseline, value, size * 2)
end
const function Dot_diff_binary(baseline: Dot, value: Dot): Loom.Serialized
  return {}
end
const function Dot_apply_diff_binary_read(baseline: Dot, diff: Loom.Serialized): Loom.Result<Dot, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 0 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 0
  return { ok = true, value = { kind = "Dot" } }
end
const function Dot_apply_diff_binary(baseline: Dot, diff: Loom.Serialized): Loom.Result<Dot, Loom.DeserializeError>
  return { ok = true, value = { kind = "Dot" } }
end
const Dot_serializer: Loom.Serializer<Dot> = { serialize = Dot_serialize_binary, deserialize = Dot_deserialize_binary, diff = Dot_diff_binary, applyDiff = Dot_apply_diff_binary }
type Line = IShape<"Line"> & {
  read label: string,
}
const function Line_serialize_binary(value: Line): Loom.Serialized
  const b = buffer_create(4 + #value.label)
  const label_value = value.label
  const label_length = #label_value
  buffer_writeu32(b, 0, label_length)
  local offset = 4
  buffer_writestring(b, offset, label_value)
  offset += label_length
  return { buffer = b }
end
const function Line_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Line, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 4 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const label_length = buffer_readu32(b, 0)
  local offset = 4
  if buffer_len(b) < offset + label_length then
    return { ok = false, error = { kind = "invalid_length", field = "label" } }
  end
  const label = buffer_readstring(b, offset, label_length)
  offset += label_length
  return { ok = true, value = { label = label, kind = "Line" } }
end
const function Line_diff_binary_write(baseline: Line, value: Line, b: buffer)
  local offset = 1
  const label_changed = not Loom.deep_equal(baseline.label, value.label)
  buffer_writebits(b, 0, 1, if label_changed then 1 else 0)
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
const function Line_diff_binary_attempt(baseline: Line, value: Line, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Line_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Line_diff_binary_attempt(baseline, value, size * 2)
end
const function Line_diff_binary(baseline: Line, value: Line): Loom.Serialized
  const baseline_s = Line_serialize_binary(baseline)
  const value_s = Line_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written = Line_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Line_apply_diff_binary_read(baseline: Line, diff: Loom.Serialized): Loom.Result<Line, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 1
  const label_changed = buffer_readbits(b, 0, 1) == 1
  local label = nil
  if label_changed then
    const read_0 = buffer_readu32(b, offset)
    offset += 4
    const label_2_length = read_0
    if buffer_len(b) < offset + label_2_length then
      return { ok = false, error = { kind = "invalid_length", field = "label" } }
    end
    const label_2 = buffer_readstring(b, offset, label_2_length)
    offset += label_2_length
    label = label_2
  else
    label = baseline.label
  end
  return { ok = true, value = { label = label, kind = "Line" } }
end
const function Line_apply_diff_binary(baseline: Line, diff: Loom.Serialized): Loom.Result<Line, Loom.DeserializeError>
  const ok, result = pcall(Line_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Line_serializer: Loom.Serializer<Line> = { serialize = Line_serialize_binary, deserialize = Line_deserialize_binary, diff = Line_diff_binary, applyDiff = Line_apply_diff_binary }
type Canvas = {
  read masks: { { boolean } },
  read shapes: { (Dot | Line) },
}
const function Canvas_serialize_binary(value: Canvas): Loom.Serialized
  local size = 0
  size += 4
  for i = 1, #value.masks do
    size += 4 + (#value.masks[i] + 7) // 8
  end
  size += 4
  size += (#value.shapes + 7) // 8
  for i_2 = 1, #value.shapes do
    const shapes_element_value = value.shapes[i_2]
    local shapes_element_tag = 0
    if shapes_element_value.kind == "Line" then
      shapes_element_tag = 1
    end
    if shapes_element_tag == 1 then
      size += 4 + #value.shapes[i_2].label
    end
  end
  const b = buffer_create(size)
  buffer_writeu32(b, 0, #value.masks)
  local offset = 4
  for i_3 = 1, #value.masks do
    const masks_item = value.masks[i_3]
    buffer_writeu32(b, offset, #masks_item)
    offset += 4
    const masks_element_bits = offset * 8
    offset += (#masks_item + 7) // 8
    for i_4 = 1, #masks_item do
      const masks_element_item = masks_item[i_4]
      buffer_writebits(b, masks_element_bits + (i_4 - 1), 1, if masks_element_item then 1 else 0)
    end
  end
  buffer_writeu32(b, offset, #value.shapes)
  offset += 4
  const shapes_bits = offset * 8
  offset += (#value.shapes + 7) // 8
  for i_5 = 1, #value.shapes do
    const shapes_item = value.shapes[i_5]
    const shapes_element_value = shapes_item
    local shapes_element_tag = 0
    if shapes_element_value.kind == "Line" then
      shapes_element_tag = 1
    end
    buffer_writebits(b, shapes_bits + (i_5 - 1), 1, shapes_element_tag)
    if shapes_element_tag == 1 then
      const label_value = shapes_item.label
      const label_length = #label_value
      buffer_writeu32(b, offset, label_length)
      offset += 4
      buffer_writestring(b, offset, label_value)
      offset += label_length
    end
  end
  return { buffer = b }
end
const function Canvas_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Canvas, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 8 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const masks_count = buffer_readu32(b, 0)
  local offset = 4
  const masks = {}
  for i = 1, masks_count do
    const read_0 = buffer_readu32(b, offset)
    offset += 4
    const masks_element_count = read_0
    const masks_element_bits = offset * 8
    offset += (masks_element_count + 7) // 8
    const masks_element = {}
    for i_2 = 1, masks_element_count do
      masks_element[i_2] = buffer_readbits(b, masks_element_bits + (i_2 - 1), 1) == 1
    end
    masks[i] = masks_element
  end
  const read_1 = buffer_readu32(b, offset)
  offset += 4
  const shapes_count = read_1
  const shapes_bits = offset * 8
  offset += (shapes_count + 7) // 8
  const shapes = {}
  for i_3 = 1, shapes_count do
    const shapes_element_tag = buffer_readbits(b, shapes_bits + (i_3 - 1), 1)
    local shapes_element = nil
    if shapes_element_tag == 0 then
      shapes_element = { kind = "Dot" }
    elseif shapes_element_tag == 1 then
      const read_2 = buffer_readu32(b, offset)
      offset += 4
      const label_length = read_2
      if buffer_len(b) < offset + label_length then
        return { ok = false, error = { kind = "invalid_length", field = "shapes[].label" } }
      end
      const label = buffer_readstring(b, offset, label_length)
      offset += label_length
      shapes_element = { kind = "Line", label = label }
    else
      return { ok = false, error = { kind = "invalid_tag", field = "shapes[]" } }
    end
    shapes[i_3] = shapes_element
  end
  return { ok = true, value = { masks = masks, shapes = shapes } }
end
const function Canvas_diff_binary_write(baseline: Canvas, value: Canvas, b: buffer)
  local offset = 1
  const masks_length_changed = #baseline.masks ~= #value.masks
  buffer_writebits(b, 0, 1, if masks_length_changed then 1 else 0)
  if masks_length_changed then
    buffer_writeu32(b, offset, #value.masks)
    offset += 4
    for i = 1, #value.masks do
      const masks_item = value.masks[i]
      buffer_writeu32(b, offset, #masks_item)
      offset += 4
      const masks_element_bits = offset * 8
      offset += (#masks_item + 7) // 8
      for i_2 = 1, #masks_item do
        const masks_element_item = masks_item[i_2]
        buffer_writebits(b, masks_element_bits + (i_2 - 1), 1, if masks_element_item then 1 else 0)
      end
    end
  else
    const masks_bits = offset * 8
    offset += (#value.masks + 7) // 8
    for i_3 = 1, #value.masks do
      const masks_element_length_changed = #baseline.masks[i_3] ~= #value.masks[i_3]
      buffer_writebits(b, masks_bits + (i_3 - 1), 1, if masks_element_length_changed then 1 else 0)
      if masks_element_length_changed then
        buffer_writeu32(b, offset, #value.masks[i_3])
        offset += 4
        const masks_element_bits_2 = offset * 8
        offset += (#value.masks[i_3] + 7) // 8
        for i_4 = 1, #value.masks[i_3] do
          const masks_element_item_2 = value.masks[i_3][i_4]
          buffer_writebits(b, masks_element_bits_2 + (i_4 - 1), 1, if masks_element_item_2 then 1 else 0)
        end
      else
        const masks_element_bits_3 = offset * 8
        offset += (#value.masks[i_3] * 2 + 7) // 8
        for i_5 = 1, #value.masks[i_3] do
          const masks_element_element_changed = not Loom.deep_equal(baseline.masks[i_3][i_5], value.masks[i_3][i_5])
          buffer_writebits(b, masks_element_bits_3 + (i_5 - 1) * 2, 1, if masks_element_element_changed then 1 else 0)
          if masks_element_element_changed then
            buffer_writebits(b, masks_element_bits_3 + (i_5 - 1) * 2 + 1, 1, if value.masks[i_3][i_5] then 1 else 0)
          end
        end
      end
    end
  end
  const shapes_length_changed = #baseline.shapes ~= #value.shapes
  buffer_writebits(b, 1, 1, if shapes_length_changed then 1 else 0)
  if shapes_length_changed then
    buffer_writeu32(b, offset, #value.shapes)
    offset += 4
    const shapes_bits = offset * 8
    offset += (#value.shapes + 7) // 8
    for i_6 = 1, #value.shapes do
      const shapes_item = value.shapes[i_6]
      const shapes_element_value = shapes_item
      local shapes_element_tag = 0
      if shapes_element_value.kind == "Line" then
        shapes_element_tag = 1
      end
      buffer_writebits(b, shapes_bits + (i_6 - 1), 1, shapes_element_tag)
      if shapes_element_tag == 1 then
        const label_value = shapes_item.label
        const label_length = #label_value
        buffer_writeu32(b, offset, label_length)
        offset += 4
        buffer_writestring(b, offset, label_value)
        offset += label_length
      end
    end
  else
    const shapes_bits_2 = offset * 8
    offset += (#value.shapes * 2 + 7) // 8
    for i_7 = 1, #value.shapes do
      const shapes_element_tag_b_value = baseline.shapes[i_7]
      local shapes_element_tag_b = 0
      if shapes_element_tag_b_value.kind == "Line" then
        shapes_element_tag_b = 1
      end
      const shapes_element_tag_c_value = value.shapes[i_7]
      local shapes_element_tag_c = 0
      if shapes_element_tag_c_value.kind == "Line" then
        shapes_element_tag_c = 1
      end
      const shapes_element_tag_changed = shapes_element_tag_b ~= shapes_element_tag_c
      buffer_writebits(b, shapes_bits_2 + (i_7 - 1) * 2, 1, if shapes_element_tag_changed then 1 else 0)
      if shapes_element_tag_changed then
        const shapes_element_value = value.shapes[i_7]
        local shapes_element_tag = 0
        if shapes_element_value.kind == "Line" then
          shapes_element_tag = 1
        end
        buffer_writebits(b, shapes_bits_2 + (i_7 - 1) * 2 + 1, 1, shapes_element_tag)
        if shapes_element_tag == 1 then
          const label_value_2 = value.shapes[i_7].label
          const label_length_2 = #label_value_2
          buffer_writeu32(b, offset, label_length_2)
          offset += 4
          buffer_writestring(b, offset, label_value_2)
          offset += label_length_2
        end
      else
        if shapes_element_tag_c == 1 then
          const label_changed = not Loom.deep_equal(baseline.shapes[i_7].label, value.shapes[i_7].label)
          buffer_writebits(b, shapes_bits_2 + (i_7 - 1) * 2 + 1, 1, if label_changed then 1 else 0)
          if label_changed then
            const label_value_3 = value.shapes[i_7].label
            const label_length_3 = #label_value_3
            buffer_writeu32(b, offset, label_length_3)
            offset += 4
            buffer_writestring(b, offset, label_value_3)
            offset += label_length_3
          end
        end
      end
    end
  end
  return offset
end
const function Canvas_diff_binary_attempt(baseline: Canvas, value: Canvas, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Canvas_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Canvas_diff_binary_attempt(baseline, value, size * 2)
end
const function Canvas_diff_binary(baseline: Canvas, value: Canvas): Loom.Serialized
  const baseline_s = Canvas_serialize_binary(baseline)
  const value_s = Canvas_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written = Canvas_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Canvas_apply_diff_binary_read(baseline: Canvas, diff: Loom.Serialized): Loom.Result<Canvas, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 1
  const masks_length_changed = buffer_readbits(b, 0, 1) == 1
  local masks = nil
  if masks_length_changed then
    const read_0 = buffer_readu32(b, offset)
    offset += 4
    const masks_2_count = read_0
    const masks_2 = {}
    for i = 1, masks_2_count do
      const read_1 = buffer_readu32(b, offset)
      offset += 4
      const masks_element_count = read_1
      const masks_element_bits = offset * 8
      offset += (masks_element_count + 7) // 8
      const masks_element = {}
      for i_2 = 1, masks_element_count do
        masks_element[i_2] = buffer_readbits(b, masks_element_bits + (i_2 - 1), 1) == 1
      end
      masks_2[i] = masks_element
    end
    masks = masks_2
  else
    const masks_count = #baseline.masks
    const masks_bits = offset * 8
    offset += (masks_count + 7) // 8
    masks = {}
    for i_3 = 1, masks_count do
      const masks_element_length_changed = buffer_readbits(b, masks_bits + (i_3 - 1), 1) == 1
      local masks_element_2 = nil
      if masks_element_length_changed then
        const read_2 = buffer_readu32(b, offset)
        offset += 4
        const masks_element_3_count = read_2
        const masks_element_3_bits = offset * 8
        offset += (masks_element_3_count + 7) // 8
        const masks_element_3 = {}
        for i_4 = 1, masks_element_3_count do
          masks_element_3[i_4] = buffer_readbits(b, masks_element_3_bits + (i_4 - 1), 1) == 1
        end
        masks_element_2 = masks_element_3
      else
        const masks_element_count_2 = #baseline.masks[i_3]
        const masks_element_bits_2 = offset * 8
        offset += (masks_element_count_2 * 2 + 7) // 8
        masks_element_2 = {}
        for i_5 = 1, masks_element_count_2 do
          const masks_element_element_changed = buffer_readbits(b, masks_element_bits_2 + (i_5 - 1) * 2, 1) == 1
          local masks_element_element = nil
          if masks_element_element_changed then
            masks_element_element = buffer_readbits(b, masks_element_bits_2 + (i_5 - 1) * 2 + 1, 1) == 1
          else
            masks_element_element = baseline.masks[i_3][i_5]
          end
          masks_element_2[i_5] = masks_element_element
        end
      end
      masks[i_3] = masks_element_2
    end
  end
  const shapes_length_changed = buffer_readbits(b, 1, 1) == 1
  local shapes = nil
  if shapes_length_changed then
    const read_3 = buffer_readu32(b, offset)
    offset += 4
    const shapes_2_count = read_3
    const shapes_2_bits = offset * 8
    offset += (shapes_2_count + 7) // 8
    const shapes_2 = {}
    for i_6 = 1, shapes_2_count do
      const shapes_element_tag = buffer_readbits(b, shapes_2_bits + (i_6 - 1), 1)
      local shapes_element = nil
      if shapes_element_tag == 0 then
        shapes_element = { kind = "Dot" }
      elseif shapes_element_tag == 1 then
        const read_4 = buffer_readu32(b, offset)
        offset += 4
        const label_length = read_4
        if buffer_len(b) < offset + label_length then
          return { ok = false, error = { kind = "invalid_length", field = "shapes[].label" } }
        end
        const label = buffer_readstring(b, offset, label_length)
        offset += label_length
        shapes_element = { kind = "Line", label = label }
      else
        return { ok = false, error = { kind = "invalid_tag", field = "shapes[]" } }
      end
      shapes_2[i_6] = shapes_element
    end
    shapes = shapes_2
  else
    const shapes_count = #baseline.shapes
    const shapes_bits = offset * 8
    offset += (shapes_count * 2 + 7) // 8
    shapes = {}
    for i_7 = 1, shapes_count do
      const shapes_element_tag_changed = buffer_readbits(b, shapes_bits + (i_7 - 1) * 2, 1) == 1
      local shapes_element_2 = nil
      if shapes_element_tag_changed then
        const shapes_element_3_tag = buffer_readbits(b, shapes_bits + (i_7 - 1) * 2 + 1, 1)
        local shapes_element_3 = nil
        if shapes_element_3_tag == 0 then
          shapes_element_3 = { kind = "Dot" }
        elseif shapes_element_3_tag == 1 then
          const read_5 = buffer_readu32(b, offset)
          offset += 4
          const label_2_length = read_5
          if buffer_len(b) < offset + label_2_length then
            return { ok = false, error = { kind = "invalid_length", field = "shapes[].label" } }
          end
          const label_2 = buffer_readstring(b, offset, label_2_length)
          offset += label_2_length
          shapes_element_3 = { kind = "Line", label = label_2 }
        else
          return { ok = false, error = { kind = "invalid_tag", field = "shapes[]" } }
        end
        shapes_element_2 = shapes_element_3
      else
        const shapes_element_tag_b_value = baseline.shapes[i_7]
        local shapes_element_tag_b = 0
        if shapes_element_tag_b_value.kind == "Line" then
          shapes_element_tag_b = 1
        end
        if shapes_element_tag_b == 0 then
          shapes_element_2 = { kind = "Dot" }
        elseif shapes_element_tag_b == 1 then
          const label_changed = buffer_readbits(b, shapes_bits + (i_7 - 1) * 2 + 1, 1) == 1
          local label_3 = nil
          if label_changed then
            const read_6 = buffer_readu32(b, offset)
            offset += 4
            const label_4_length = read_6
            if buffer_len(b) < offset + label_4_length then
              return { ok = false, error = { kind = "invalid_length", field = "shapes[].label" } }
            end
            const label_4 = buffer_readstring(b, offset, label_4_length)
            offset += label_4_length
            label_3 = label_4
          else
            label_3 = baseline.shapes[i_7].label
          end
          shapes_element_2 = { kind = "Line", label = label_3 }
        end
      end
      shapes[i_7] = shapes_element_2
    end
  end
  return { ok = true, value = { masks = masks, shapes = shapes } }
end
const function Canvas_apply_diff_binary(baseline: Canvas, diff: Loom.Serialized): Loom.Result<Canvas, Loom.DeserializeError>
  const ok, result = pcall(Canvas_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Canvas_serializer: Loom.Serializer<Canvas> = { serialize = Canvas_serialize_binary, deserialize = Canvas_deserialize_binary, diff = Canvas_diff_binary, applyDiff = Canvas_apply_diff_binary }