const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writeu8 = buffer.writeu8
const buffer_writestring = buffer.writestring
const buffer_writebits = buffer.writebits
type User = {
  read id: number,
  read name: string,
  read is_admin: boolean,
}
const function User_serialize_binary(value: User): Loom.Serialized
  const b = buffer_create(2 + 1 + #value.name)
  buffer_writeu8(b, 1, value.id)
  const name_value = value.name
  const name_length = #name_value
  buffer_writeu8(b, 2, name_length)
  local offset = 3
  buffer_writestring(b, offset, name_value)
  offset += name_length
  buffer_writebits(b, 0, 1, if value.is_admin then 1 else 0)
  return { buffer = b }
end
const user = { id = 69, name = "Shan", is_admin = true }
const serialized = User_serialize_binary(user)
