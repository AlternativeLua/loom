const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writeu32 = buffer.writeu32
const buffer_len = buffer.len
const buffer_readu32 = buffer.readu32
const buffer_writeu8 = buffer.writeu8
const buffer_readu8 = buffer.readu8
type Bag = {
  read owners: { Part },
}
const function Bag_serialize_binary(value: Bag): Loom.Serialized
  const blobs = {}
  const b = buffer_create(4 + #value.owners * 0)
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
  if buffer_len(b) < offset + owners_count * 0 then
    return { ok = false, error = { kind = "invalid_length", field = "owners" } }
  end
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
const Bag_serializer: Loom.Serializer<Bag> = { serialize = Bag_serialize_binary, deserialize = Bag_deserialize_binary }
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
const Leaf_serializer: Loom.Serializer<Leaf> = { serialize = Leaf_serialize_binary, deserialize = Leaf_deserialize_binary }
type Branch = {
  read leaves: { Leaf },
}
const function Branch_serialize_binary(value: Branch): Loom.Serialized
  const b = buffer_create(4 + #value.leaves * 1)
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
  if buffer_len(b) < offset + leaves_count * 1 then
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
const Branch_serializer: Loom.Serializer<Branch> = { serialize = Branch_serialize_binary, deserialize = Branch_deserialize_binary }