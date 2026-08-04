const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writeu8 = buffer.writeu8
const buffer_writei16 = buffer.writei16
const buffer_len = buffer.len
const buffer_readu8 = buffer.readu8
const buffer_readi16 = buffer.readi16
const buffer_writebits = buffer.writebits
const buffer_copy = buffer.copy
const buffer_readbits = buffer.readbits
type MyData = {
  read id: number,
  read position: Vector3,
}
const function MyData_serialize_binary(value: MyData): Loom.Serialized
  const b = buffer_create(7)
  buffer_writeu8(b, 0, value.id)
  const position_value = value.position
  buffer_writei16(b, 1, position_value.X)
  buffer_writei16(b, 3, position_value.Y)
  buffer_writei16(b, 5, position_value.Z)
  return { buffer = b }
end
const function MyData_deserialize_binary(serialized: Loom.Serialized): Loom.Result<MyData, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 7 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  return { ok = true, value = { id = buffer_readu8(b, 0), position = Vector3.new(buffer_readi16(b, 1), buffer_readi16(b, 3), buffer_readi16(b, 5)) } }
end
const function MyData_diff_binary_write(baseline: MyData, value: MyData, b: buffer)
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
    const position_value = value.position
    buffer_writei16(b, offset, position_value.X)
    offset += 2
    buffer_writei16(b, offset, position_value.Y)
    offset += 2
    buffer_writei16(b, offset, position_value.Z)
    offset += 2
  end
  return offset
end
const function MyData_diff_binary_attempt(baseline: MyData, value: MyData, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(MyData_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return MyData_diff_binary_attempt(baseline, value, size * 2)
end
const function MyData_diff_binary(baseline: MyData, value: MyData): Loom.Serialized
  const baseline_s = MyData_serialize_binary(baseline)
  const value_s = MyData_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written = MyData_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function MyData_apply_diff_binary_read(baseline: MyData, diff: Loom.Serialized): Loom.Result<MyData, Loom.DeserializeError>
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
    const read_1 = buffer_readi16(b, offset)
    offset += 2
    const read_2 = buffer_readi16(b, offset)
    offset += 2
    const read_3 = buffer_readi16(b, offset)
    offset += 2
    position = Vector3.new(read_1, read_2, read_3)
  else
    position = baseline.position
  end
  return { ok = true, value = { id = id, position = position } }
end
const function MyData_apply_diff_binary(baseline: MyData, diff: Loom.Serialized): Loom.Result<MyData, Loom.DeserializeError>
  const ok, result = pcall(MyData_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const MyData_serializer: Loom.Serializer<MyData> = { serialize = MyData_serialize_binary, deserialize = MyData_deserialize_binary, diff = MyData_diff_binary, applyDiff = MyData_apply_diff_binary }