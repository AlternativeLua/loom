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
  read position: Vector3,
}
const function Entity_serialize_binary(value: Entity): Loom.Serialized
  const position_value = value.position
  local position_sentinel = 0
  if position_value == Vector3.zero then
    position_sentinel = 1
  elseif position_value == Vector3.one then
    position_sentinel = 2
  elseif position_value == Vector3.xAxis then
    position_sentinel = 3
  elseif position_value == Vector3.yAxis then
    position_sentinel = 4
  elseif position_value == Vector3.zAxis then
    position_sentinel = 5
  end
  const b = buffer_create(2 + (if position_sentinel == 0 then 6 else 0))
  buffer_writeu8(b, 1, value.id)
  buffer_writebits(b, 0, 3, position_sentinel)
  local offset = 2
  if position_sentinel == 0 then
    buffer_writei16(b, offset, position_value.X)
    offset += 2
    buffer_writei16(b, offset, position_value.Y)
    offset += 2
    buffer_writei16(b, offset, position_value.Z)
    offset += 2
  end
  return { buffer = b }
end
const function Entity_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Entity, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 2 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const position_sentinel = buffer_readbits(b, 0, 3)
  local position = nil
  local offset = 2
  if position_sentinel == 0 then
    if buffer_len(b) < offset + 6 then
      return { ok = false, error = { kind = "truncated", field = "position" } }
    end
    const read_0 = buffer_readi16(b, offset)
    offset += 2
    const read_1 = buffer_readi16(b, offset)
    offset += 2
    const read_2 = buffer_readi16(b, offset)
    offset += 2
    position = Vector3.new(read_0, read_1, read_2)
  elseif position_sentinel == 1 then
    position = Vector3.zero
  elseif position_sentinel == 2 then
    position = Vector3.one
  elseif position_sentinel == 3 then
    position = Vector3.xAxis
  elseif position_sentinel == 4 then
    position = Vector3.yAxis
  elseif position_sentinel == 5 then
    position = Vector3.zAxis
  else
    return { ok = false, error = { kind = "invalid_tag", field = "position" } }
  end
  return { ok = true, value = { id = buffer_readu8(b, 1), position = position } }
end
const function Entity_diff_binary_write(baseline: Entity, value: Entity, b: buffer)
  local offset = 1
  const id_changed = not Loom.deep_equal(baseline.id, value.id)
  buffer_writebits(b, 0, 1, if id_changed then 1 else 0)
  if id_changed then
    buffer_writeu8(b, offset, value.id)
    offset += 1
  end
  const position_changed = not Loom.deep_equal(baseline.position, value.position)
  buffer_writebits(b, 1, 1, if position_changed then 1 else 0)
  if position_changed then
    buffer_writebits(b, 2, 3, position_sentinel)
    if position_sentinel == 0 then
      buffer_writei16(b, offset, position_value.X)
      offset += 2
      buffer_writei16(b, offset, position_value.Y)
      offset += 2
      buffer_writei16(b, offset, position_value.Z)
      offset += 2
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
  const position_changed = buffer_readbits(b, 1, 1) == 1
  local position = nil
  if position_changed then
    const position_2_sentinel = buffer_readbits(b, 2, 3)
    local position_2 = nil
    if position_2_sentinel == 0 then
      if buffer_len(b) < offset + 6 then
        return { ok = false, error = { kind = "truncated", field = "position" } }
      end
      const read_1 = buffer_readi16(b, offset)
      offset += 2
      const read_2 = buffer_readi16(b, offset)
      offset += 2
      const read_3 = buffer_readi16(b, offset)
      offset += 2
      position_2 = Vector3.new(read_1, read_2, read_3)
    elseif position_2_sentinel == 1 then
      position_2 = Vector3.zero
    elseif position_2_sentinel == 2 then
      position_2 = Vector3.one
    elseif position_2_sentinel == 3 then
      position_2 = Vector3.xAxis
    elseif position_2_sentinel == 4 then
      position_2 = Vector3.yAxis
    elseif position_2_sentinel == 5 then
      position_2 = Vector3.zAxis
    else
      return { ok = false, error = { kind = "invalid_tag", field = "position" } }
    end
    position = position_2
  else
    position = baseline.position
  end
  return { ok = true, value = { id = id, position = position } }
end
const function Entity_apply_diff_binary(baseline: Entity, diff: Loom.Serialized): Loom.Result<Entity, Loom.DeserializeError>
  const ok, result = pcall(Entity_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Entity_serializer: Loom.Serializer<Entity> = { serialize = Entity_serialize_binary, deserialize = Entity_deserialize_binary, diff = Entity_diff_binary, applyDiff = Entity_apply_diff_binary }