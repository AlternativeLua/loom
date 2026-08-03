const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writeu8 = buffer.writeu8
const buffer_writei16 = buffer.writei16
const buffer_len = buffer.len
const buffer_readu8 = buffer.readu8
const buffer_readi16 = buffer.readi16
type MyData = {
  read id: number,
  read position: Vector3,
}
const function MyData_serialize_binary(value: MyData): Loom.Serialized
  const b = buffer_create(7)
  buffer_writeu8(b, 0, value.id)
  buffer_writei16(b, 1, value.position.X)
  buffer_writei16(b, 3, value.position.Y)
  buffer_writei16(b, 5, value.position.Z)
  return { buffer = b }
end
const function MyData_deserialize_binary(serialized: Loom.Serialized): Loom.Result<MyData, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 7 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  return { ok = true, value = { id = buffer_readu8(b, 0), position = Vector3.new(buffer_readi16(b, 1), buffer_readi16(b, 3), buffer_readi16(b, 5)) } }
end