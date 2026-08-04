const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writeu8 = buffer.writeu8
const buffer_writebits = buffer.writebits
const buffer_writei16 = buffer.writei16
const buffer_len = buffer.len
const buffer_readu8 = buffer.readu8
const buffer_readbits = buffer.readbits
const buffer_readi16 = buffer.readi16
const buffer_copy = buffer.copy
type Entity = {
  read id: number,
  read target: number?,
}
const function Entity_serialize_binary(value: Entity): Loom.Serialized
  const b = buffer_create(2 + (if value.target ~= nil then 2 else 0))
  buffer_writeu8(b, 1, value.id)
  buffer_writebits(b, 0, 1, if value.target ~= nil then 1 else 0)
  local offset = 2
  if value.target ~= nil then
    buffer_writei16(b, offset, value.target)
    offset += 2
  end
  return { buffer = b }
end
const function Entity_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Entity, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 2 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const target_present = buffer_readbits(b, 0, 1) == 1
  local target = nil
  local offset = 2
  if target_present then
    if buffer_len(b) < offset + 2 then
      return { ok = false, error = { kind = "truncated", field = "target" } }
    end
    const read_0 = buffer_readi16(b, offset)
    offset += 2
    target = read_0
  end
  return { ok = true, value = { id = buffer_readu8(b, 1), target = target } }
end
const function Entity_diff_binary_write(baseline: Entity, value: Entity, b: buffer)
  local offset = 1
  const id_changed = not Loom.deep_equal(baseline.id, value.id)
  buffer_writebits(b, 0, 1, if id_changed then 1 else 0)
  if id_changed then
    buffer_writeu8(b, offset, value.id)
    offset += 1
  end
  const target_presence_changed = (baseline.target ~= nil) ~= (value.target ~= nil)
  buffer_writebits(b, 1, 1, if target_presence_changed then 1 else 0)
  if target_presence_changed then
    buffer_writebits(b, 2, 1, if value.target ~= nil then 1 else 0)
    if value.target ~= nil then
      buffer_writei16(b, offset, value.target)
      offset += 2
    end
  else
    if value.target ~= nil then
      const target_changed = not Loom.deep_equal(baseline.target, value.target)
      buffer_writebits(b, 2, 1, if target_changed then 1 else 0)
      if target_changed then
        buffer_writei16(b, offset, value.target)
        offset += 2
      end
    end
  end
  return offset
end
const function Entity_diff_binary_attempt(baseline: Entity, value: Entity, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Entity_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Entity_diff_binary_attempt(baseline, value, size * 2)
end
const function Entity_diff_binary(baseline: Entity, value: Entity): Loom.Serialized
  const baseline_s = Entity_serialize_binary(baseline)
  const value_s = Entity_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written = Entity_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Entity_apply_diff_binary_read(baseline: Entity, diff: Loom.Serialized): Loom.Result<Entity, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 1
  const id_changed = buffer_readbits(b, 0, 1) == 1
  local id = nil
  if id_changed then
    const read_0 = buffer_readu8(b, offset)
    offset += 1
    id = read_0
  else
    id = baseline.id
  end
  const target_presence_changed = buffer_readbits(b, 1, 1) == 1
  local target = nil
  if target_presence_changed then
    const target_2_present = buffer_readbits(b, 2, 1) == 1
    local target_2 = nil
    if target_2_present then
      if buffer_len(b) < offset + 2 then
        return { ok = false, error = { kind = "truncated", field = "target" } }
      end
      const read_1 = buffer_readi16(b, offset)
      offset += 2
      target_2 = read_1
    end
    target = target_2
  else
    if baseline.target ~= nil then
      const target_changed = buffer_readbits(b, 3, 1) == 1
      local target_3 = nil
      if target_changed then
        const read_2 = buffer_readi16(b, offset)
        offset += 2
        target_3 = read_2
      else
        target_3 = baseline.target
      end
      target = target_3
    end
  end
  return { ok = true, value = { id = id, target = target } }
end
const function Entity_apply_diff_binary(baseline: Entity, diff: Loom.Serialized): Loom.Result<Entity, Loom.DeserializeError>
  const ok, result = pcall(Entity_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Entity_serializer: Loom.Serializer<Entity> = { serialize = Entity_serialize_binary, deserialize = Entity_deserialize_binary, diff = Entity_diff_binary, applyDiff = Entity_apply_diff_binary }