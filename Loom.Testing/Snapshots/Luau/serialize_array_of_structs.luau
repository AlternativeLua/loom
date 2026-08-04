const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writeu32 = buffer.writeu32
const buffer_len = buffer.len
const buffer_readu32 = buffer.readu32
const buffer_writebits = buffer.writebits
const buffer_copy = buffer.copy
const buffer_readbits = buffer.readbits
const buffer_writeu8 = buffer.writeu8
const buffer_readu8 = buffer.readu8
type Bag = {
  read owners: { Part },
}
const function Bag_serialize_binary(value: Bag): Loom.Serialized
  const blobs = {}
  const b = buffer_create(4)
  buffer_writeu32(b, 0, #value.owners)
  local offset = 4
  for i = 1, #value.owners do
    const owners_item = value.owners[i]
    table.insert(blobs, owners_item)
  end
  return { buffer = b, blobs = blobs }
end
const function Bag_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Bag, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 4 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const blobs = serialized.blobs
  if blobs == nil then
    return { ok = false, error = { kind = "missing_blob" } }
  end
  local blob_index = 1
  const owners_count = buffer_readu32(b, 0)
  local offset = 4
  const owners = {}
  for i = 1, owners_count do
    const owners_element_blob = blobs[blob_index]
    blob_index += 1
    if owners_element_blob == nil then
      return { ok = false, error = { kind = "missing_blob", field = "owners[]" } }
    end
    if typeof(owners_element_blob) ~= "Instance" or not owners_element_blob:IsA("Part") then
      return { ok = false, error = { kind = "invalid_blob", field = "owners[]" } }
    end
    owners[i] = owners_element_blob
  end
  return { ok = true, value = { owners = owners } }
end
const function Bag_diff_binary_write(baseline: Bag, value: Bag, b: buffer)
  const blobs = {}
  local offset = 1
  const owners_length_changed = #baseline.owners ~= #value.owners
  buffer_writebits(b, 0, 1, if owners_length_changed then 1 else 0)
  if owners_length_changed then
    buffer_writeu32(b, offset, #value.owners)
    offset += 4
    for i = 1, #value.owners do
      const owners_item = value.owners[i]
      table.insert(blobs, owners_item)
    end
  else
    const owners_bits = offset * 8
    offset += (#value.owners + 7) // 8
    for i_2 = 1, #value.owners do
      const owners_element_changed = not Loom.deep_equal(baseline.owners[i_2], value.owners[i_2])
      buffer_writebits(b, owners_bits + (i_2 - 1), 1, if owners_element_changed then 1 else 0)
      if owners_element_changed then
        table.insert(blobs, value.owners[i_2])
      end
    end
  end
  return offset, blobs
end
const function Bag_diff_binary_attempt(baseline: Bag, value: Bag, size: number)
  const scratch = buffer_create(size)
  const ok, written, blobs = pcall(Bag_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written, blobs
  end
  return Bag_diff_binary_attempt(baseline, value, size * 2)
end
const function Bag_diff_binary(baseline: Bag, value: Bag): Loom.Serialized
  const baseline_s = Bag_serialize_binary(baseline)
  const value_s = Bag_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written, blobs = Bag_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result, blobs = blobs }
end
const function Bag_apply_diff_binary_read(baseline: Bag, diff: Loom.Serialized): Loom.Result<Bag, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const blobs = diff.blobs
  if blobs == nil then
    return { ok = false, error = { kind = "missing_blob" } }
  end
  local blob_index = 1
  local offset = 1
  const owners_length_changed = buffer_readbits(b, 0, 1) == 1
  local owners = nil
  if owners_length_changed then
    const read_0 = buffer_readu32(b, offset)
    offset += 4
    const owners_2_count = read_0
    const owners_2 = {}
    for i = 1, owners_2_count do
      const owners_element_blob = blobs[blob_index]
      blob_index += 1
      if owners_element_blob == nil then
        return { ok = false, error = { kind = "missing_blob", field = "owners[]" } }
      end
      if typeof(owners_element_blob) ~= "Instance" or not owners_element_blob:IsA("Part") then
        return { ok = false, error = { kind = "invalid_blob", field = "owners[]" } }
      end
      owners_2[i] = owners_element_blob
    end
    owners = owners_2
  else
    const owners_count = #baseline.owners
    const owners_bits = offset * 8
    offset += (owners_count + 7) // 8
    owners = {}
    for i_2 = 1, owners_count do
      const owners_element_changed = buffer_readbits(b, owners_bits + (i_2 - 1), 1) == 1
      local owners_element = nil
      if owners_element_changed then
        const owners_element_blob_2 = blobs[blob_index]
        blob_index += 1
        if owners_element_blob_2 == nil then
          return { ok = false, error = { kind = "missing_blob", field = "owners[]" } }
        end
        if typeof(owners_element_blob_2) ~= "Instance" or not owners_element_blob_2:IsA("Part") then
          return { ok = false, error = { kind = "invalid_blob", field = "owners[]" } }
        end
        owners_element = owners_element_blob_2
      else
        owners_element = baseline.owners[i_2]
      end
      owners[i_2] = owners_element
    end
  end
  return { ok = true, value = { owners = owners } }
end
const function Bag_apply_diff_binary(baseline: Bag, diff: Loom.Serialized): Loom.Result<Bag, Loom.DeserializeError>
  const ok, result = pcall(Bag_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Bag_serializer: Loom.Serializer<Bag> = { serialize = Bag_serialize_binary, deserialize = Bag_deserialize_binary, diff = Bag_diff_binary, applyDiff = Bag_apply_diff_binary }
type Leaf = {
  read v: number,
}
const function Leaf_serialize_binary(value: Leaf): Loom.Serialized
  const b = buffer_create(1)
  buffer_writeu8(b, 0, value.v)
  return { buffer = b }
end
const function Leaf_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Leaf, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  return { ok = true, value = { v = buffer_readu8(b, 0) } }
end
const function Leaf_diff_binary_write(baseline: Leaf, value: Leaf, b: buffer)
  local offset = 1
  const v_changed = not Loom.deep_equal(baseline.v, value.v)
  buffer_writebits(b, 0, 1, if v_changed then 1 else 0)
  if v_changed then
    buffer_writeu8(b, offset, value.v)
    offset += 1
  end
  return offset
end
const function Leaf_diff_binary_attempt(baseline: Leaf, value: Leaf, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Leaf_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Leaf_diff_binary_attempt(baseline, value, size * 2)
end
const function Leaf_diff_binary(baseline: Leaf, value: Leaf): Loom.Serialized
  const baseline_s = Leaf_serialize_binary(baseline)
  const value_s = Leaf_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written = Leaf_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Leaf_apply_diff_binary_read(baseline: Leaf, diff: Loom.Serialized): Loom.Result<Leaf, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 1
  const v_changed = buffer_readbits(b, 0, 1) == 1
  local v = nil
  if v_changed then
    const read_0 = buffer_readu8(b, offset)
    offset += 1
    v = read_0
  else
    v = baseline.v
  end
  return { ok = true, value = { v = v } }
end
const function Leaf_apply_diff_binary(baseline: Leaf, diff: Loom.Serialized): Loom.Result<Leaf, Loom.DeserializeError>
  const ok, result = pcall(Leaf_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Leaf_serializer: Loom.Serializer<Leaf> = { serialize = Leaf_serialize_binary, deserialize = Leaf_deserialize_binary, diff = Leaf_diff_binary, applyDiff = Leaf_apply_diff_binary }
type Branch = {
  read leaves: { Leaf },
}
const function Branch_serialize_binary(value: Branch): Loom.Serialized
  const b = buffer_create(4 + #value.leaves)
  buffer_writeu32(b, 0, #value.leaves)
  local offset = 4
  for i = 1, #value.leaves do
    const leaves_item = value.leaves[i]
    buffer_writeu8(b, offset, leaves_item.v)
    offset += 1
  end
  return { buffer = b }
end
const function Branch_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Branch, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 4 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const leaves_count = buffer_readu32(b, 0)
  local offset = 4
  if buffer_len(b) < offset + leaves_count then
    return { ok = false, error = { kind = "invalid_length", field = "leaves" } }
  end
  const leaves = {}
  for i = 1, leaves_count do
    const read_0 = buffer_readu8(b, offset)
    offset += 1
    leaves[i] = { v = read_0 }
  end
  return { ok = true, value = { leaves = leaves } }
end
const function Branch_diff_binary_write(baseline: Branch, value: Branch, b: buffer)
  local offset = 1
  const leaves_length_changed = #baseline.leaves ~= #value.leaves
  buffer_writebits(b, 0, 1, if leaves_length_changed then 1 else 0)
  if leaves_length_changed then
    buffer_writeu32(b, offset, #value.leaves)
    offset += 4
    for i = 1, #value.leaves do
      const leaves_item = value.leaves[i]
      buffer_writeu8(b, offset, leaves_item.v)
      offset += 1
    end
  else
    const leaves_bits = offset * 8
    offset += (#value.leaves + 7) // 8
    for i_2 = 1, #value.leaves do
      const v_changed = not Loom.deep_equal(baseline.leaves[i_2].v, value.leaves[i_2].v)
      buffer_writebits(b, leaves_bits + (i_2 - 1), 1, if v_changed then 1 else 0)
      if v_changed then
        buffer_writeu8(b, offset, value.leaves[i_2].v)
        offset += 1
      end
    end
  end
  return offset
end
const function Branch_diff_binary_attempt(baseline: Branch, value: Branch, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Branch_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Branch_diff_binary_attempt(baseline, value, size * 2)
end
const function Branch_diff_binary(baseline: Branch, value: Branch): Loom.Serialized
  const baseline_s = Branch_serialize_binary(baseline)
  const value_s = Branch_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written = Branch_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Branch_apply_diff_binary_read(baseline: Branch, diff: Loom.Serialized): Loom.Result<Branch, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 1
  const leaves_length_changed = buffer_readbits(b, 0, 1) == 1
  local leaves = nil
  if leaves_length_changed then
    const read_0 = buffer_readu32(b, offset)
    offset += 4
    const leaves_2_count = read_0
    if buffer_len(b) < offset + leaves_2_count then
      return { ok = false, error = { kind = "invalid_length", field = "leaves" } }
    end
    const leaves_2 = {}
    for i = 1, leaves_2_count do
      const read_1 = buffer_readu8(b, offset)
      offset += 1
      leaves_2[i] = { v = read_1 }
    end
    leaves = leaves_2
  else
    const leaves_count = #baseline.leaves
    const leaves_bits = offset * 8
    offset += (leaves_count + 7) // 8
    leaves = {}
    for i_2 = 1, leaves_count do
      const v_changed = buffer_readbits(b, leaves_bits + (i_2 - 1), 1) == 1
      local v = nil
      if v_changed then
        const read_2 = buffer_readu8(b, offset)
        offset += 1
        v = read_2
      else
        v = baseline.leaves[i_2].v
      end
      leaves[i_2] = { v = v }
    end
  end
  return { ok = true, value = { leaves = leaves } }
end
const function Branch_apply_diff_binary(baseline: Branch, diff: Loom.Serialized): Loom.Result<Branch, Loom.DeserializeError>
  const ok, result = pcall(Branch_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Branch_serializer: Loom.Serializer<Branch> = { serialize = Branch_serialize_binary, deserialize = Branch_deserialize_binary, diff = Branch_diff_binary, applyDiff = Branch_apply_diff_binary }