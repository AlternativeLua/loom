const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_len = buffer.len
const buffer_writeu8 = buffer.writeu8
const buffer_readu8 = buffer.readu8
const buffer_writebits = buffer.writebits
const buffer_copy = buffer.copy
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
const function LogOutAction_diff_binary_write(baseline: LogOutAction, value: LogOutAction, b: buffer)
  local offset = 0
  return offset
end
const function LogOutAction_diff_binary_attempt(baseline: LogOutAction, value: LogOutAction, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(LogOutAction_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return LogOutAction_diff_binary_attempt(baseline, value, size * 2)
end
const function LogOutAction_diff_binary(baseline: LogOutAction, value: LogOutAction): Loom.Serialized
  return {}
end
const function LogOutAction_apply_diff_binary_read(baseline: LogOutAction, diff: Loom.Serialized): Loom.Result<LogOutAction, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 0 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 0
  return { ok = true, value = { kind = "LogOut" } }
end
const function LogOutAction_apply_diff_binary(baseline: LogOutAction, diff: Loom.Serialized): Loom.Result<LogOutAction, Loom.DeserializeError>
  return { ok = true, value = { kind = "LogOut" } }
end
const LogOutAction_serializer: Loom.Serializer<LogOutAction> = { serialize = LogOutAction_serialize_binary, deserialize = LogOutAction_deserialize_binary, diff = LogOutAction_diff_binary, applyDiff = LogOutAction_apply_diff_binary }
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
const function ClickAction_diff_binary_write(baseline: ClickAction, value: ClickAction, b: buffer)
  local offset = 1
  const x_changed = not Loom.deep_equal(baseline.x, value.x)
  buffer_writebits(b, 0, 1, if x_changed then 1 else 0)
  if x_changed then
    buffer_writeu8(b, offset, value.x)
    offset += 1
  end
  return offset
end
const function ClickAction_diff_binary_attempt(baseline: ClickAction, value: ClickAction, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(ClickAction_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return ClickAction_diff_binary_attempt(baseline, value, size * 2)
end
const function ClickAction_diff_binary(baseline: ClickAction, value: ClickAction): Loom.Serialized
  const baseline_s = ClickAction_serialize_binary(baseline)
  const value_s = ClickAction_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written = ClickAction_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function ClickAction_apply_diff_binary_read(baseline: ClickAction, diff: Loom.Serialized): Loom.Result<ClickAction, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 1
  const x_changed = buffer_readbits(b, 0, 1) == 1
  local x = nil
  if x_changed then
    const read_0 = buffer_readu8(b, offset)
    offset += 1
    x = read_0
  else
    x = baseline.x
  end
  return { ok = true, value = { x = x, kind = "Click" } }
end
const function ClickAction_apply_diff_binary(baseline: ClickAction, diff: Loom.Serialized): Loom.Result<ClickAction, Loom.DeserializeError>
  const ok, result = pcall(ClickAction_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const ClickAction_serializer: Loom.Serializer<ClickAction> = { serialize = ClickAction_serialize_binary, deserialize = ClickAction_deserialize_binary, diff = ClickAction_diff_binary, applyDiff = ClickAction_apply_diff_binary }
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
const function Envelope_diff_binary_write(baseline: Envelope, value: Envelope, b: buffer)
  local offset = 1
  const action_tag_b_value = baseline.action
  local action_tag_b = 0
  if action_tag_b_value.kind == "Click" then
    action_tag_b = 1
  end
  const action_tag_c_value = value.action
  local action_tag_c = 0
  if action_tag_c_value.kind == "Click" then
    action_tag_c = 1
  end
  const action_tag_changed = action_tag_b ~= action_tag_c
  buffer_writebits(b, 0, 1, if action_tag_changed then 1 else 0)
  if action_tag_changed then
    const action_value = value.action
    local action_tag = 0
    if action_value.kind == "Click" then
      action_tag = 1
    end
    buffer_writebits(b, 1, 1, action_tag)
    if action_tag == 1 then
      buffer_writeu8(b, offset, value.action.x)
      offset += 1
    end
  else
    if action_tag_c == 1 then
      const x_changed = not Loom.deep_equal(baseline.action.x, value.action.x)
      buffer_writebits(b, 1, 1, if x_changed then 1 else 0)
      if x_changed then
        buffer_writeu8(b, offset, value.action.x)
        offset += 1
      end
    end
  end
  const color_tag_b_value = baseline.color
  local color_tag_b = 0
  if color_tag_b_value == "green" then
    color_tag_b = 1
  elseif color_tag_b_value == "blue" then
    color_tag_b = 2
  end
  const color_tag_c_value = value.color
  local color_tag_c = 0
  if color_tag_c_value == "green" then
    color_tag_c = 1
  elseif color_tag_c_value == "blue" then
    color_tag_c = 2
  end
  const color_tag_changed = color_tag_b ~= color_tag_c
  buffer_writebits(b, 2, 1, if color_tag_changed then 1 else 0)
  if color_tag_changed then
    const color_value = value.color
    local color_tag = 0
    if color_value == "green" then
      color_tag = 1
    elseif color_value == "blue" then
      color_tag = 2
    end
    buffer_writebits(b, 3, 2, color_tag)
  else
  end
  return offset
end
const function Envelope_diff_binary_attempt(baseline: Envelope, value: Envelope, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Envelope_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Envelope_diff_binary_attempt(baseline, value, size * 2)
end
const function Envelope_diff_binary(baseline: Envelope, value: Envelope): Loom.Serialized
  const baseline_s = Envelope_serialize_binary(baseline)
  const value_s = Envelope_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written = Envelope_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Envelope_apply_diff_binary_read(baseline: Envelope, diff: Loom.Serialized): Loom.Result<Envelope, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 1
  const action_tag_changed = buffer_readbits(b, 0, 1) == 1
  local action = nil
  if action_tag_changed then
    const action_2_tag = buffer_readbits(b, 1, 1)
    local action_2 = nil
    if action_2_tag == 0 then
      action_2 = { kind = "LogOut" }
    elseif action_2_tag == 1 then
      if buffer_len(b) < offset + 1 then
        return { ok = false, error = { kind = "truncated", field = "action" } }
      end
      const read_0 = buffer_readu8(b, offset)
      offset += 1
      action_2 = { kind = "Click", x = read_0 }
    else
      return { ok = false, error = { kind = "invalid_tag", field = "action" } }
    end
    action = action_2
  else
    const action_tag_b_value = baseline.action
    local action_tag_b = 0
    if action_tag_b_value.kind == "Click" then
      action_tag_b = 1
    end
    if action_tag_b == 0 then
      action = { kind = "LogOut" }
    elseif action_tag_b == 1 then
      const x_changed = buffer_readbits(b, 2, 1) == 1
      local x = nil
      if x_changed then
        const read_1 = buffer_readu8(b, offset)
        offset += 1
        x = read_1
      else
        x = baseline.action.x
      end
      action = { kind = "Click", x = x }
    end
  end
  const color_tag_changed = buffer_readbits(b, 3, 1) == 1
  local color = nil
  if color_tag_changed then
    const color_2_tag = buffer_readbits(b, 4, 2)
    local color_2 = nil
    if color_2_tag == 0 then
      color_2 = "red"
    elseif color_2_tag == 1 then
      color_2 = "green"
    elseif color_2_tag == 2 then
      color_2 = "blue"
    else
      return { ok = false, error = { kind = "invalid_tag", field = "color" } }
    end
    color = color_2
  else
    const color_tag_b_value = baseline.color
    local color_tag_b = 0
    if color_tag_b_value == "green" then
      color_tag_b = 1
    elseif color_tag_b_value == "blue" then
      color_tag_b = 2
    end
    if color_tag_b == 0 then
      color = "red"
    elseif color_tag_b == 1 then
      color = "green"
    elseif color_tag_b == 2 then
      color = "blue"
    end
  end
  return { ok = true, value = { action = action, color = color } }
end
const function Envelope_apply_diff_binary(baseline: Envelope, diff: Loom.Serialized): Loom.Result<Envelope, Loom.DeserializeError>
  const ok, result = pcall(Envelope_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Envelope_serializer: Loom.Serializer<Envelope> = { serialize = Envelope_serialize_binary, deserialize = Envelope_deserialize_binary, diff = Envelope_diff_binary, applyDiff = Envelope_apply_diff_binary }