const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writeu8 = buffer.writeu8
const buffer_len = buffer.len
const buffer_readu8 = buffer.readu8
const buffer_writebits = buffer.writebits
const buffer_readbits = buffer.readbits
type IAction<Kind> = {
  read kind: Kind & string,
}
type LogOutAction = IAction<"LogOut"> & {}
const function LogOutAction_serialize_binary(value: LogOutAction): Loom.Serialized
  return {}
end
const function LogOutAction_deserialize_binary(serialized: Loom.Serialized): Loom.Result<LogOutAction, Loom.DeserializeError>
  return { ok = true, value = { kind = "LogOut" } }
end
const LogOutAction_serializer: Loom.Serializer<LogOutAction> = { serialize = LogOutAction_serialize_binary, deserialize = LogOutAction_deserialize_binary }
type ClickAction = IAction<"Click"> & {
  read x: number,
}
const function ClickAction_serialize_binary(value: ClickAction): Loom.Serialized
  const b = buffer_create(1)
  buffer_writeu8(b, 0, value.x)
  return { buffer = b }
end
const function ClickAction_deserialize_binary(serialized: Loom.Serialized): Loom.Result<ClickAction, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  return { ok = true, value = { x = buffer_readu8(b, 0), kind = "Click" } }
end
const ClickAction_serializer: Loom.Serializer<ClickAction> = { serialize = ClickAction_serialize_binary, deserialize = ClickAction_deserialize_binary }
type Envelope = {
  read action: LogOutAction | ClickAction,
  read color: "red" | "green" | "blue",
}
const function Envelope_serialize_binary(value: Envelope): Loom.Serialized
  const action_value = value.action
  local action_tag = 0
  if action_value.kind == "Click" then
    action_tag = 1
  end
  const color_value = value.color
  local color_tag = 0
  if color_value == "green" then
    color_tag = 1
  elseif color_value == "blue" then
    color_tag = 2
  end
  local size = 1
  if action_tag == 1 then
    size += 1
  end
  const b = buffer_create(size)
  buffer_writebits(b, 0, 1, action_tag)
  local offset = 1
  if action_tag == 1 then
    buffer_writeu8(b, offset, value.action.x)
    offset += 1
  end
  buffer_writebits(b, 1, 2, color_tag)
  return { buffer = b }
end
const function Envelope_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Envelope, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const action_tag = buffer_readbits(b, 0, 1)
  local action = nil
  local offset = 1
  if action_tag == 0 then
    action = { kind = "LogOut" }
  elseif action_tag == 1 then
    if buffer_len(b) < offset + 1 then
      return { ok = false, error = { kind = "truncated", field = "action" } }
    end
    const read_0 = buffer_readu8(b, offset)
    offset += 1
    action = { kind = "Click", x = read_0 }
  else
    return { ok = false, error = { kind = "invalid_tag", field = "action" } }
  end
  const color_tag = buffer_readbits(b, 1, 2)
  local color = nil
  if color_tag == 0 then
    color = "red"
  elseif color_tag == 1 then
    color = "green"
  elseif color_tag == 2 then
    color = "blue"
  else
    return { ok = false, error = { kind = "invalid_tag", field = "color" } }
  end
  return { ok = true, value = { action = action, color = color } }
end
const Envelope_serializer: Loom.Serializer<Envelope> = { serialize = Envelope_serialize_binary, deserialize = Envelope_deserialize_binary }