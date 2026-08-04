const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writeu8 = buffer.writeu8
const buffer_len = buffer.len
const buffer_readu8 = buffer.readu8
const buffer_writebits = buffer.writebits
const buffer_copy = buffer.copy
const buffer_readbits = buffer.readbits
const buffer_writeu32 = buffer.writeu32
const buffer_writestring = buffer.writestring
const buffer_writef32 = buffer.writef32
const buffer_readu32 = buffer.readu32
const buffer_readstring = buffer.readstring
const buffer_readf32 = buffer.readf32
type Leaf = {
  read n: number,
}
const function Leaf_serialize_binary(value: Leaf): Loom.Serialized
  const b = buffer_create(1)
  buffer_writeu8(b, 0, value.n)
  return { buffer = b }
end
const function Leaf_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Leaf, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  return { ok = true, value = { n = buffer_readu8(b, 0) } }
end
const function Leaf_diff_binary_write(baseline: Leaf, value: Leaf, b: buffer)
  local offset = 1
  const n_changed = not Loom.deep_equal(baseline.n, value.n)
  buffer_writebits(b, 0, 1, if n_changed then 1 else 0)
  if n_changed then
    buffer_writeu8(b, offset, value.n)
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
  const n_changed = buffer_readbits(b, 0, 1) == 1
  local n = nil
  if n_changed then
    const read_0 = buffer_readu8(b, offset)
    offset += 1
    n = read_0
  else
    n = baseline.n
  end
  return { ok = true, value = { n = n } }
end
const function Leaf_apply_diff_binary(baseline: Leaf, diff: Loom.Serialized): Loom.Result<Leaf, Loom.DeserializeError>
  const ok, result = pcall(Leaf_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Leaf_serializer: Loom.Serializer<Leaf> = { serialize = Leaf_serialize_binary, deserialize = Leaf_deserialize_binary, diff = Leaf_diff_binary, applyDiff = Leaf_apply_diff_binary }
type Compositions = {
  read maybe_map: Record<string, number>?,
  read map_list: { Record<string, Leaf> },
  read sparse: Record<string, string?>,
  read maybe_leaf: Leaf?,
  read maybe_names: { string }?,
}
const function Compositions_serialize_binary(value: Compositions): Loom.Serialized
  local size = 1 + (if value.maybe_leaf ~= nil then 1 else 0)
  if value.maybe_map ~= nil then
    size += 4
    local maybe_map_measured = 0
    for maybe_map_k, maybe_map_v in value.maybe_map do
      maybe_map_measured += 1
      size += 4 + #maybe_map_k
      size += 4
    end
  end
  size += 4
  for i = 1, #value.map_list do
    size += 4
    local map_list_element_measured = 0
    for map_list_element_k, map_list_element_v in value.map_list[i] do
      map_list_element_measured += 1
      size += 4 + #map_list_element_k
      size += 1
    end
  end
  size += 4
  local sparse_measured = 0
  for sparse_k, sparse_v in value.sparse do
    sparse_measured += 1
    size += 4 + #sparse_k
    if sparse_v ~= nil then
      size += 4 + #sparse_v
    end
  end
  size += (sparse_measured + 7) // 8
  if value.maybe_names ~= nil then
    size += 4
    for i_2 = 1, #value.maybe_names do
      size += 4 + #value.maybe_names[i_2]
    end
  end
  const b = buffer_create(size)
  buffer_writebits(b, 0, 1, if value.maybe_map ~= nil then 1 else 0)
  local offset = 1
  if value.maybe_map ~= nil then
    local maybe_map_written = 0
    for maybe_map_key in value.maybe_map do
      maybe_map_written += 1
    end
    buffer_writeu32(b, offset, maybe_map_written)
    offset += 4
    for maybe_map_k_out, maybe_map_v_out in value.maybe_map do
      const maybe_map_k_length = #maybe_map_k_out
      buffer_writeu32(b, offset, maybe_map_k_length)
      offset += 4
      buffer_writestring(b, offset, maybe_map_k_out)
      offset += maybe_map_k_length
      buffer_writef32(b, offset, maybe_map_v_out)
      offset += 4
    end
  end
  buffer_writeu32(b, offset, #value.map_list)
  offset += 4
  for i_3 = 1, #value.map_list do
    const map_list_item = value.map_list[i_3]
    local map_list_element_written = 0
    for map_list_element_key in map_list_item do
      map_list_element_written += 1
    end
    buffer_writeu32(b, offset, map_list_element_written)
    offset += 4
    for map_list_element_k_out, map_list_element_v_out in map_list_item do
      const map_list_element_k_length = #map_list_element_k_out
      buffer_writeu32(b, offset, map_list_element_k_length)
      offset += 4
      buffer_writestring(b, offset, map_list_element_k_out)
      offset += map_list_element_k_length
      buffer_writeu8(b, offset, map_list_element_v_out.n)
      offset += 1
    end
  end
  local sparse_written = 0
  for sparse_key in value.sparse do
    sparse_written += 1
  end
  buffer_writeu32(b, offset, sparse_written)
  offset += 4
  const sparse_bits = offset * 8
  offset += (sparse_written + 7) // 8
  local sparse_index = 1
  for sparse_k_out, sparse_v_out in value.sparse do
    const sparse_k_length = #sparse_k_out
    buffer_writeu32(b, offset, sparse_k_length)
    offset += 4
    buffer_writestring(b, offset, sparse_k_out)
    offset += sparse_k_length
    buffer_writebits(b, sparse_bits + (sparse_index - 1), 1, if sparse_v_out ~= nil then 1 else 0)
    if sparse_v_out ~= nil then
      const sparse_v_length = #sparse_v_out
      buffer_writeu32(b, offset, sparse_v_length)
      offset += 4
      buffer_writestring(b, offset, sparse_v_out)
      offset += sparse_v_length
    end
    sparse_index += 1
  end
  buffer_writebits(b, 1, 1, if value.maybe_leaf ~= nil then 1 else 0)
  if value.maybe_leaf ~= nil then
    buffer_writeu8(b, offset, value.maybe_leaf.n)
    offset += 1
  end
  buffer_writebits(b, 2, 1, if value.maybe_names ~= nil then 1 else 0)
  if value.maybe_names ~= nil then
    buffer_writeu32(b, offset, #value.maybe_names)
    offset += 4
    for i_4 = 1, #value.maybe_names do
      const maybe_names_item = value.maybe_names[i_4]
      const maybe_names_element_length = #maybe_names_item
      buffer_writeu32(b, offset, maybe_names_element_length)
      offset += 4
      buffer_writestring(b, offset, maybe_names_item)
      offset += maybe_names_element_length
    end
  end
  return { buffer = b }
end
const function Compositions_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Compositions, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 9 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const maybe_map_present = buffer_readbits(b, 0, 1) == 1
  local maybe_map = nil
  local offset = 1
  if maybe_map_present then
    const read_0 = buffer_readu32(b, offset)
    offset += 4
    const maybe_map_2_count = read_0
    const maybe_map_2 = {}
    for i = 1, maybe_map_2_count do
      const read_1 = buffer_readu32(b, offset)
      offset += 4
      const maybe_map_k_length = read_1
      if buffer_len(b) < offset + maybe_map_k_length then
        return { ok = false, error = { kind = "invalid_length", field = "maybe_map[k]" } }
      end
      const maybe_map_k = buffer_readstring(b, offset, maybe_map_k_length)
      offset += maybe_map_k_length
      const read_2 = buffer_readf32(b, offset)
      offset += 4
      maybe_map_2[maybe_map_k] = read_2
    end
    maybe_map = maybe_map_2
  end
  const read_3 = buffer_readu32(b, offset)
  offset += 4
  const map_list_count = read_3
  const map_list = {}
  for i_2 = 1, map_list_count do
    const read_4 = buffer_readu32(b, offset)
    offset += 4
    const map_list_element_count = read_4
    const map_list_element = {}
    for i_3 = 1, map_list_element_count do
      const read_5 = buffer_readu32(b, offset)
      offset += 4
      const map_list_element_k_length = read_5
      if buffer_len(b) < offset + map_list_element_k_length then
        return { ok = false, error = { kind = "invalid_length", field = "map_list[][k]" } }
      end
      const map_list_element_k = buffer_readstring(b, offset, map_list_element_k_length)
      offset += map_list_element_k_length
      const read_6 = buffer_readu8(b, offset)
      offset += 1
      map_list_element[map_list_element_k] = { n = read_6 }
    end
    map_list[i_2] = map_list_element
  end
  const read_7 = buffer_readu32(b, offset)
  offset += 4
  const sparse_count = read_7
  const sparse_bits = offset * 8
  offset += (sparse_count + 7) // 8
  const sparse = {}
  for i_4 = 1, sparse_count do
    const read_8 = buffer_readu32(b, offset)
    offset += 4
    const sparse_k_length = read_8
    if buffer_len(b) < offset + sparse_k_length then
      return { ok = false, error = { kind = "invalid_length", field = "sparse[k]" } }
    end
    const sparse_k = buffer_readstring(b, offset, sparse_k_length)
    offset += sparse_k_length
    const sparse_v_present = buffer_readbits(b, sparse_bits + (i_4 - 1), 1) == 1
    local sparse_v = nil
    if sparse_v_present then
      const read_9 = buffer_readu32(b, offset)
      offset += 4
      const sparse_v_2_length = read_9
      if buffer_len(b) < offset + sparse_v_2_length then
        return { ok = false, error = { kind = "invalid_length", field = "sparse[v]" } }
      end
      const sparse_v_2 = buffer_readstring(b, offset, sparse_v_2_length)
      offset += sparse_v_2_length
      sparse_v = sparse_v_2
    end
    sparse[sparse_k] = sparse_v
  end
  const maybe_leaf_present = buffer_readbits(b, 1, 1) == 1
  local maybe_leaf = nil
  if maybe_leaf_present then
    if buffer_len(b) < offset + 1 then
      return { ok = false, error = { kind = "truncated", field = "maybe_leaf" } }
    end
    const read_10 = buffer_readu8(b, offset)
    offset += 1
    maybe_leaf = { n = read_10 }
  end
  const maybe_names_present = buffer_readbits(b, 2, 1) == 1
  local maybe_names = nil
  if maybe_names_present then
    const read_11 = buffer_readu32(b, offset)
    offset += 4
    const maybe_names_2_count = read_11
    const maybe_names_2 = {}
    for i_5 = 1, maybe_names_2_count do
      const read_12 = buffer_readu32(b, offset)
      offset += 4
      const maybe_names_element_length = read_12
      if buffer_len(b) < offset + maybe_names_element_length then
        return { ok = false, error = { kind = "invalid_length", field = "maybe_names[]" } }
      end
      const maybe_names_element = buffer_readstring(b, offset, maybe_names_element_length)
      offset += maybe_names_element_length
      maybe_names_2[i_5] = maybe_names_element
    end
    maybe_names = maybe_names_2
  end
  return { ok = true, value = { maybe_map = maybe_map, map_list = map_list, sparse = sparse, maybe_leaf = maybe_leaf, maybe_names = maybe_names } }
end
const function Compositions_diff_binary_write(baseline: Compositions, value: Compositions, b: buffer)
  local offset = 1
  const maybe_map_presence_changed = (baseline.maybe_map ~= nil) ~= (value.maybe_map ~= nil)
  buffer_writebits(b, 0, 1, if maybe_map_presence_changed then 1 else 0)
  if maybe_map_presence_changed then
    buffer_writebits(b, 1, 1, if value.maybe_map ~= nil then 1 else 0)
    if value.maybe_map ~= nil then
      local maybe_map_written = 0
      for maybe_map_key in value.maybe_map do
        maybe_map_written += 1
      end
      buffer_writeu32(b, offset, maybe_map_written)
      offset += 4
      for maybe_map_k_out, maybe_map_v_out in value.maybe_map do
        const maybe_map_k_length = #maybe_map_k_out
        buffer_writeu32(b, offset, maybe_map_k_length)
        offset += 4
        buffer_writestring(b, offset, maybe_map_k_out)
        offset += maybe_map_k_length
        buffer_writef32(b, offset, maybe_map_v_out)
        offset += 4
      end
    end
  else
    if value.maybe_map ~= nil then
      const maybe_map_removed = {}
      const maybe_map_added = {}
      const maybe_map_changed = {}
      for maybe_map_current_key, maybe_map_current_entry in value.maybe_map do
        const maybe_map_baseline_entry = baseline.maybe_map[maybe_map_current_key]
        if maybe_map_baseline_entry == nil then
          table.insert(maybe_map_added, maybe_map_current_key)
        else
          if not Loom.deep_equal(maybe_map_baseline_entry, maybe_map_current_entry) then
            table.insert(maybe_map_changed, maybe_map_current_key)
          end
        end
      end
      for maybe_map_baseline_key in baseline.maybe_map do
        if value.maybe_map[maybe_map_baseline_key] == nil then
          table.insert(maybe_map_removed, maybe_map_baseline_key)
        end
      end
      buffer_writeu32(b, offset, #maybe_map_removed)
      offset += 4
      for i = 1, #maybe_map_removed do
        const maybe_map_key_i = maybe_map_removed[i]
        const maybe_map_k_length_2 = #maybe_map_key_i
        buffer_writeu32(b, offset, maybe_map_k_length_2)
        offset += 4
        buffer_writestring(b, offset, maybe_map_key_i)
        offset += maybe_map_k_length_2
      end
      buffer_writeu32(b, offset, #maybe_map_added)
      offset += 4
      for i_2 = 1, #maybe_map_added do
        const maybe_map_key_i_2 = maybe_map_added[i_2]
        const maybe_map_k_length_3 = #maybe_map_key_i_2
        buffer_writeu32(b, offset, maybe_map_k_length_3)
        offset += 4
        buffer_writestring(b, offset, maybe_map_key_i_2)
        offset += maybe_map_k_length_3
        buffer_writef32(b, offset, value.maybe_map[maybe_map_key_i_2])
        offset += 4
      end
      buffer_writeu32(b, offset, #maybe_map_changed)
      offset += 4
      const maybe_map_entry_bits = offset * 8
      offset += (#maybe_map_changed + 7) // 8
      for i_3 = 1, #maybe_map_changed do
        const maybe_map_key_i_3 = maybe_map_changed[i_3]
        const maybe_map_k_length_4 = #maybe_map_key_i_3
        buffer_writeu32(b, offset, maybe_map_k_length_4)
        offset += 4
        buffer_writestring(b, offset, maybe_map_key_i_3)
        offset += maybe_map_k_length_4
        const maybe_map_v_changed = not Loom.deep_equal(baseline.maybe_map[maybe_map_key_i_3], value.maybe_map[maybe_map_key_i_3])
        buffer_writebits(b, maybe_map_entry_bits + (i_3 - 1), 1, if maybe_map_v_changed then 1 else 0)
        if maybe_map_v_changed then
          buffer_writef32(b, offset, value.maybe_map[maybe_map_key_i_3])
          offset += 4
        end
      end
    end
  end
  const map_list_length_changed = #baseline.map_list ~= #value.map_list
  buffer_writebits(b, 2, 1, if map_list_length_changed then 1 else 0)
  if map_list_length_changed then
    buffer_writeu32(b, offset, #value.map_list)
    offset += 4
    for i_4 = 1, #value.map_list do
      const map_list_item = value.map_list[i_4]
      local map_list_element_written = 0
      for map_list_element_key in map_list_item do
        map_list_element_written += 1
      end
      buffer_writeu32(b, offset, map_list_element_written)
      offset += 4
      for map_list_element_k_out, map_list_element_v_out in map_list_item do
        const map_list_element_k_length = #map_list_element_k_out
        buffer_writeu32(b, offset, map_list_element_k_length)
        offset += 4
        buffer_writestring(b, offset, map_list_element_k_out)
        offset += map_list_element_k_length
        buffer_writeu8(b, offset, map_list_element_v_out.n)
        offset += 1
      end
    end
  else
    for i_5 = 1, #value.map_list do
      const map_list_element_removed = {}
      const map_list_element_added = {}
      const map_list_element_changed = {}
      for map_list_element_current_key, map_list_element_current_entry in value.map_list[i_5] do
        const map_list_element_baseline_entry = baseline.map_list[i_5][map_list_element_current_key]
        if map_list_element_baseline_entry == nil then
          table.insert(map_list_element_added, map_list_element_current_key)
        else
          if not Loom.deep_equal(map_list_element_baseline_entry, map_list_element_current_entry) then
            table.insert(map_list_element_changed, map_list_element_current_key)
          end
        end
      end
      for map_list_element_baseline_key in baseline.map_list[i_5] do
        if value.map_list[i_5][map_list_element_baseline_key] == nil then
          table.insert(map_list_element_removed, map_list_element_baseline_key)
        end
      end
      buffer_writeu32(b, offset, #map_list_element_removed)
      offset += 4
      for i_6 = 1, #map_list_element_removed do
        const map_list_element_key_i_6 = map_list_element_removed[i_6]
        const map_list_element_k_length_2 = #map_list_element_key_i_6
        buffer_writeu32(b, offset, map_list_element_k_length_2)
        offset += 4
        buffer_writestring(b, offset, map_list_element_key_i_6)
        offset += map_list_element_k_length_2
      end
      buffer_writeu32(b, offset, #map_list_element_added)
      offset += 4
      for i_7 = 1, #map_list_element_added do
        const map_list_element_key_i_7 = map_list_element_added[i_7]
        const map_list_element_k_length_3 = #map_list_element_key_i_7
        buffer_writeu32(b, offset, map_list_element_k_length_3)
        offset += 4
        buffer_writestring(b, offset, map_list_element_key_i_7)
        offset += map_list_element_k_length_3
        buffer_writeu8(b, offset, value.map_list[i_5][map_list_element_key_i_7].n)
        offset += 1
      end
      buffer_writeu32(b, offset, #map_list_element_changed)
      offset += 4
      const map_list_element_entry_bits = offset * 8
      offset += (#map_list_element_changed + 7) // 8
      for i_8 = 1, #map_list_element_changed do
        const map_list_element_key_i_8 = map_list_element_changed[i_8]
        const map_list_element_k_length_4 = #map_list_element_key_i_8
        buffer_writeu32(b, offset, map_list_element_k_length_4)
        offset += 4
        buffer_writestring(b, offset, map_list_element_key_i_8)
        offset += map_list_element_k_length_4
        const n_changed = not Loom.deep_equal(baseline.map_list[i_5][map_list_element_key_i_8].n, value.map_list[i_5][map_list_element_key_i_8].n)
        buffer_writebits(b, map_list_element_entry_bits + (i_8 - 1), 1, if n_changed then 1 else 0)
        if n_changed then
          buffer_writeu8(b, offset, value.map_list[i_5][map_list_element_key_i_8].n)
          offset += 1
        end
      end
    end
  end
  const sparse_removed = {}
  const sparse_added = {}
  const sparse_changed = {}
  for sparse_current_key, sparse_current_entry in value.sparse do
    const sparse_baseline_entry = baseline.sparse[sparse_current_key]
    if sparse_baseline_entry == nil then
      table.insert(sparse_added, sparse_current_key)
    else
      if not Loom.deep_equal(sparse_baseline_entry, sparse_current_entry) then
        table.insert(sparse_changed, sparse_current_key)
      end
    end
  end
  for sparse_baseline_key in baseline.sparse do
    if value.sparse[sparse_baseline_key] == nil then
      table.insert(sparse_removed, sparse_baseline_key)
    end
  end
  buffer_writeu32(b, offset, #sparse_removed)
  offset += 4
  for i_9 = 1, #sparse_removed do
    const sparse_key_i_9 = sparse_removed[i_9]
    const sparse_k_length = #sparse_key_i_9
    buffer_writeu32(b, offset, sparse_k_length)
    offset += 4
    buffer_writestring(b, offset, sparse_key_i_9)
    offset += sparse_k_length
  end
  buffer_writeu32(b, offset, #sparse_added)
  offset += 4
  for i_10 = 1, #sparse_added do
    const sparse_key_i_10 = sparse_added[i_10]
    const sparse_k_length_2 = #sparse_key_i_10
    buffer_writeu32(b, offset, sparse_k_length_2)
    offset += 4
    buffer_writestring(b, offset, sparse_key_i_10)
    offset += sparse_k_length_2
    buffer_writebits(b, 3, 1, if value.sparse[sparse_key_i_10] ~= nil then 1 else 0)
    if value.sparse[sparse_key_i_10] ~= nil then
      const sparse_v_value = value.sparse[sparse_key_i_10]
      const sparse_v_length = #sparse_v_value
      buffer_writeu32(b, offset, sparse_v_length)
      offset += 4
      buffer_writestring(b, offset, sparse_v_value)
      offset += sparse_v_length
    end
  end
  buffer_writeu32(b, offset, #sparse_changed)
  offset += 4
  const sparse_entry_bits = offset * 8
  offset += (#sparse_changed * 2 + 7) // 8
  for i_11 = 1, #sparse_changed do
    const sparse_key_i_11 = sparse_changed[i_11]
    const sparse_k_length_3 = #sparse_key_i_11
    buffer_writeu32(b, offset, sparse_k_length_3)
    offset += 4
    buffer_writestring(b, offset, sparse_key_i_11)
    offset += sparse_k_length_3
    const sparse_v_presence_changed = (baseline.sparse[sparse_key_i_11] ~= nil) ~= (value.sparse[sparse_key_i_11] ~= nil)
    buffer_writebits(b, sparse_entry_bits + (i_11 - 1) * 2, 1, if sparse_v_presence_changed then 1 else 0)
    if sparse_v_presence_changed then
      buffer_writebits(b, sparse_entry_bits + (i_11 - 1) * 2 + 1, 1, if value.sparse[sparse_key_i_11] ~= nil then 1 else 0)
      if value.sparse[sparse_key_i_11] ~= nil then
        const sparse_v_value_2 = value.sparse[sparse_key_i_11]
        const sparse_v_length_2 = #sparse_v_value_2
        buffer_writeu32(b, offset, sparse_v_length_2)
        offset += 4
        buffer_writestring(b, offset, sparse_v_value_2)
        offset += sparse_v_length_2
      end
    else
      if value.sparse[sparse_key_i_11] ~= nil then
        const sparse_v_changed = not Loom.deep_equal(baseline.sparse[sparse_key_i_11], value.sparse[sparse_key_i_11])
        buffer_writebits(b, sparse_entry_bits + (i_11 - 1) * 2 + 1, 1, if sparse_v_changed then 1 else 0)
        if sparse_v_changed then
          const sparse_v_value_3 = value.sparse[sparse_key_i_11]
          const sparse_v_length_3 = #sparse_v_value_3
          buffer_writeu32(b, offset, sparse_v_length_3)
          offset += 4
          buffer_writestring(b, offset, sparse_v_value_3)
          offset += sparse_v_length_3
        end
      end
    end
  end
  const maybe_leaf_presence_changed = (baseline.maybe_leaf ~= nil) ~= (value.maybe_leaf ~= nil)
  buffer_writebits(b, 4, 1, if maybe_leaf_presence_changed then 1 else 0)
  if maybe_leaf_presence_changed then
    buffer_writebits(b, 5, 1, if value.maybe_leaf ~= nil then 1 else 0)
    if value.maybe_leaf ~= nil then
      buffer_writeu8(b, offset, value.maybe_leaf.n)
      offset += 1
    end
  else
    if value.maybe_leaf ~= nil then
      const n_changed_2 = not Loom.deep_equal(baseline.maybe_leaf.n, value.maybe_leaf.n)
      buffer_writebits(b, 5, 1, if n_changed_2 then 1 else 0)
      if n_changed_2 then
        buffer_writeu8(b, offset, value.maybe_leaf.n)
        offset += 1
      end
    end
  end
  const maybe_names_presence_changed = (baseline.maybe_names ~= nil) ~= (value.maybe_names ~= nil)
  buffer_writebits(b, 6, 1, if maybe_names_presence_changed then 1 else 0)
  if maybe_names_presence_changed then
    buffer_writebits(b, 7, 1, if value.maybe_names ~= nil then 1 else 0)
    if value.maybe_names ~= nil then
      buffer_writeu32(b, offset, #value.maybe_names)
      offset += 4
      for i_12 = 1, #value.maybe_names do
        const maybe_names_item = value.maybe_names[i_12]
        const maybe_names_element_length = #maybe_names_item
        buffer_writeu32(b, offset, maybe_names_element_length)
        offset += 4
        buffer_writestring(b, offset, maybe_names_item)
        offset += maybe_names_element_length
      end
    end
  else
    if value.maybe_names ~= nil then
      const maybe_names_length_changed = #baseline.maybe_names ~= #value.maybe_names
      buffer_writebits(b, 7, 1, if maybe_names_length_changed then 1 else 0)
      if maybe_names_length_changed then
        buffer_writeu32(b, offset, #value.maybe_names)
        offset += 4
        for i_13 = 1, #value.maybe_names do
          const maybe_names_item_2 = value.maybe_names[i_13]
          const maybe_names_element_length_2 = #maybe_names_item_2
          buffer_writeu32(b, offset, maybe_names_element_length_2)
          offset += 4
          buffer_writestring(b, offset, maybe_names_item_2)
          offset += maybe_names_element_length_2
        end
      else
        const maybe_names_bits = offset * 8
        offset += (#value.maybe_names + 7) // 8
        for i_14 = 1, #value.maybe_names do
          const maybe_names_element_changed = not Loom.deep_equal(baseline.maybe_names[i_14], value.maybe_names[i_14])
          buffer_writebits(b, maybe_names_bits + (i_14 - 1), 1, if maybe_names_element_changed then 1 else 0)
          if maybe_names_element_changed then
            const maybe_names_element_value = value.maybe_names[i_14]
            const maybe_names_element_length_3 = #maybe_names_element_value
            buffer_writeu32(b, offset, maybe_names_element_length_3)
            offset += 4
            buffer_writestring(b, offset, maybe_names_element_value)
            offset += maybe_names_element_length_3
          end
        end
      end
    end
  end
  return offset
end
const function Compositions_diff_binary_attempt(baseline: Compositions, value: Compositions, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Compositions_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Compositions_diff_binary_attempt(baseline, value, size * 2)
end
const function Compositions_diff_binary(baseline: Compositions, value: Compositions): Loom.Serialized
  const baseline_s = Compositions_serialize_binary(baseline)
  const value_s = Compositions_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written = Compositions_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Compositions_apply_diff_binary_read(baseline: Compositions, diff: Loom.Serialized): Loom.Result<Compositions, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 1
  const maybe_map_presence_changed = buffer_readbits(b, 0, 1) == 1
  local maybe_map = nil
  if maybe_map_presence_changed then
    const maybe_map_2_present = buffer_readbits(b, 1, 1) == 1
    local maybe_map_2 = nil
    if maybe_map_2_present then
      const read_0 = buffer_readu32(b, offset)
      offset += 4
      const maybe_map_3_count = read_0
      const maybe_map_3 = {}
      for i = 1, maybe_map_3_count do
        const read_1 = buffer_readu32(b, offset)
        offset += 4
        const maybe_map_k_length = read_1
        if buffer_len(b) < offset + maybe_map_k_length then
          return { ok = false, error = { kind = "invalid_length", field = "maybe_map[k]" } }
        end
        const maybe_map_k = buffer_readstring(b, offset, maybe_map_k_length)
        offset += maybe_map_k_length
        const read_2 = buffer_readf32(b, offset)
        offset += 4
        maybe_map_3[maybe_map_k] = read_2
      end
      maybe_map_2 = maybe_map_3
    end
    maybe_map = maybe_map_2
  else
    if baseline.maybe_map ~= nil then
      const maybe_map_4 = {}
      for maybe_map_copy_key, maybe_map_copy_value in baseline.maybe_map do
        maybe_map_4[maybe_map_copy_key] = maybe_map_copy_value
      end
      const read_3 = buffer_readu32(b, offset)
      offset += 4
      const maybe_map_removed_count = read_3
      for i_2 = 1, maybe_map_removed_count do
        const read_4 = buffer_readu32(b, offset)
        offset += 4
        const maybe_map_k_2_length = read_4
        if buffer_len(b) < offset + maybe_map_k_2_length then
          return { ok = false, error = { kind = "invalid_length", field = "maybe_map[k]" } }
        end
        const maybe_map_k_2 = buffer_readstring(b, offset, maybe_map_k_2_length)
        offset += maybe_map_k_2_length
        const maybe_map_removed_key = maybe_map_k_2
        maybe_map_4[maybe_map_removed_key] = nil
      end
      const read_5 = buffer_readu32(b, offset)
      offset += 4
      const maybe_map_added_count = read_5
      for i_3 = 1, maybe_map_added_count do
        const read_6 = buffer_readu32(b, offset)
        offset += 4
        const maybe_map_k_3_length = read_6
        if buffer_len(b) < offset + maybe_map_k_3_length then
          return { ok = false, error = { kind = "invalid_length", field = "maybe_map[k]" } }
        end
        const maybe_map_k_3 = buffer_readstring(b, offset, maybe_map_k_3_length)
        offset += maybe_map_k_3_length
        const maybe_map_added_key = maybe_map_k_3
        const read_7 = buffer_readf32(b, offset)
        offset += 4
        maybe_map_4[maybe_map_added_key] = read_7
      end
      const read_8 = buffer_readu32(b, offset)
      offset += 4
      const maybe_map_changed_count = read_8
      const maybe_map_entry_bits = offset * 8
      offset += (maybe_map_changed_count + 7) // 8
      for i_4 = 1, maybe_map_changed_count do
        const read_9 = buffer_readu32(b, offset)
        offset += 4
        const maybe_map_k_4_length = read_9
        if buffer_len(b) < offset + maybe_map_k_4_length then
          return { ok = false, error = { kind = "invalid_length", field = "maybe_map[k]" } }
        end
        const maybe_map_k_4 = buffer_readstring(b, offset, maybe_map_k_4_length)
        offset += maybe_map_k_4_length
        const maybe_map_changed_key = maybe_map_k_4
        const maybe_map_v_changed = buffer_readbits(b, maybe_map_entry_bits + (i_4 - 1), 1) == 1
        local maybe_map_v = nil
        if maybe_map_v_changed then
          const read_10 = buffer_readf32(b, offset)
          offset += 4
          maybe_map_v = read_10
        else
          maybe_map_v = baseline.maybe_map[maybe_map_changed_key]
        end
        maybe_map_4[maybe_map_changed_key] = maybe_map_v
      end
      maybe_map = maybe_map_4
    end
  end
  const map_list_length_changed = buffer_readbits(b, 2, 1) == 1
  local map_list = nil
  if map_list_length_changed then
    const read_11 = buffer_readu32(b, offset)
    offset += 4
    const map_list_2_count = read_11
    const map_list_2 = {}
    for i_5 = 1, map_list_2_count do
      const read_12 = buffer_readu32(b, offset)
      offset += 4
      const map_list_element_count = read_12
      const map_list_element = {}
      for i_6 = 1, map_list_element_count do
        const read_13 = buffer_readu32(b, offset)
        offset += 4
        const map_list_element_k_length = read_13
        if buffer_len(b) < offset + map_list_element_k_length then
          return { ok = false, error = { kind = "invalid_length", field = "map_list[][k]" } }
        end
        const map_list_element_k = buffer_readstring(b, offset, map_list_element_k_length)
        offset += map_list_element_k_length
        const read_14 = buffer_readu8(b, offset)
        offset += 1
        map_list_element[map_list_element_k] = { n = read_14 }
      end
      map_list_2[i_5] = map_list_element
    end
    map_list = map_list_2
  else
    const map_list_count = #baseline.map_list
    map_list = {}
    for i_7 = 1, map_list_count do
      const map_list_element_2 = {}
      for map_list_element_copy_key, map_list_element_copy_value in baseline.map_list[i_7] do
        map_list_element_2[map_list_element_copy_key] = map_list_element_copy_value
      end
      const read_15 = buffer_readu32(b, offset)
      offset += 4
      const map_list_element_removed_count = read_15
      for i_8 = 1, map_list_element_removed_count do
        const read_16 = buffer_readu32(b, offset)
        offset += 4
        const map_list_element_k_2_length = read_16
        if buffer_len(b) < offset + map_list_element_k_2_length then
          return { ok = false, error = { kind = "invalid_length", field = "map_list[][k]" } }
        end
        const map_list_element_k_2 = buffer_readstring(b, offset, map_list_element_k_2_length)
        offset += map_list_element_k_2_length
        const map_list_element_removed_key = map_list_element_k_2
        map_list_element_2[map_list_element_removed_key] = nil
      end
      const read_17 = buffer_readu32(b, offset)
      offset += 4
      const map_list_element_added_count = read_17
      for i_9 = 1, map_list_element_added_count do
        const read_18 = buffer_readu32(b, offset)
        offset += 4
        const map_list_element_k_3_length = read_18
        if buffer_len(b) < offset + map_list_element_k_3_length then
          return { ok = false, error = { kind = "invalid_length", field = "map_list[][k]" } }
        end
        const map_list_element_k_3 = buffer_readstring(b, offset, map_list_element_k_3_length)
        offset += map_list_element_k_3_length
        const map_list_element_added_key = map_list_element_k_3
        const read_19 = buffer_readu8(b, offset)
        offset += 1
        map_list_element_2[map_list_element_added_key] = { n = read_19 }
      end
      const read_20 = buffer_readu32(b, offset)
      offset += 4
      const map_list_element_changed_count = read_20
      const map_list_element_entry_bits = offset * 8
      offset += (map_list_element_changed_count + 7) // 8
      for i_10 = 1, map_list_element_changed_count do
        const read_21 = buffer_readu32(b, offset)
        offset += 4
        const map_list_element_k_4_length = read_21
        if buffer_len(b) < offset + map_list_element_k_4_length then
          return { ok = false, error = { kind = "invalid_length", field = "map_list[][k]" } }
        end
        const map_list_element_k_4 = buffer_readstring(b, offset, map_list_element_k_4_length)
        offset += map_list_element_k_4_length
        const map_list_element_changed_key = map_list_element_k_4
        const n_changed = buffer_readbits(b, map_list_element_entry_bits + (i_10 - 1), 1) == 1
        local n = nil
        if n_changed then
          const read_22 = buffer_readu8(b, offset)
          offset += 1
          n = read_22
        else
          n = baseline.map_list[i_7][map_list_element_changed_key].n
        end
        map_list_element_2[map_list_element_changed_key] = { n = n }
      end
      map_list[i_7] = map_list_element_2
    end
  end
  const sparse = {}
  for sparse_copy_key, sparse_copy_value in baseline.sparse do
    sparse[sparse_copy_key] = sparse_copy_value
  end
  const read_23 = buffer_readu32(b, offset)
  offset += 4
  const sparse_removed_count = read_23
  for i_11 = 1, sparse_removed_count do
    const read_24 = buffer_readu32(b, offset)
    offset += 4
    const sparse_k_length = read_24
    if buffer_len(b) < offset + sparse_k_length then
      return { ok = false, error = { kind = "invalid_length", field = "sparse[k]" } }
    end
    const sparse_k = buffer_readstring(b, offset, sparse_k_length)
    offset += sparse_k_length
    const sparse_removed_key = sparse_k
    sparse[sparse_removed_key] = nil
  end
  const read_25 = buffer_readu32(b, offset)
  offset += 4
  const sparse_added_count = read_25
  for i_12 = 1, sparse_added_count do
    const read_26 = buffer_readu32(b, offset)
    offset += 4
    const sparse_k_2_length = read_26
    if buffer_len(b) < offset + sparse_k_2_length then
      return { ok = false, error = { kind = "invalid_length", field = "sparse[k]" } }
    end
    const sparse_k_2 = buffer_readstring(b, offset, sparse_k_2_length)
    offset += sparse_k_2_length
    const sparse_added_key = sparse_k_2
    const sparse_v_present = buffer_readbits(b, 3, 1) == 1
    local sparse_v = nil
    if sparse_v_present then
      const read_27 = buffer_readu32(b, offset)
      offset += 4
      const sparse_v_2_length = read_27
      if buffer_len(b) < offset + sparse_v_2_length then
        return { ok = false, error = { kind = "invalid_length", field = "sparse[v]" } }
      end
      const sparse_v_2 = buffer_readstring(b, offset, sparse_v_2_length)
      offset += sparse_v_2_length
      sparse_v = sparse_v_2
    end
    sparse[sparse_added_key] = sparse_v
  end
  const read_28 = buffer_readu32(b, offset)
  offset += 4
  const sparse_changed_count = read_28
  const sparse_entry_bits = offset * 8
  offset += (sparse_changed_count * 2 + 7) // 8
  for i_13 = 1, sparse_changed_count do
    const read_29 = buffer_readu32(b, offset)
    offset += 4
    const sparse_k_3_length = read_29
    if buffer_len(b) < offset + sparse_k_3_length then
      return { ok = false, error = { kind = "invalid_length", field = "sparse[k]" } }
    end
    const sparse_k_3 = buffer_readstring(b, offset, sparse_k_3_length)
    offset += sparse_k_3_length
    const sparse_changed_key = sparse_k_3
    const sparse_v_presence_changed = buffer_readbits(b, sparse_entry_bits + (i_13 - 1) * 2, 1) == 1
    local sparse_v_3 = nil
    if sparse_v_presence_changed then
      const sparse_v_4_present = buffer_readbits(b, sparse_entry_bits + (i_13 - 1) * 2 + 1, 1) == 1
      local sparse_v_4 = nil
      if sparse_v_4_present then
        const read_30 = buffer_readu32(b, offset)
        offset += 4
        const sparse_v_5_length = read_30
        if buffer_len(b) < offset + sparse_v_5_length then
          return { ok = false, error = { kind = "invalid_length", field = "sparse[v]" } }
        end
        const sparse_v_5 = buffer_readstring(b, offset, sparse_v_5_length)
        offset += sparse_v_5_length
        sparse_v_4 = sparse_v_5
      end
      sparse_v_3 = sparse_v_4
    else
      if baseline.sparse[sparse_changed_key] ~= nil then
        const sparse_v_changed = buffer_readbits(b, sparse_entry_bits + (i_13 - 1) * 2 + 1, 1) == 1
        local sparse_v_6 = nil
        if sparse_v_changed then
          const read_31 = buffer_readu32(b, offset)
          offset += 4
          const sparse_v_7_length = read_31
          if buffer_len(b) < offset + sparse_v_7_length then
            return { ok = false, error = { kind = "invalid_length", field = "sparse[v]" } }
          end
          const sparse_v_7 = buffer_readstring(b, offset, sparse_v_7_length)
          offset += sparse_v_7_length
          sparse_v_6 = sparse_v_7
        else
          sparse_v_6 = baseline.sparse[sparse_changed_key]
        end
        sparse_v_3 = sparse_v_6
      end
    end
    sparse[sparse_changed_key] = sparse_v_3
  end
  const maybe_leaf_presence_changed = buffer_readbits(b, 4, 1) == 1
  local maybe_leaf = nil
  if maybe_leaf_presence_changed then
    const maybe_leaf_2_present = buffer_readbits(b, 5, 1) == 1
    local maybe_leaf_2 = nil
    if maybe_leaf_2_present then
      if buffer_len(b) < offset + 1 then
        return { ok = false, error = { kind = "truncated", field = "maybe_leaf" } }
      end
      const read_32 = buffer_readu8(b, offset)
      offset += 1
      maybe_leaf_2 = { n = read_32 }
    end
    maybe_leaf = maybe_leaf_2
  else
    if baseline.maybe_leaf ~= nil then
      const n_changed_2 = buffer_readbits(b, 5, 1) == 1
      local n_2 = nil
      if n_changed_2 then
        const read_33 = buffer_readu8(b, offset)
        offset += 1
        n_2 = read_33
      else
        n_2 = baseline.maybe_leaf.n
      end
      maybe_leaf = { n = n_2 }
    end
  end
  const maybe_names_presence_changed = buffer_readbits(b, 6, 1) == 1
  local maybe_names = nil
  if maybe_names_presence_changed then
    const maybe_names_2_present = buffer_readbits(b, 7, 1) == 1
    local maybe_names_2 = nil
    if maybe_names_2_present then
      const read_34 = buffer_readu32(b, offset)
      offset += 4
      const maybe_names_3_count = read_34
      const maybe_names_3 = {}
      for i_14 = 1, maybe_names_3_count do
        const read_35 = buffer_readu32(b, offset)
        offset += 4
        const maybe_names_element_length = read_35
        if buffer_len(b) < offset + maybe_names_element_length then
          return { ok = false, error = { kind = "invalid_length", field = "maybe_names[]" } }
        end
        const maybe_names_element = buffer_readstring(b, offset, maybe_names_element_length)
        offset += maybe_names_element_length
        maybe_names_3[i_14] = maybe_names_element
      end
      maybe_names_2 = maybe_names_3
    end
    maybe_names = maybe_names_2
  else
    if baseline.maybe_names ~= nil then
      const maybe_names_length_changed = buffer_readbits(b, 7, 1) == 1
      local maybe_names_4 = nil
      if maybe_names_length_changed then
        const read_36 = buffer_readu32(b, offset)
        offset += 4
        const maybe_names_5_count = read_36
        const maybe_names_5 = {}
        for i_15 = 1, maybe_names_5_count do
          const read_37 = buffer_readu32(b, offset)
          offset += 4
          const maybe_names_element_2_length = read_37
          if buffer_len(b) < offset + maybe_names_element_2_length then
            return { ok = false, error = { kind = "invalid_length", field = "maybe_names[]" } }
          end
          const maybe_names_element_2 = buffer_readstring(b, offset, maybe_names_element_2_length)
          offset += maybe_names_element_2_length
          maybe_names_5[i_15] = maybe_names_element_2
        end
        maybe_names_4 = maybe_names_5
      else
        const maybe_names_count = #baseline.maybe_names
        const maybe_names_bits = offset * 8
        offset += (maybe_names_count + 7) // 8
        maybe_names_4 = {}
        for i_16 = 1, maybe_names_count do
          const maybe_names_element_changed = buffer_readbits(b, maybe_names_bits + (i_16 - 1), 1) == 1
          local maybe_names_element_3 = nil
          if maybe_names_element_changed then
            const read_38 = buffer_readu32(b, offset)
            offset += 4
            const maybe_names_element_4_length = read_38
            if buffer_len(b) < offset + maybe_names_element_4_length then
              return { ok = false, error = { kind = "invalid_length", field = "maybe_names[]" } }
            end
            const maybe_names_element_4 = buffer_readstring(b, offset, maybe_names_element_4_length)
            offset += maybe_names_element_4_length
            maybe_names_element_3 = maybe_names_element_4
          else
            maybe_names_element_3 = baseline.maybe_names[i_16]
          end
          maybe_names_4[i_16] = maybe_names_element_3
        end
      end
      maybe_names = maybe_names_4
    end
  end
  return { ok = true, value = { maybe_map = maybe_map, map_list = map_list, sparse = sparse, maybe_leaf = maybe_leaf, maybe_names = maybe_names } }
end
const function Compositions_apply_diff_binary(baseline: Compositions, diff: Loom.Serialized): Loom.Result<Compositions, Loom.DeserializeError>
  const ok, result = pcall(Compositions_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Compositions_serializer: Loom.Serializer<Compositions> = { serialize = Compositions_serialize_binary, deserialize = Compositions_deserialize_binary, diff = Compositions_diff_binary, applyDiff = Compositions_apply_diff_binary }