const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_len = buffer.len
const buffer_writeu32 = buffer.writeu32
const buffer_writestring = buffer.writestring
const buffer_writebits = buffer.writebits
const buffer_readu32 = buffer.readu32
const buffer_readstring = buffer.readstring
const buffer_readbits = buffer.readbits
const buffer_copy = buffer.copy
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
const function Ping_diff_binary_write(baseline: Ping, value: Ping, b: buffer)
  local offset = 0
  return offset
end
const function Ping_diff_binary_attempt(baseline: Ping, value: Ping, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Ping_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Ping_diff_binary_attempt(baseline, value, size * 2)
end
const function Ping_diff_binary(baseline: Ping, value: Ping): Loom.Serialized
  return {}
end
const function Ping_apply_diff_binary_read(baseline: Ping, diff: Loom.Serialized): Loom.Result<Ping, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 0 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 0
  return { ok = true, value = { kind = "Ping" } }
end
const function Ping_apply_diff_binary(baseline: Ping, diff: Loom.Serialized): Loom.Result<Ping, Loom.DeserializeError>
  return { ok = true, value = { kind = "Ping" } }
end
const Ping_serializer: Loom.Serializer<Ping> = { serialize = Ping_serialize_binary, deserialize = Ping_deserialize_binary, diff = Ping_diff_binary, applyDiff = Ping_apply_diff_binary }
type Chat = IEvent<"Chat"> & {
  read message: string,
  read volume: number,
}
const function Chat_serialize_binary(value: Chat): Loom.Serialized
  const b = buffer_create(1 + 4 + #value.message)
  const message_value = value.message
  const message_length = #message_value
  buffer_writeu32(b, 1, message_length)
  local offset = 5
  buffer_writestring(b, offset, message_value)
  offset += message_length
  buffer_writebits(b, 0, 7, math.round(value.volume))
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
  return { ok = true, value = { message = message, volume = buffer_readbits(b, 0, 7), kind = "Chat" } }
end
const function Chat_diff_binary_write(baseline: Chat, value: Chat, b: buffer)
  local offset = 2
  const message_changed = not Loom.deep_equal(baseline.message, value.message)
  buffer_writebits(b, 0, 1, if message_changed then 1 else 0)
  if message_changed then
    const message_value = value.message
    const message_length = #message_value
    buffer_writeu32(b, offset, message_length)
    offset += 4
    buffer_writestring(b, offset, message_value)
    offset += message_length
  end
  const volume_changed = not Loom.deep_equal(baseline.volume, value.volume)
  buffer_writebits(b, 1, 1, if volume_changed then 1 else 0)
  if volume_changed then
    buffer_writebits(b, 2, 7, math.round(value.volume))
  end
  return offset
end
const function Chat_diff_binary_attempt(baseline: Chat, value: Chat, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Chat_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Chat_diff_binary_attempt(baseline, value, size * 2)
end
const function Chat_diff_binary(baseline: Chat, value: Chat): Loom.Serialized
  const baseline_s = Chat_serialize_binary(baseline)
  const value_s = Chat_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 2
  const scratch, written = Chat_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Chat_apply_diff_binary_read(baseline: Chat, diff: Loom.Serialized): Loom.Result<Chat, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 2 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 2
  const message_changed = buffer_readbits(b, 0, 1) == 1
  local message = nil
  if message_changed then
    const read_0 = buffer_readu32(b, offset)
    offset += 4
    const message_2_length = read_0
    if buffer_len(b) < offset + message_2_length then
      return { ok = false, error = { kind = "invalid_length", field = "message" } }
    end
    const message_2 = buffer_readstring(b, offset, message_2_length)
    offset += message_2_length
    message = message_2
  else
    message = baseline.message
  end
  const volume_changed = buffer_readbits(b, 1, 1) == 1
  local volume = nil
  if volume_changed then
    volume = buffer_readbits(b, 2, 7)
  else
    volume = baseline.volume
  end
  return { ok = true, value = { message = message, volume = volume, kind = "Chat" } }
end
const function Chat_apply_diff_binary(baseline: Chat, diff: Loom.Serialized): Loom.Result<Chat, Loom.DeserializeError>
  const ok, result = pcall(Chat_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Chat_serializer: Loom.Serializer<Chat> = { serialize = Chat_serialize_binary, deserialize = Chat_deserialize_binary, diff = Chat_diff_binary, applyDiff = Chat_apply_diff_binary }
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
const function Inner_diff_binary_write(baseline: Inner, value: Inner, b: buffer)
  local offset = 1
  const flag_changed = not Loom.deep_equal(baseline.flag, value.flag)
  buffer_writebits(b, 0, 1, if flag_changed then 1 else 0)
  if flag_changed then
    buffer_writebits(b, 1, 1, if value.flag then 1 else 0)
  end
  const code_changed = not Loom.deep_equal(baseline.code, value.code)
  buffer_writebits(b, 2, 1, if code_changed then 1 else 0)
  if code_changed then
    buffer_writeu8(b, offset, value.code)
    offset += 1
  end
  return offset
end
const function Inner_diff_binary_attempt(baseline: Inner, value: Inner, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Inner_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Inner_diff_binary_attempt(baseline, value, size * 2)
end
const function Inner_diff_binary(baseline: Inner, value: Inner): Loom.Serialized
  const baseline_s = Inner_serialize_binary(baseline)
  const value_s = Inner_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written = Inner_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Inner_apply_diff_binary_read(baseline: Inner, diff: Loom.Serialized): Loom.Result<Inner, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 1
  const flag_changed = buffer_readbits(b, 0, 1) == 1
  local flag = nil
  if flag_changed then
    flag = buffer_readbits(b, 1, 1) == 1
  else
    flag = baseline.flag
  end
  const code_changed = buffer_readbits(b, 2, 1) == 1
  local code = nil
  if code_changed then
    const read_0 = buffer_readu8(b, offset)
    offset += 1
    code = read_0
  else
    code = baseline.code
  end
  return { ok = true, value = { flag = flag, code = code } }
end
const function Inner_apply_diff_binary(baseline: Inner, diff: Loom.Serialized): Loom.Result<Inner, Loom.DeserializeError>
  const ok, result = pcall(Inner_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Inner_serializer: Loom.Serializer<Inner> = { serialize = Inner_serialize_binary, deserialize = Inner_deserialize_binary, diff = Inner_diff_binary, applyDiff = Inner_apply_diff_binary }
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
    const label_value = value.label
    const label_length = #label_value
    buffer_writeu32(b, offset, label_length)
    offset += 4
    buffer_writestring(b, offset, label_value)
    offset += label_length
  end
  buffer_writeu32(b, offset, #value.points)
  offset += 4
  for i = 1, #value.points do
    const points_item = value.points[i]
    buffer_writei16(b, offset, points_item.X)
    offset += 2
    buffer_writei16(b, offset, points_item.Y)
    offset += 2
    buffer_writei16(b, offset, points_item.Z)
    offset += 2
  end
  buffer_writebits(b, 2, 1, payload_tag)
  if payload_tag == 1 then
    const message_value = value.payload.message
    const message_length = #message_value
    buffer_writeu32(b, offset, message_length)
    offset += 4
    buffer_writestring(b, offset, message_value)
    offset += message_length
    buffer_writebits(b, 3, 7, math.round(value.payload.volume))
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
    payload = { kind = "Chat", message = message, volume = buffer_readbits(b, 3, 7) }
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
    inner = { flag = buffer_readbits(b, 0, 1) == 1, code = buffer_readu8(b, 2) },
    label = label,
    points = points,
    payload = payload,
    owner = owner_blob,
    frame = CFrame.new(read_6, read_7, read_8, read_9, read_10, read_11, read_12),
  } }
end
const function KitchenSink_diff_binary_write(baseline: KitchenSink, value: KitchenSink, b: buffer)
  const blobs = {}
  local offset = 3
  const flag_changed = not Loom.deep_equal(baseline.inner.flag, value.inner.flag)
  buffer_writebits(b, 0, 1, if flag_changed then 1 else 0)
  if flag_changed then
    buffer_writebits(b, 1, 1, if value.inner.flag then 1 else 0)
  end
  const code_changed = not Loom.deep_equal(baseline.inner.code, value.inner.code)
  buffer_writebits(b, 2, 1, if code_changed then 1 else 0)
  if code_changed then
    buffer_writeu8(b, offset, value.inner.code)
    offset += 1
  end
  const label_presence_changed = (baseline.label ~= nil) ~= (value.label ~= nil)
  buffer_writebits(b, 3, 1, if label_presence_changed then 1 else 0)
  if label_presence_changed then
    buffer_writebits(b, 4, 1, if value.label ~= nil then 1 else 0)
    if value.label ~= nil then
      const label_value = value.label
      const label_length = #label_value
      buffer_writeu32(b, offset, label_length)
      offset += 4
      buffer_writestring(b, offset, label_value)
      offset += label_length
    end
  else
    if value.label ~= nil then
      const label_changed = not Loom.deep_equal(baseline.label, value.label)
      buffer_writebits(b, 4, 1, if label_changed then 1 else 0)
      if label_changed then
        const label_value_2 = value.label
        const label_length_2 = #label_value_2
        buffer_writeu32(b, offset, label_length_2)
        offset += 4
        buffer_writestring(b, offset, label_value_2)
        offset += label_length_2
      end
    end
  end
  const points_length_changed = #baseline.points ~= #value.points
  buffer_writebits(b, 5, 1, if points_length_changed then 1 else 0)
  if points_length_changed then
    buffer_writeu32(b, offset, #value.points)
    offset += 4
    for i = 1, #value.points do
      const points_item = value.points[i]
      buffer_writei16(b, offset, points_item.X)
      offset += 2
      buffer_writei16(b, offset, points_item.Y)
      offset += 2
      buffer_writei16(b, offset, points_item.Z)
      offset += 2
    end
  else
    const points_bits = offset * 8
    offset += (#value.points + 7) // 8
    for i_2 = 1, #value.points do
      const points_element_changed = not Loom.deep_equal(baseline.points[i_2], value.points[i_2])
      buffer_writebits(b, points_bits + (i_2 - 1), 1, if points_element_changed then 1 else 0)
      if points_element_changed then
        const points_element_value = value.points[i_2]
        buffer_writei16(b, offset, points_element_value.X)
        offset += 2
        buffer_writei16(b, offset, points_element_value.Y)
        offset += 2
        buffer_writei16(b, offset, points_element_value.Z)
        offset += 2
      end
    end
  end
  const payload_tag_b_value = baseline.payload
  local payload_tag_b = 0
  if payload_tag_b_value.kind == "Chat" then
    payload_tag_b = 1
  end
  const payload_tag_c_value = value.payload
  local payload_tag_c = 0
  if payload_tag_c_value.kind == "Chat" then
    payload_tag_c = 1
  end
  const payload_tag_changed = payload_tag_b ~= payload_tag_c
  buffer_writebits(b, 6, 1, if payload_tag_changed then 1 else 0)
  if payload_tag_changed then
    const payload_value = value.payload
    local payload_tag = 0
    if payload_value.kind == "Chat" then
      payload_tag = 1
    end
    buffer_writebits(b, 7, 1, payload_tag)
    if payload_tag == 1 then
      const message_value = value.payload.message
      const message_length = #message_value
      buffer_writeu32(b, offset, message_length)
      offset += 4
      buffer_writestring(b, offset, message_value)
      offset += message_length
      buffer_writebits(b, 8, 7, math.round(value.payload.volume))
    end
  else
    if payload_tag_c == 1 then
      const message_changed = not Loom.deep_equal(baseline.payload.message, value.payload.message)
      buffer_writebits(b, 7, 1, if message_changed then 1 else 0)
      if message_changed then
        const message_value_2 = value.payload.message
        const message_length_2 = #message_value_2
        buffer_writeu32(b, offset, message_length_2)
        offset += 4
        buffer_writestring(b, offset, message_value_2)
        offset += message_length_2
      end
      const volume_changed = not Loom.deep_equal(baseline.payload.volume, value.payload.volume)
      buffer_writebits(b, 8, 1, if volume_changed then 1 else 0)
      if volume_changed then
        buffer_writebits(b, 9, 7, math.round(value.payload.volume))
      end
    end
  end
  const owner_changed = not Loom.deep_equal(baseline.owner, value.owner)
  buffer_writebits(b, 16, 1, if owner_changed then 1 else 0)
  if owner_changed then
    table.insert(blobs, value.owner)
  end
  const frame_changed = not Loom.deep_equal(baseline.frame, value.frame)
  buffer_writebits(b, 17, 1, if frame_changed then 1 else 0)
  if frame_changed then
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
  end
  return offset, blobs
end
const function KitchenSink_diff_binary_attempt(baseline: KitchenSink, value: KitchenSink, size: number)
  const scratch = buffer_create(size)
  const ok, written, blobs = pcall(KitchenSink_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written, blobs
  end
  return KitchenSink_diff_binary_attempt(baseline, value, size * 2)
end
const function KitchenSink_diff_binary(baseline: KitchenSink, value: KitchenSink): Loom.Serialized
  const baseline_s = KitchenSink_serialize_binary(baseline)
  const value_s = KitchenSink_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 3
  const scratch, written, blobs = KitchenSink_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result, blobs = blobs }
end
const function KitchenSink_apply_diff_binary_read(baseline: KitchenSink, diff: Loom.Serialized): Loom.Result<KitchenSink, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 3 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const blobs = diff.blobs
  if blobs == nil then
    return { ok = false, error = { kind = "missing_blob" } }
  end
  local blob_index = 1
  local offset = 3
  const flag_changed = buffer_readbits(b, 0, 1) == 1
  local flag = nil
  if flag_changed then
    flag = buffer_readbits(b, 1, 1) == 1
  else
    flag = baseline.inner.flag
  end
  const code_changed = buffer_readbits(b, 2, 1) == 1
  local code = nil
  if code_changed then
    const read_0 = buffer_readu8(b, offset)
    offset += 1
    code = read_0
  else
    code = baseline.inner.code
  end
  const label_presence_changed = buffer_readbits(b, 3, 1) == 1
  local label = nil
  if label_presence_changed then
    const label_2_present = buffer_readbits(b, 4, 1) == 1
    local label_2 = nil
    if label_2_present then
      const read_1 = buffer_readu32(b, offset)
      offset += 4
      const label_3_length = read_1
      if buffer_len(b) < offset + label_3_length then
        return { ok = false, error = { kind = "invalid_length", field = "label" } }
      end
      const label_3 = buffer_readstring(b, offset, label_3_length)
      offset += label_3_length
      label_2 = label_3
    end
    label = label_2
  else
    if baseline.label ~= nil then
      const label_changed = buffer_readbits(b, 5, 1) == 1
      local label_4 = nil
      if label_changed then
        const read_2 = buffer_readu32(b, offset)
        offset += 4
        const label_5_length = read_2
        if buffer_len(b) < offset + label_5_length then
          return { ok = false, error = { kind = "invalid_length", field = "label" } }
        end
        const label_5 = buffer_readstring(b, offset, label_5_length)
        offset += label_5_length
        label_4 = label_5
      else
        label_4 = baseline.label
      end
      label = label_4
    end
  end
  const points_length_changed = buffer_readbits(b, 6, 1) == 1
  local points = nil
  if points_length_changed then
    const read_3 = buffer_readu32(b, offset)
    offset += 4
    const points_2_count = read_3
    if buffer_len(b) < offset + points_2_count * 6 then
      return { ok = false, error = { kind = "invalid_length", field = "points" } }
    end
    const points_2 = {}
    for i = 1, points_2_count do
      const read_4 = buffer_readi16(b, offset)
      offset += 2
      const read_5 = buffer_readi16(b, offset)
      offset += 2
      const read_6 = buffer_readi16(b, offset)
      offset += 2
      points_2[i] = Vector3.new(read_4, read_5, read_6)
    end
    points = points_2
  else
    const points_count = #baseline.points
    const points_bits = offset * 8
    offset += (points_count + 7) // 8
    points = {}
    for i_2 = 1, points_count do
      const points_element_changed = buffer_readbits(b, points_bits + (i_2 - 1), 1) == 1
      local points_element = nil
      if points_element_changed then
        const read_7 = buffer_readi16(b, offset)
        offset += 2
        const read_8 = buffer_readi16(b, offset)
        offset += 2
        const read_9 = buffer_readi16(b, offset)
        offset += 2
        points_element = Vector3.new(read_7, read_8, read_9)
      else
        points_element = baseline.points[i_2]
      end
      points[i_2] = points_element
    end
  end
  const payload_tag_changed = buffer_readbits(b, 7, 1) == 1
  local payload = nil
  if payload_tag_changed then
    const payload_2_tag = buffer_readbits(b, 8, 1)
    local payload_2 = nil
    if payload_2_tag == 0 then
      payload_2 = { kind = "Ping" }
    elseif payload_2_tag == 1 then
      const read_10 = buffer_readu32(b, offset)
      offset += 4
      const message_length = read_10
      if buffer_len(b) < offset + message_length then
        return { ok = false, error = { kind = "invalid_length", field = "payload.message" } }
      end
      const message = buffer_readstring(b, offset, message_length)
      offset += message_length
      payload_2 = { kind = "Chat", message = message, volume = buffer_readbits(b, 9, 7) }
    else
      return { ok = false, error = { kind = "invalid_tag", field = "payload" } }
    end
    payload = payload_2
  else
    const payload_tag_b_value = baseline.payload
    local payload_tag_b = 0
    if payload_tag_b_value.kind == "Chat" then
      payload_tag_b = 1
    end
    if payload_tag_b == 0 then
      payload = { kind = "Ping" }
    elseif payload_tag_b == 1 then
      const message_changed = buffer_readbits(b, 16, 1) == 1
      local message_2 = nil
      if message_changed then
        const read_11 = buffer_readu32(b, offset)
        offset += 4
        const message_3_length = read_11
        if buffer_len(b) < offset + message_3_length then
          return { ok = false, error = { kind = "invalid_length", field = "payload.message" } }
        end
        const message_3 = buffer_readstring(b, offset, message_3_length)
        offset += message_3_length
        message_2 = message_3
      else
        message_2 = baseline.payload.message
      end
      const volume_changed = buffer_readbits(b, 17, 1) == 1
      local volume = nil
      if volume_changed then
        volume = buffer_readbits(b, 18, 7)
      else
        volume = baseline.payload.volume
      end
      payload = { kind = "Chat", message = message_2, volume = volume }
    end
  end
  const owner_changed = buffer_readbits(b, 25, 1) == 1
  local owner = nil
  if owner_changed then
    const owner_blob = blobs[blob_index]
    blob_index += 1
    if owner_blob == nil then
      return { ok = false, error = { kind = "missing_blob", field = "owner" } }
    end
    if typeof(owner_blob) ~= "Instance" or not owner_blob:IsA("Part") then
      return { ok = false, error = { kind = "invalid_blob", field = "owner" } }
    end
    owner = owner_blob
  else
    owner = baseline.owner
  end
  const frame_changed = buffer_readbits(b, 26, 1) == 1
  local frame = nil
  if frame_changed then
    const read_12 = buffer_readf32(b, offset)
    offset += 4
    const read_13 = buffer_readf32(b, offset)
    offset += 4
    const read_14 = buffer_readf32(b, offset)
    offset += 4
    const read_15 = buffer_readf32(b, offset)
    offset += 4
    const read_16 = buffer_readf32(b, offset)
    offset += 4
    const read_17 = buffer_readf32(b, offset)
    offset += 4
    const read_18 = buffer_readf32(b, offset)
    offset += 4
    frame = CFrame.new(read_12, read_13, read_14, read_15, read_16, read_17, read_18)
  else
    frame = baseline.frame
  end
  return { ok = true, value = {
    tag = "sink",
    inner = { flag = flag, code = code },
    label = label,
    points = points,
    payload = payload,
    owner = owner,
    frame = frame,
  } }
end
const function KitchenSink_apply_diff_binary(baseline: KitchenSink, diff: Loom.Serialized): Loom.Result<KitchenSink, Loom.DeserializeError>
  const ok, result = pcall(KitchenSink_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const KitchenSink_serializer: Loom.Serializer<KitchenSink> = { serialize = KitchenSink_serialize_binary, deserialize = KitchenSink_deserialize_binary, diff = KitchenSink_diff_binary, applyDiff = KitchenSink_apply_diff_binary }