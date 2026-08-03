const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writeu8 = buffer.writeu8
const buffer_len = buffer.len
const buffer_readu8 = buffer.readu8
const MessageData_serializer_map = { [0] = ShootGunPacket_serializer }
type Message = number
type ShootGunPacket = {
  read velocity: number,
}
const function ShootGunPacket_serialize_binary(value: ShootGunPacket): Loom.Serialized
  const b = buffer_create(1)
  buffer_writeu8(b, 0, value.velocity)
  return { buffer = b }
end
const function ShootGunPacket_deserialize_binary(serialized: Loom.Serialized): Loom.Result<ShootGunPacket, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  return { ok = true, value = { velocity = buffer_readu8(b, 0) } }
end
const ShootGunPacket_serializer: Loom.Serializer<ShootGunPacket> = { serialize = ShootGunPacket_serialize_binary, deserialize = ShootGunPacket_deserialize_binary }
type MessageData = { read [index<Message, "ShootGun">]: ShootGunPacket }
const function create_message_serializer<K>(message: K & Message): Loom.Serializer<index<MessageData, K & Message>>
  return MessageData_serializer_map[message]
end