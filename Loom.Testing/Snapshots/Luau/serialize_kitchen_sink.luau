const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writeu32 = buffer.writeu32
const buffer_writestring = buffer.writestring
const buffer_writebits = buffer.writebits
const buffer_len = buffer.len
const buffer_readu32 = buffer.readu32
const buffer_readstring = buffer.readstring
const buffer_readbits = buffer.readbits
const buffer_writeu8 = buffer.writeu8
const buffer_readu8 = buffer.readu8
const buffer_writei16 = buffer.writei16
const buffer_writef32 = buffer.writef32
const buffer_readi16 = buffer.readi16
const buffer_readf32 = buffer.readf32
type IEvent<Kind> = {
  read kind: Kind & string,
}
type Ping = IEvent<"Ping"> & {}
const function Ping_serialize_binary(value: Ping): Loom.Serialized
  return {}
end
const function Ping_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Ping, Loom.DeserializeError>
  return { ok = true, value = { kind = "Ping" } }
end
const Ping_serializer: Loom.Serializer<Ping> = { serialize = Ping_serialize_binary, deserialize = Ping_deserialize_binary }
type Chat = IEvent<"Chat"> & {
  read message: string,
  read volume: number,
}
const function Chat_serialize_binary(value: Chat): Loom.Serialized
  const b = buffer_create(1 + 4 + #value.message)
  buffer_writeu32(b, 1, #value.message)
  local offset = 5
  buffer_writestring(b, offset, value.message)
  offset += #value.message
  buffer_writebits(b, 0, 7, math.round((value.volume - 0) / 1))
  return { buffer = b }
end
const function Chat_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Chat, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 5 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const message_length = buffer_readu32(b, 1)
  local offset = 5
  if buffer_len(b) < offset + message_length then
    return { ok = false, error = { kind = "invalid_length", field = "message" } }
  end
  const message = buffer_readstring(b, offset, message_length)
  offset += message_length
  return { ok = true, value = { message = message, volume = 0 + buffer_readbits(b, 0, 7) * 1, kind = "Chat" } }
end
const Chat_serializer: Loom.Serializer<Chat> = { serialize = Chat_serialize_binary, deserialize = Chat_deserialize_binary }
type Inner = {
  read flag: boolean,
  read code: number,
}
const function Inner_serialize_binary(value: Inner): Loom.Serialized
  const b = buffer_create(2)
  buffer_writebits(b, 0, 1, if value.flag then 1 else 0)
  buffer_writeu8(b, 1, value.code)
  return { buffer = b }
end
const function Inner_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Inner, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 2 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  return { ok = true, value = { flag = buffer_readbits(b, 0, 1) == 1, code = buffer_readu8(b, 1) } }
end
const Inner_serializer: Loom.Serializer<Inner> = { serialize = Inner_serialize_binary, deserialize = Inner_deserialize_binary }
type KitchenSink = {
  read tag: "sink",
  read inner: Inner,
  read label: string?,
  read points: { Vector3 },
  read payload: Ping | Chat,
  read owner: Part,
  read frame: CFrame,
}
const function KitchenSink_serialize_binary(value: KitchenSink): Loom.Serialized
  const blobs = {}
  const payload_value = value.payload
  local payload_tag = 0
  if payload_value.kind == "Chat" then
    payload_tag = 1
  end
  local size = 31 + 4 + #value.points * 6
  if value.label ~= nil then
    size += 4 + #value.label
  end
  if payload_tag == 1 then
    size += 4 + #value.payload.message
  end
  const b = buffer_create(size)
  buffer_writebits(b, 0, 1, if value.inner.flag then 1 else 0)
  buffer_writeu8(b, 2, value.inner.code)
  buffer_writebits(b, 1, 1, if value.label ~= nil then 1 else 0)
  local offset = 3
  if value.label ~= nil then
    buffer_writeu32(b, offset, #value.label)
    offset += 4
    buffer_writestring(b, offset, value.label)
    offset += #value.label
  end
  buffer_writeu32(b, offset, #value.points)
  offset += 4
  for i = 1, #value.points do
    buffer_writei16(b, offset, value.points[i].X)
    offset += 2
    buffer_writei16(b, offset, value.points[i].Y)
    offset += 2
    buffer_writei16(b, offset, value.points[i].Z)
    offset += 2
  end
  buffer_writebits(b, 2, 1, payload_tag)
  if payload_tag == 1 then
    buffer_writeu32(b, offset, #value.payload.message)
    offset += 4
    buffer_writestring(b, offset, value.payload.message)
    offset += #value.payload.message
    buffer_writebits(b, 3, 7, math.round((value.payload.volume - 0) / 1))
  end
  table.insert(blobs, value.owner)
  buffer_writef32(b, offset, value.frame.X)
  offset += 4
  buffer_writef32(b, offset, value.frame.Y)
  offset += 4
  buffer_writef32(b, offset, value.frame.Z)
  offset += 4
  const qx, qy, qz, qw = Loom.cframe_to_quaternion(value.frame)
  buffer_writef32(b, offset, qx)
  offset += 4
  buffer_writef32(b, offset, qy)
  offset += 4
  buffer_writef32(b, offset, qz)
  offset += 4
  buffer_writef32(b, offset, qw)
  offset += 4
  return { buffer = b, blobs = blobs }
end
const function KitchenSink_deserialize_binary(serialized: Loom.Serialized): Loom.Result<KitchenSink, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 35 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const blobs = serialized.blobs
  if blobs == nil then
    return { ok = false, error = { kind = "missing_blob" } }
  end
  local blob_index = 1
  const label_present = buffer_readbits(b, 1, 1) == 1
  local label = nil
  local offset = 3
  if label_present then
    const read_0 = buffer_readu32(b, offset)
    offset += 4
    const label_2_length = read_0
    if buffer_len(b) < offset + label_2_length then
      return { ok = false, error = { kind = "invalid_length", field = "label" } }
    end
    const label_2 = buffer_readstring(b, offset, label_2_length)
    offset += label_2_length
    label = label_2
  end
  const read_1 = buffer_readu32(b, offset)
  offset += 4
  const points_count = read_1
  if buffer_len(b) < offset + points_count * 6 then
    return { ok = false, error = { kind = "invalid_length", field = "points" } }
  end
  const points = {}
  for i = 1, points_count do
    const read_2 = buffer_readi16(b, offset)
    offset += 2
    const read_3 = buffer_readi16(b, offset)
    offset += 2
    const read_4 = buffer_readi16(b, offset)
    offset += 2
    points[i] = Vector3.new(read_2, read_3, read_4)
  end
  const payload_tag = buffer_readbits(b, 2, 1)
  local payload = nil
  if payload_tag == 0 then
    payload = { kind = "Ping" }
  elseif payload_tag == 1 then
    const read_5 = buffer_readu32(b, offset)
    offset += 4
    const message_length = read_5
    if buffer_len(b) < offset + message_length then
      return { ok = false, error = { kind = "invalid_length", field = "payload.message" } }
    end
    const message = buffer_readstring(b, offset, message_length)
    offset += message_length
    payload = { kind = "Chat", message = message, volume = 0 + buffer_readbits(b, 3, 7) * 1 }
  else
    return { ok = false, error = { kind = "invalid_tag", field = "payload" } }
  end
  const owner_blob = blobs[blob_index]
  blob_index += 1
  if owner_blob == nil then
    return { ok = false, error = { kind = "missing_blob", field = "owner" } }
  end
  if typeof(owner_blob) ~= "Instance" or not owner_blob:IsA("Part") then
    return { ok = false, error = { kind = "invalid_blob", field = "owner" } }
  end
  const read_6 = buffer_readf32(b, offset)
  offset += 4
  const read_7 = buffer_readf32(b, offset)
  offset += 4
  const read_8 = buffer_readf32(b, offset)
  offset += 4
  const read_9 = buffer_readf32(b, offset)
  offset += 4
  const read_10 = buffer_readf32(b, offset)
  offset += 4
  const read_11 = buffer_readf32(b, offset)
  offset += 4
  const read_12 = buffer_readf32(b, offset)
  offset += 4
  return { ok = true, value = {
    tag = "sink",
    label = label,
    points = points,
    payload = payload,
    owner = owner_blob,
    frame = CFrame.new(read_6, read_7, read_8, read_9, read_10, read_11, read_12),
    inner = { flag = buffer_readbits(b, 0, 1) == 1, code = buffer_readu8(b, 2) },
  } }
end
const KitchenSink_serializer: Loom.Serializer<KitchenSink> = { serialize = KitchenSink_serialize_binary, deserialize = KitchenSink_deserialize_binary }