const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writeu8 = buffer.writeu8
const buffer_writeu32 = buffer.writeu32
const buffer_writestring = buffer.writestring
const buffer_len = buffer.len
const buffer_readu8 = buffer.readu8
const buffer_readu32 = buffer.readu32
const buffer_readstring = buffer.readstring
const buffer_writebits = buffer.writebits
const buffer_copy = buffer.copy
const buffer_readbits = buffer.readbits
type Player = {
  read id: number,
  read name: string,
}
const function Player_serialize_binary(value: Player): Loom.Serialized
  const b = buffer_create(1 + 4 + #value.name)
  buffer_writeu8(b, 0, value.id)
  const name_value = value.name
  const name_length = #name_value
  buffer_writeu32(b, 1, name_length)
  local offset = 5
  buffer_writestring(b, offset, name_value)
  offset += name_length
  return { buffer = b }
end
const function Player_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Player, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 5 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const name_length = buffer_readu32(b, 1)
  local offset = 5
  if buffer_len(b) < offset + name_length then
    return { ok = false, error = { kind = "invalid_length", field = "name" } }
  end
  const name = buffer_readstring(b, offset, name_length)
  offset += name_length
  return { ok = true, value = { id = buffer_readu8(b, 0), name = name } }
end
const function Player_diff_binary_write(baseline: Player, value: Player, b: buffer)
  local offset = 1
  const id_changed = not Loom.deep_equal(baseline.id, value.id)
  buffer_writebits(b, 0, 1, if id_changed then 1 else 0)
  if id_changed then
    buffer_writeu8(b, offset, value.id)
    offset += 1
  end
  const name_changed = not Loom.deep_equal(baseline.name, value.name)
  buffer_writebits(b, 1, 1, if name_changed then 1 else 0)
  if name_changed then
    const name_value = value.name
    const name_length = #name_value
    buffer_writeu32(b, offset, name_length)
    offset += 4
    buffer_writestring(b, offset, name_value)
    offset += name_length
  end
  return offset
end
const function Player_diff_binary_attempt(baseline: Player, value: Player, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Player_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Player_diff_binary_attempt(baseline, value, size * 2)
end
const function Player_diff_binary(baseline: Player, value: Player): Loom.Serialized
  const baseline_s = Player_serialize_binary(baseline)
  const value_s = Player_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written = Player_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Player_apply_diff_binary_read(baseline: Player, diff: Loom.Serialized): Loom.Result<Player, Loom.DeserializeError>
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
  const name_changed = buffer_readbits(b, 1, 1) == 1
  local name = nil
  if name_changed then
    const read_1 = buffer_readu32(b, offset)
    offset += 4
    const name_2_length = read_1
    if buffer_len(b) < offset + name_2_length then
      return { ok = false, error = { kind = "invalid_length", field = "name" } }
    end
    const name_2 = buffer_readstring(b, offset, name_2_length)
    offset += name_2_length
    name = name_2
  else
    name = baseline.name
  end
  return { ok = true, value = { id = id, name = name } }
end
const function Player_apply_diff_binary(baseline: Player, diff: Loom.Serialized): Loom.Result<Player, Loom.DeserializeError>
  const ok, result = pcall(Player_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Player_serializer: Loom.Serializer<Player> = { serialize = Player_serialize_binary, deserialize = Player_deserialize_binary, diff = Player_diff_binary, applyDiff = Player_apply_diff_binary }