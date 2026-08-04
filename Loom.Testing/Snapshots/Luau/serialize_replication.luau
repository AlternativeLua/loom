const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writebits = buffer.writebits
const buffer_len = buffer.len
const buffer_readbits = buffer.readbits
const buffer_copy = buffer.copy
const buffer_writei16 = buffer.writei16
const buffer_readi16 = buffer.readi16
const buffer_writeu8 = buffer.writeu8
const buffer_readu8 = buffer.readu8
const buffer_writeu16 = buffer.writeu16
const buffer_writeu32 = buffer.writeu32
const buffer_writestring = buffer.writestring
const buffer_writef32 = buffer.writef32
const buffer_readu16 = buffer.readu16
const buffer_readu32 = buffer.readu32
const buffer_readstring = buffer.readstring
const buffer_readf32 = buffer.readf32
-- Server-to-client replication for one player, sent every tick.
type Team = number
type IEvent<Kind> = {
  read kind: Kind & string,
}
type Damaged = IEvent<"Damaged"> & {
  read amount: number,
  read attacker: Player?,
}
const function Damaged_serialize_binary(value: Damaged): Loom.Serialized
  const blobs = {}
  const b = buffer_create(2)
  buffer_writebits(b, 0, 10, math.round(value.amount))
  buffer_writebits(b, 10, 1, if value.attacker ~= nil then 1 else 0)
  local offset = 2
  if value.attacker ~= nil then
    table.insert(blobs, value.attacker)
  end
  return { buffer = b, blobs = blobs }
end
const function Damaged_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Damaged, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 2 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const blobs = serialized.blobs
  if blobs == nil then
    return { ok = false, error = { kind = "missing_blob" } }
  end
  local blob_index = 1
  const attacker_present = buffer_readbits(b, 10, 1) == 1
  local attacker = nil
  local offset = 2
  if attacker_present then
    const attacker_blob = blobs[blob_index]
    blob_index += 1
    if attacker_blob == nil then
      return { ok = false, error = { kind = "missing_blob", field = "attacker" } }
    end
    if typeof(attacker_blob) ~= "Instance" or not attacker_blob:IsA("Player") then
      return { ok = false, error = { kind = "invalid_blob", field = "attacker" } }
    end
    attacker = attacker_blob
  end
  return { ok = true, value = { amount = buffer_readbits(b, 0, 10), attacker = attacker, kind = "Damaged" } }
end
const function Damaged_diff_binary_write(baseline: Damaged, value: Damaged, b: buffer)
  const blobs = {}
  local offset = 2
  const amount_changed = not Loom.deep_equal(baseline.amount, value.amount)
  buffer_writebits(b, 0, 1, if amount_changed then 1 else 0)
  if amount_changed then
    buffer_writebits(b, 1, 10, math.round(value.amount))
  end
  const attacker_presence_changed = (baseline.attacker ~= nil) ~= (value.attacker ~= nil)
  buffer_writebits(b, 11, 1, if attacker_presence_changed then 1 else 0)
  if attacker_presence_changed then
    buffer_writebits(b, 12, 1, if value.attacker ~= nil then 1 else 0)
    if value.attacker ~= nil then
      table.insert(blobs, value.attacker)
    end
  else
    if value.attacker ~= nil then
      const attacker_changed = not Loom.deep_equal(baseline.attacker, value.attacker)
      buffer_writebits(b, 12, 1, if attacker_changed then 1 else 0)
      if attacker_changed then
        table.insert(blobs, value.attacker)
      end
    end
  end
  return offset, blobs
end
const function Damaged_diff_binary_attempt(baseline: Damaged, value: Damaged, size: number)
  const scratch = buffer_create(size)
  const ok, written, blobs = pcall(Damaged_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written, blobs
  end
  return Damaged_diff_binary_attempt(baseline, value, size * 2)
end
const function Damaged_diff_binary(baseline: Damaged, value: Damaged): Loom.Serialized
  const baseline_s = Damaged_serialize_binary(baseline)
  const value_s = Damaged_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 2
  const scratch, written, blobs = Damaged_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result, blobs = blobs }
end
const function Damaged_apply_diff_binary_read(baseline: Damaged, diff: Loom.Serialized): Loom.Result<Damaged, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 2 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const blobs = diff.blobs
  if blobs == nil then
    return { ok = false, error = { kind = "missing_blob" } }
  end
  local blob_index = 1
  local offset = 2
  const amount_changed = buffer_readbits(b, 0, 1) == 1
  local amount = nil
  if amount_changed then
    amount = buffer_readbits(b, 1, 10)
  else
    amount = baseline.amount
  end
  const attacker_presence_changed = buffer_readbits(b, 11, 1) == 1
  local attacker = nil
  if attacker_presence_changed then
    const attacker_2_present = buffer_readbits(b, 12, 1) == 1
    local attacker_2 = nil
    if attacker_2_present then
      const attacker_blob = blobs[blob_index]
      blob_index += 1
      if attacker_blob == nil then
        return { ok = false, error = { kind = "missing_blob", field = "attacker" } }
      end
      if typeof(attacker_blob) ~= "Instance" or not attacker_blob:IsA("Player") then
        return { ok = false, error = { kind = "invalid_blob", field = "attacker" } }
      end
      attacker_2 = attacker_blob
    end
    attacker = attacker_2
  else
    if baseline.attacker ~= nil then
      const attacker_changed = buffer_readbits(b, 12, 1) == 1
      local attacker_3 = nil
      if attacker_changed then
        const attacker_blob_2 = blobs[blob_index]
        blob_index += 1
        if attacker_blob_2 == nil then
          return { ok = false, error = { kind = "missing_blob", field = "attacker" } }
        end
        if typeof(attacker_blob_2) ~= "Instance" or not attacker_blob_2:IsA("Player") then
          return { ok = false, error = { kind = "invalid_blob", field = "attacker" } }
        end
        attacker_3 = attacker_blob_2
      else
        attacker_3 = baseline.attacker
      end
      attacker = attacker_3
    end
  end
  return { ok = true, value = { amount = amount, attacker = attacker, kind = "Damaged" } }
end
const function Damaged_apply_diff_binary(baseline: Damaged, diff: Loom.Serialized): Loom.Result<Damaged, Loom.DeserializeError>
  const ok, result = pcall(Damaged_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Damaged_serializer: Loom.Serializer<Damaged> = { serialize = Damaged_serialize_binary, deserialize = Damaged_deserialize_binary, diff = Damaged_diff_binary, applyDiff = Damaged_apply_diff_binary }
type Respawned = IEvent<"Respawned"> & {
  read at: Vector3,
}
const function Respawned_serialize_binary(value: Respawned): Loom.Serialized
  const b = buffer_create(6)
  const at_value = value.at
  buffer_writei16(b, 0, at_value.X)
  buffer_writei16(b, 2, at_value.Y)
  buffer_writei16(b, 4, at_value.Z)
  return { buffer = b }
end
const function Respawned_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Respawned, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 6 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  return { ok = true, value = { at = Vector3.new(buffer_readi16(b, 0), buffer_readi16(b, 2), buffer_readi16(b, 4)), kind = "Respawned" } }
end
const function Respawned_diff_binary_write(baseline: Respawned, value: Respawned, b: buffer)
  local offset = 1
  const at_changed = not Loom.deep_equal(baseline.at, value.at)
  buffer_writebits(b, 0, 1, if at_changed then 1 else 0)
  if at_changed then
    const at_value = value.at
    buffer_writei16(b, offset, at_value.X)
    offset += 2
    buffer_writei16(b, offset, at_value.Y)
    offset += 2
    buffer_writei16(b, offset, at_value.Z)
    offset += 2
  end
  return offset
end
const function Respawned_diff_binary_attempt(baseline: Respawned, value: Respawned, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Respawned_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Respawned_diff_binary_attempt(baseline, value, size * 2)
end
const function Respawned_diff_binary(baseline: Respawned, value: Respawned): Loom.Serialized
  const baseline_s = Respawned_serialize_binary(baseline)
  const value_s = Respawned_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written = Respawned_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Respawned_apply_diff_binary_read(baseline: Respawned, diff: Loom.Serialized): Loom.Result<Respawned, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 1
  const at_changed = buffer_readbits(b, 0, 1) == 1
  local at = nil
  if at_changed then
    const read_0 = buffer_readi16(b, offset)
    offset += 2
    const read_1 = buffer_readi16(b, offset)
    offset += 2
    const read_2 = buffer_readi16(b, offset)
    offset += 2
    at = Vector3.new(read_0, read_1, read_2)
  else
    at = baseline.at
  end
  return { ok = true, value = { at = at, kind = "Respawned" } }
end
const function Respawned_apply_diff_binary(baseline: Respawned, diff: Loom.Serialized): Loom.Result<Respawned, Loom.DeserializeError>
  const ok, result = pcall(Respawned_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Respawned_serializer: Loom.Serializer<Respawned> = { serialize = Respawned_serialize_binary, deserialize = Respawned_deserialize_binary, diff = Respawned_diff_binary, applyDiff = Respawned_apply_diff_binary }
type Loadout = {
  read primary: number,
  read secondary: number,
}
const function Loadout_serialize_binary(value: Loadout): Loom.Serialized
  const b = buffer_create(2)
  buffer_writeu8(b, 0, value.primary)
  buffer_writeu8(b, 1, value.secondary)
  return { buffer = b }
end
const function Loadout_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Loadout, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 2 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  return { ok = true, value = { primary = buffer_readu8(b, 0), secondary = buffer_readu8(b, 1) } }
end
const function Loadout_diff_binary_write(baseline: Loadout, value: Loadout, b: buffer)
  local offset = 1
  const primary_changed = not Loom.deep_equal(baseline.primary, value.primary)
  buffer_writebits(b, 0, 1, if primary_changed then 1 else 0)
  if primary_changed then
    buffer_writeu8(b, offset, value.primary)
    offset += 1
  end
  const secondary_changed = not Loom.deep_equal(baseline.secondary, value.secondary)
  buffer_writebits(b, 1, 1, if secondary_changed then 1 else 0)
  if secondary_changed then
    buffer_writeu8(b, offset, value.secondary)
    offset += 1
  end
  return offset
end
const function Loadout_diff_binary_attempt(baseline: Loadout, value: Loadout, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Loadout_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Loadout_diff_binary_attempt(baseline, value, size * 2)
end
const function Loadout_diff_binary(baseline: Loadout, value: Loadout): Loom.Serialized
  const baseline_s = Loadout_serialize_binary(baseline)
  const value_s = Loadout_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written = Loadout_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Loadout_apply_diff_binary_read(baseline: Loadout, diff: Loom.Serialized): Loom.Result<Loadout, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 1
  const primary_changed = buffer_readbits(b, 0, 1) == 1
  local primary = nil
  if primary_changed then
    const read_0 = buffer_readu8(b, offset)
    offset += 1
    primary = read_0
  else
    primary = baseline.primary
  end
  const secondary_changed = buffer_readbits(b, 1, 1) == 1
  local secondary = nil
  if secondary_changed then
    const read_1 = buffer_readu8(b, offset)
    offset += 1
    secondary = read_1
  else
    secondary = baseline.secondary
  end
  return { ok = true, value = { primary = primary, secondary = secondary } }
end
const function Loadout_apply_diff_binary(baseline: Loadout, diff: Loom.Serialized): Loom.Result<Loadout, Loom.DeserializeError>
  const ok, result = pcall(Loadout_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Loadout_serializer: Loom.Serializer<Loadout> = { serialize = Loadout_serialize_binary, deserialize = Loadout_deserialize_binary, diff = Loadout_diff_binary, applyDiff = Loadout_apply_diff_binary }
type PlayerSnapshot = {
  read user_id: number,
  read display_name: string,
  read health: number,
  read alive: boolean,
  read team: Team,
  read velocity: Vector3,
  read aim: CFrame,
  read loadout: Loadout,
  read recent: { (Damaged | Respawned) },
  read character: Model?,
}
const function PlayerSnapshot_serialize_binary(value: PlayerSnapshot): Loom.Serialized
  const blobs = {}
  const team_value = value.team
  local team_tag = 0
  if team_value == 1 then
    team_tag = 1
  elseif team_value == 2 then
    team_tag = 2
  end
  const velocity_value = value.velocity
  local velocity_sentinel = 0
  if velocity_value == Vector3.zero then
    velocity_sentinel = 1
  elseif velocity_value == Vector3.one then
    velocity_sentinel = 2
  elseif velocity_value == Vector3.xAxis then
    velocity_sentinel = 3
  elseif velocity_value == Vector3.yAxis then
    velocity_sentinel = 4
  elseif velocity_value == Vector3.zAxis then
    velocity_sentinel = 5
  end
  const aim_value = value.aim
  local aim_sentinel = 0
  if aim_value == CFrame.identity then
    aim_sentinel = 1
  end
  local size = 6 + 4 + #value.display_name + (if velocity_sentinel == 0 then 6 else 0) + (if aim_sentinel == 0 then 16 else 0)
  size += 4
  size += (#value.recent * 12 + 7) // 8
  for i = 1, #value.recent do
    const recent_element_value = value.recent[i]
    local recent_element_tag = 0
    if recent_element_value.kind == "Respawned" then
      recent_element_tag = 1
    end
    if recent_element_tag == 1 then
      size += 6
    end
  end
  const b = buffer_create(size)
  buffer_writeu16(b, 2, value.user_id)
  const display_name_value = value.display_name
  const display_name_length = #display_name_value
  buffer_writeu32(b, 4, display_name_length)
  local offset = 8
  buffer_writestring(b, offset, display_name_value)
  offset += display_name_length
  buffer_writebits(b, 0, 7, math.round(value.health))
  buffer_writebits(b, 7, 1, if value.alive then 1 else 0)
  buffer_writebits(b, 8, 2, team_tag)
  buffer_writebits(b, 10, 3, velocity_sentinel)
  if velocity_sentinel == 0 then
    buffer_writei16(b, offset, velocity_value.X)
    offset += 2
    buffer_writei16(b, offset, velocity_value.Y)
    offset += 2
    buffer_writei16(b, offset, velocity_value.Z)
    offset += 2
  end
  buffer_writebits(b, 13, 1, aim_sentinel)
  if aim_sentinel == 0 then
    buffer_writef32(b, offset, aim_value.X)
    offset += 4
    buffer_writef32(b, offset, aim_value.Y)
    offset += 4
    buffer_writef32(b, offset, aim_value.Z)
    offset += 4
    buffer_writeu32(b, offset, Loom.pack_quaternion(Loom.cframe_to_quaternion(aim_value)))
    offset += 4
  end
  buffer_writeu8(b, offset, value.loadout.primary)
  offset += 1
  buffer_writeu8(b, offset, value.loadout.secondary)
  offset += 1
  buffer_writeu32(b, offset, #value.recent)
  offset += 4
  const recent_bits = offset * 8
  offset += (#value.recent * 12 + 7) // 8
  for i_2 = 1, #value.recent do
    const recent_item = value.recent[i_2]
    const recent_element_value = recent_item
    local recent_element_tag = 0
    if recent_element_value.kind == "Respawned" then
      recent_element_tag = 1
    end
    buffer_writebits(b, recent_bits + (i_2 - 1) * 12, 1, recent_element_tag)
    if recent_element_tag == 0 then
      buffer_writebits(b, recent_bits + (i_2 - 1) * 12 + 1, 10, math.round(recent_item.amount))
      buffer_writebits(b, recent_bits + (i_2 - 1) * 12 + 11, 1, if recent_item.attacker ~= nil then 1 else 0)
      if recent_item.attacker ~= nil then
        table.insert(blobs, recent_item.attacker)
      end
    elseif recent_element_tag == 1 then
      const at_value = recent_item.at
      buffer_writei16(b, offset, at_value.X)
      offset += 2
      buffer_writei16(b, offset, at_value.Y)
      offset += 2
      buffer_writei16(b, offset, at_value.Z)
      offset += 2
    end
  end
  buffer_writebits(b, 14, 1, if value.character ~= nil then 1 else 0)
  if value.character ~= nil then
    table.insert(blobs, value.character)
  end
  return { buffer = b, blobs = blobs }
end
const function PlayerSnapshot_deserialize_binary(serialized: Loom.Serialized): Loom.Result<PlayerSnapshot, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 14 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const blobs = serialized.blobs
  if blobs == nil then
    return { ok = false, error = { kind = "missing_blob" } }
  end
  local blob_index = 1
  const display_name_length = buffer_readu32(b, 4)
  local offset = 8
  if buffer_len(b) < offset + display_name_length then
    return { ok = false, error = { kind = "invalid_length", field = "display_name" } }
  end
  const display_name = buffer_readstring(b, offset, display_name_length)
  offset += display_name_length
  const team_tag = buffer_readbits(b, 8, 2)
  local team = nil
  if team_tag == 0 then
    team = 0
  elseif team_tag == 1 then
    team = 1
  elseif team_tag == 2 then
    team = 2
  else
    return { ok = false, error = { kind = "invalid_tag", field = "team" } }
  end
  const velocity_sentinel = buffer_readbits(b, 10, 3)
  local velocity = nil
  if velocity_sentinel == 0 then
    if buffer_len(b) < offset + 6 then
      return { ok = false, error = { kind = "truncated", field = "velocity" } }
    end
    const read_0 = buffer_readi16(b, offset)
    offset += 2
    const read_1 = buffer_readi16(b, offset)
    offset += 2
    const read_2 = buffer_readi16(b, offset)
    offset += 2
    velocity = Vector3.new(read_0, read_1, read_2)
  elseif velocity_sentinel == 1 then
    velocity = Vector3.zero
  elseif velocity_sentinel == 2 then
    velocity = Vector3.one
  elseif velocity_sentinel == 3 then
    velocity = Vector3.xAxis
  elseif velocity_sentinel == 4 then
    velocity = Vector3.yAxis
  elseif velocity_sentinel == 5 then
    velocity = Vector3.zAxis
  else
    return { ok = false, error = { kind = "invalid_tag", field = "velocity" } }
  end
  const aim_sentinel = buffer_readbits(b, 13, 1)
  local aim = nil
  if aim_sentinel == 0 then
    if buffer_len(b) < offset + 16 then
      return { ok = false, error = { kind = "truncated", field = "aim" } }
    end
    const read_3 = buffer_readf32(b, offset)
    offset += 4
    const read_4 = buffer_readf32(b, offset)
    offset += 4
    const read_5 = buffer_readf32(b, offset)
    offset += 4
    const read_6 = buffer_readu32(b, offset)
    offset += 4
    aim = CFrame.new(read_3, read_4, read_5, Loom.unpack_quaternion(read_6))
  elseif aim_sentinel == 1 then
    aim = CFrame.identity
  else
    return { ok = false, error = { kind = "invalid_tag", field = "aim" } }
  end
  const read_7 = buffer_readu8(b, offset)
  offset += 1
  const read_8 = buffer_readu8(b, offset)
  offset += 1
  const read_9 = buffer_readu32(b, offset)
  offset += 4
  const recent_count = read_9
  const recent_bits = offset * 8
  offset += (recent_count * 12 + 7) // 8
  const recent = {}
  for i = 1, recent_count do
    const recent_element_tag = buffer_readbits(b, recent_bits + (i - 1) * 12, 1)
    local recent_element = nil
    if recent_element_tag == 0 then
      const attacker_present = buffer_readbits(b, recent_bits + (i - 1) * 12 + 11, 1) == 1
      local attacker = nil
      if attacker_present then
        const attacker_blob = blobs[blob_index]
        blob_index += 1
        if attacker_blob == nil then
          return { ok = false, error = { kind = "missing_blob", field = "recent[].attacker" } }
        end
        if typeof(attacker_blob) ~= "Instance" or not attacker_blob:IsA("Player") then
          return { ok = false, error = { kind = "invalid_blob", field = "recent[].attacker" } }
        end
        attacker = attacker_blob
      end
      recent_element = { kind = "Damaged", amount = buffer_readbits(b, recent_bits + (i - 1) * 12 + 1, 10), attacker = attacker }
    elseif recent_element_tag == 1 then
      if buffer_len(b) < offset + 6 then
        return { ok = false, error = { kind = "truncated", field = "recent[]" } }
      end
      const read_10 = buffer_readi16(b, offset)
      offset += 2
      const read_11 = buffer_readi16(b, offset)
      offset += 2
      const read_12 = buffer_readi16(b, offset)
      offset += 2
      recent_element = { kind = "Respawned", at = Vector3.new(read_10, read_11, read_12) }
    else
      return { ok = false, error = { kind = "invalid_tag", field = "recent[]" } }
    end
    recent[i] = recent_element
  end
  const character_present = buffer_readbits(b, 14, 1) == 1
  local character = nil
  if character_present then
    const character_blob = blobs[blob_index]
    blob_index += 1
    if character_blob == nil then
      return { ok = false, error = { kind = "missing_blob", field = "character" } }
    end
    if typeof(character_blob) ~= "Instance" or not character_blob:IsA("Model") then
      return { ok = false, error = { kind = "invalid_blob", field = "character" } }
    end
    character = character_blob
  end
  return { ok = true, value = {
    user_id = buffer_readu16(b, 2),
    display_name = display_name,
    health = buffer_readbits(b, 0, 7),
    alive = buffer_readbits(b, 7, 1) == 1,
    team = team,
    velocity = velocity,
    aim = aim,
    loadout = { primary = read_7, secondary = read_8 },
    recent = recent,
    character = character,
  } }
end
const function PlayerSnapshot_diff_binary_write(baseline: PlayerSnapshot, value: PlayerSnapshot, b: buffer)
  const blobs = {}
  local offset = 4
  const user_id_changed = not Loom.deep_equal(baseline.user_id, value.user_id)
  buffer_writebits(b, 0, 1, if user_id_changed then 1 else 0)
  if user_id_changed then
    buffer_writeu16(b, offset, value.user_id)
    offset += 2
  end
  const display_name_changed = not Loom.deep_equal(baseline.display_name, value.display_name)
  buffer_writebits(b, 1, 1, if display_name_changed then 1 else 0)
  if display_name_changed then
    const display_name_value = value.display_name
    const display_name_length = #display_name_value
    buffer_writeu32(b, offset, display_name_length)
    offset += 4
    buffer_writestring(b, offset, display_name_value)
    offset += display_name_length
  end
  const health_changed = not Loom.deep_equal(baseline.health, value.health)
  buffer_writebits(b, 2, 1, if health_changed then 1 else 0)
  if health_changed then
    buffer_writebits(b, 3, 7, math.round(value.health))
  end
  const alive_changed = not Loom.deep_equal(baseline.alive, value.alive)
  buffer_writebits(b, 10, 1, if alive_changed then 1 else 0)
  if alive_changed then
    buffer_writebits(b, 11, 1, if value.alive then 1 else 0)
  end
  const team_tag_b_value = baseline.team
  local team_tag_b = 0
  if team_tag_b_value == 1 then
    team_tag_b = 1
  elseif team_tag_b_value == 2 then
    team_tag_b = 2
  end
  const team_tag_c_value = value.team
  local team_tag_c = 0
  if team_tag_c_value == 1 then
    team_tag_c = 1
  elseif team_tag_c_value == 2 then
    team_tag_c = 2
  end
  const team_tag_changed = team_tag_b ~= team_tag_c
  buffer_writebits(b, 12, 1, if team_tag_changed then 1 else 0)
  if team_tag_changed then
    const team_value = value.team
    local team_tag = 0
    if team_value == 1 then
      team_tag = 1
    elseif team_value == 2 then
      team_tag = 2
    end
    buffer_writebits(b, 13, 2, team_tag)
  else
  end
  const velocity_changed = not Loom.deep_equal(baseline.velocity, value.velocity)
  buffer_writebits(b, 15, 1, if velocity_changed then 1 else 0)
  if velocity_changed then
    const velocity_value = value.velocity
    local velocity_sentinel = 0
    if velocity_value == Vector3.zero then
      velocity_sentinel = 1
    elseif velocity_value == Vector3.one then
      velocity_sentinel = 2
    elseif velocity_value == Vector3.xAxis then
      velocity_sentinel = 3
    elseif velocity_value == Vector3.yAxis then
      velocity_sentinel = 4
    elseif velocity_value == Vector3.zAxis then
      velocity_sentinel = 5
    end
    buffer_writebits(b, 16, 3, velocity_sentinel)
    if velocity_sentinel == 0 then
      buffer_writei16(b, offset, velocity_value.X)
      offset += 2
      buffer_writei16(b, offset, velocity_value.Y)
      offset += 2
      buffer_writei16(b, offset, velocity_value.Z)
      offset += 2
    end
  end
  const aim_changed = not Loom.deep_equal(baseline.aim, value.aim)
  buffer_writebits(b, 19, 1, if aim_changed then 1 else 0)
  if aim_changed then
    const aim_value = value.aim
    local aim_sentinel = 0
    if aim_value == CFrame.identity then
      aim_sentinel = 1
    end
    buffer_writebits(b, 20, 1, aim_sentinel)
    if aim_sentinel == 0 then
      buffer_writef32(b, offset, aim_value.X)
      offset += 4
      buffer_writef32(b, offset, aim_value.Y)
      offset += 4
      buffer_writef32(b, offset, aim_value.Z)
      offset += 4
      buffer_writeu32(b, offset, Loom.pack_quaternion(Loom.cframe_to_quaternion(aim_value)))
      offset += 4
    end
  end
  const primary_changed = not Loom.deep_equal(baseline.loadout.primary, value.loadout.primary)
  buffer_writebits(b, 21, 1, if primary_changed then 1 else 0)
  if primary_changed then
    buffer_writeu8(b, offset, value.loadout.primary)
    offset += 1
  end
  const secondary_changed = not Loom.deep_equal(baseline.loadout.secondary, value.loadout.secondary)
  buffer_writebits(b, 22, 1, if secondary_changed then 1 else 0)
  if secondary_changed then
    buffer_writeu8(b, offset, value.loadout.secondary)
    offset += 1
  end
  const recent_length_changed = #baseline.recent ~= #value.recent
  buffer_writebits(b, 23, 1, if recent_length_changed then 1 else 0)
  if recent_length_changed then
    buffer_writeu32(b, offset, #value.recent)
    offset += 4
    const recent_bits = offset * 8
    offset += (#value.recent * 12 + 7) // 8
    for i = 1, #value.recent do
      const recent_item = value.recent[i]
      const recent_element_value = recent_item
      local recent_element_tag = 0
      if recent_element_value.kind == "Respawned" then
        recent_element_tag = 1
      end
      buffer_writebits(b, recent_bits + (i - 1) * 12, 1, recent_element_tag)
      if recent_element_tag == 0 then
        buffer_writebits(b, recent_bits + (i - 1) * 12 + 1, 10, math.round(recent_item.amount))
        buffer_writebits(b, recent_bits + (i - 1) * 12 + 11, 1, if recent_item.attacker ~= nil then 1 else 0)
        if recent_item.attacker ~= nil then
          table.insert(blobs, recent_item.attacker)
        end
      elseif recent_element_tag == 1 then
        const at_value = recent_item.at
        buffer_writei16(b, offset, at_value.X)
        offset += 2
        buffer_writei16(b, offset, at_value.Y)
        offset += 2
        buffer_writei16(b, offset, at_value.Z)
        offset += 2
      end
    end
  else
    const recent_bits_2 = offset * 8
    offset += (#value.recent * 14 + 7) // 8
    for i_2 = 1, #value.recent do
      const recent_element_tag_b_value = baseline.recent[i_2]
      local recent_element_tag_b = 0
      if recent_element_tag_b_value.kind == "Respawned" then
        recent_element_tag_b = 1
      end
      const recent_element_tag_c_value = value.recent[i_2]
      local recent_element_tag_c = 0
      if recent_element_tag_c_value.kind == "Respawned" then
        recent_element_tag_c = 1
      end
      const recent_element_tag_changed = recent_element_tag_b ~= recent_element_tag_c
      buffer_writebits(b, recent_bits_2 + (i_2 - 1) * 14, 1, if recent_element_tag_changed then 1 else 0)
      if recent_element_tag_changed then
        const recent_element_value = value.recent[i_2]
        local recent_element_tag = 0
        if recent_element_value.kind == "Respawned" then
          recent_element_tag = 1
        end
        buffer_writebits(b, recent_bits_2 + (i_2 - 1) * 14 + 1, 1, recent_element_tag)
        if recent_element_tag == 0 then
          buffer_writebits(b, recent_bits_2 + (i_2 - 1) * 14 + 2, 10, math.round(value.recent[i_2].amount))
          buffer_writebits(b, recent_bits_2 + (i_2 - 1) * 14 + 12, 1, if value.recent[i_2].attacker ~= nil then 1 else 0)
          if value.recent[i_2].attacker ~= nil then
            table.insert(blobs, value.recent[i_2].attacker)
          end
        elseif recent_element_tag == 1 then
          const at_value_2 = value.recent[i_2].at
          buffer_writei16(b, offset, at_value_2.X)
          offset += 2
          buffer_writei16(b, offset, at_value_2.Y)
          offset += 2
          buffer_writei16(b, offset, at_value_2.Z)
          offset += 2
        end
      else
        if recent_element_tag_c == 0 then
          const amount_changed = not Loom.deep_equal(baseline.recent[i_2].amount, value.recent[i_2].amount)
          buffer_writebits(b, recent_bits_2 + (i_2 - 1) * 14 + 1, 1, if amount_changed then 1 else 0)
          if amount_changed then
            buffer_writebits(b, recent_bits_2 + (i_2 - 1) * 14 + 2, 10, math.round(value.recent[i_2].amount))
          end
          const attacker_presence_changed = (baseline.recent[i_2].attacker ~= nil) ~= (value.recent[i_2].attacker ~= nil)
          buffer_writebits(b, recent_bits_2 + (i_2 - 1) * 14 + 12, 1, if attacker_presence_changed then 1 else 0)
          if attacker_presence_changed then
            buffer_writebits(b, recent_bits_2 + (i_2 - 1) * 14 + 13, 1, if value.recent[i_2].attacker ~= nil then 1 else 0)
            if value.recent[i_2].attacker ~= nil then
              table.insert(blobs, value.recent[i_2].attacker)
            end
          else
            if value.recent[i_2].attacker ~= nil then
              const attacker_changed = not Loom.deep_equal(baseline.recent[i_2].attacker, value.recent[i_2].attacker)
              buffer_writebits(b, recent_bits_2 + (i_2 - 1) * 14 + 13, 1, if attacker_changed then 1 else 0)
              if attacker_changed then
                table.insert(blobs, value.recent[i_2].attacker)
              end
            end
          end
        elseif recent_element_tag_c == 1 then
          const at_changed = not Loom.deep_equal(baseline.recent[i_2].at, value.recent[i_2].at)
          buffer_writebits(b, recent_bits_2 + (i_2 - 1) * 14 + 1, 1, if at_changed then 1 else 0)
          if at_changed then
            const at_value_3 = value.recent[i_2].at
            buffer_writei16(b, offset, at_value_3.X)
            offset += 2
            buffer_writei16(b, offset, at_value_3.Y)
            offset += 2
            buffer_writei16(b, offset, at_value_3.Z)
            offset += 2
          end
        end
      end
    end
  end
  const character_presence_changed = (baseline.character ~= nil) ~= (value.character ~= nil)
  buffer_writebits(b, 24, 1, if character_presence_changed then 1 else 0)
  if character_presence_changed then
    buffer_writebits(b, 25, 1, if value.character ~= nil then 1 else 0)
    if value.character ~= nil then
      table.insert(blobs, value.character)
    end
  else
    if value.character ~= nil then
      const character_changed = not Loom.deep_equal(baseline.character, value.character)
      buffer_writebits(b, 25, 1, if character_changed then 1 else 0)
      if character_changed then
        table.insert(blobs, value.character)
      end
    end
  end
  return offset, blobs
end
const function PlayerSnapshot_diff_binary_attempt(baseline: PlayerSnapshot, value: PlayerSnapshot, size: number)
  const scratch = buffer_create(size)
  const ok, written, blobs = pcall(PlayerSnapshot_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written, blobs
  end
  return PlayerSnapshot_diff_binary_attempt(baseline, value, size * 2)
end
const function PlayerSnapshot_diff_binary(baseline: PlayerSnapshot, value: PlayerSnapshot): Loom.Serialized
  const baseline_s = PlayerSnapshot_serialize_binary(baseline)
  const value_s = PlayerSnapshot_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 4
  const scratch, written, blobs = PlayerSnapshot_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result, blobs = blobs }
end
const function PlayerSnapshot_apply_diff_binary_read(baseline: PlayerSnapshot, diff: Loom.Serialized): Loom.Result<PlayerSnapshot, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 4 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const blobs = diff.blobs
  if blobs == nil then
    return { ok = false, error = { kind = "missing_blob" } }
  end
  local blob_index = 1
  local offset = 4
  const user_id_changed = buffer_readbits(b, 0, 1) == 1
  local user_id = nil
  if user_id_changed then
    const read_0 = buffer_readu16(b, offset)
    offset += 2
    user_id = read_0
  else
    user_id = baseline.user_id
  end
  const display_name_changed = buffer_readbits(b, 1, 1) == 1
  local display_name = nil
  if display_name_changed then
    const read_1 = buffer_readu32(b, offset)
    offset += 4
    const display_name_2_length = read_1
    if buffer_len(b) < offset + display_name_2_length then
      return { ok = false, error = { kind = "invalid_length", field = "display_name" } }
    end
    const display_name_2 = buffer_readstring(b, offset, display_name_2_length)
    offset += display_name_2_length
    display_name = display_name_2
  else
    display_name = baseline.display_name
  end
  const health_changed = buffer_readbits(b, 2, 1) == 1
  local health = nil
  if health_changed then
    health = buffer_readbits(b, 3, 7)
  else
    health = baseline.health
  end
  const alive_changed = buffer_readbits(b, 10, 1) == 1
  local alive = nil
  if alive_changed then
    alive = buffer_readbits(b, 11, 1) == 1
  else
    alive = baseline.alive
  end
  const team_tag_changed = buffer_readbits(b, 12, 1) == 1
  local team = nil
  if team_tag_changed then
    const team_2_tag = buffer_readbits(b, 13, 2)
    local team_2 = nil
    if team_2_tag == 0 then
      team_2 = 0
    elseif team_2_tag == 1 then
      team_2 = 1
    elseif team_2_tag == 2 then
      team_2 = 2
    else
      return { ok = false, error = { kind = "invalid_tag", field = "team" } }
    end
    team = team_2
  else
    const team_tag_b_value = baseline.team
    local team_tag_b = 0
    if team_tag_b_value == 1 then
      team_tag_b = 1
    elseif team_tag_b_value == 2 then
      team_tag_b = 2
    end
    if team_tag_b == 0 then
      team = 0
    elseif team_tag_b == 1 then
      team = 1
    elseif team_tag_b == 2 then
      team = 2
    end
  end
  const velocity_changed = buffer_readbits(b, 15, 1) == 1
  local velocity = nil
  if velocity_changed then
    const velocity_2_sentinel = buffer_readbits(b, 16, 3)
    local velocity_2 = nil
    if velocity_2_sentinel == 0 then
      if buffer_len(b) < offset + 6 then
        return { ok = false, error = { kind = "truncated", field = "velocity" } }
      end
      const read_2 = buffer_readi16(b, offset)
      offset += 2
      const read_3 = buffer_readi16(b, offset)
      offset += 2
      const read_4 = buffer_readi16(b, offset)
      offset += 2
      velocity_2 = Vector3.new(read_2, read_3, read_4)
    elseif velocity_2_sentinel == 1 then
      velocity_2 = Vector3.zero
    elseif velocity_2_sentinel == 2 then
      velocity_2 = Vector3.one
    elseif velocity_2_sentinel == 3 then
      velocity_2 = Vector3.xAxis
    elseif velocity_2_sentinel == 4 then
      velocity_2 = Vector3.yAxis
    elseif velocity_2_sentinel == 5 then
      velocity_2 = Vector3.zAxis
    else
      return { ok = false, error = { kind = "invalid_tag", field = "velocity" } }
    end
    velocity = velocity_2
  else
    velocity = baseline.velocity
  end
  const aim_changed = buffer_readbits(b, 19, 1) == 1
  local aim = nil
  if aim_changed then
    const aim_2_sentinel = buffer_readbits(b, 20, 1)
    local aim_2 = nil
    if aim_2_sentinel == 0 then
      if buffer_len(b) < offset + 16 then
        return { ok = false, error = { kind = "truncated", field = "aim" } }
      end
      const read_5 = buffer_readf32(b, offset)
      offset += 4
      const read_6 = buffer_readf32(b, offset)
      offset += 4
      const read_7 = buffer_readf32(b, offset)
      offset += 4
      const read_8 = buffer_readu32(b, offset)
      offset += 4
      aim_2 = CFrame.new(read_5, read_6, read_7, Loom.unpack_quaternion(read_8))
    elseif aim_2_sentinel == 1 then
      aim_2 = CFrame.identity
    else
      return { ok = false, error = { kind = "invalid_tag", field = "aim" } }
    end
    aim = aim_2
  else
    aim = baseline.aim
  end
  const primary_changed = buffer_readbits(b, 21, 1) == 1
  local primary = nil
  if primary_changed then
    const read_9 = buffer_readu8(b, offset)
    offset += 1
    primary = read_9
  else
    primary = baseline.loadout.primary
  end
  const secondary_changed = buffer_readbits(b, 22, 1) == 1
  local secondary = nil
  if secondary_changed then
    const read_10 = buffer_readu8(b, offset)
    offset += 1
    secondary = read_10
  else
    secondary = baseline.loadout.secondary
  end
  const recent_length_changed = buffer_readbits(b, 23, 1) == 1
  local recent = nil
  if recent_length_changed then
    const read_11 = buffer_readu32(b, offset)
    offset += 4
    const recent_2_count = read_11
    const recent_2_bits = offset * 8
    offset += (recent_2_count * 12 + 7) // 8
    const recent_2 = {}
    for i = 1, recent_2_count do
      const recent_element_tag = buffer_readbits(b, recent_2_bits + (i - 1) * 12, 1)
      local recent_element = nil
      if recent_element_tag == 0 then
        const attacker_present = buffer_readbits(b, recent_2_bits + (i - 1) * 12 + 11, 1) == 1
        local attacker = nil
        if attacker_present then
          const attacker_blob = blobs[blob_index]
          blob_index += 1
          if attacker_blob == nil then
            return { ok = false, error = { kind = "missing_blob", field = "recent[].attacker" } }
          end
          if typeof(attacker_blob) ~= "Instance" or not attacker_blob:IsA("Player") then
            return { ok = false, error = { kind = "invalid_blob", field = "recent[].attacker" } }
          end
          attacker = attacker_blob
        end
        recent_element = { kind = "Damaged", amount = buffer_readbits(b, recent_2_bits + (i - 1) * 12 + 1, 10), attacker = attacker }
      elseif recent_element_tag == 1 then
        if buffer_len(b) < offset + 6 then
          return { ok = false, error = { kind = "truncated", field = "recent[]" } }
        end
        const read_12 = buffer_readi16(b, offset)
        offset += 2
        const read_13 = buffer_readi16(b, offset)
        offset += 2
        const read_14 = buffer_readi16(b, offset)
        offset += 2
        recent_element = { kind = "Respawned", at = Vector3.new(read_12, read_13, read_14) }
      else
        return { ok = false, error = { kind = "invalid_tag", field = "recent[]" } }
      end
      recent_2[i] = recent_element
    end
    recent = recent_2
  else
    const recent_count = #baseline.recent
    const recent_bits = offset * 8
    offset += (recent_count * 14 + 7) // 8
    recent = {}
    for i_2 = 1, recent_count do
      const recent_element_tag_changed = buffer_readbits(b, recent_bits + (i_2 - 1) * 14, 1) == 1
      local recent_element_2 = nil
      if recent_element_tag_changed then
        const recent_element_3_tag = buffer_readbits(b, recent_bits + (i_2 - 1) * 14 + 1, 1)
        local recent_element_3 = nil
        if recent_element_3_tag == 0 then
          const attacker_2_present = buffer_readbits(b, recent_bits + (i_2 - 1) * 14 + 12, 1) == 1
          local attacker_2 = nil
          if attacker_2_present then
            const attacker_blob_2 = blobs[blob_index]
            blob_index += 1
            if attacker_blob_2 == nil then
              return { ok = false, error = { kind = "missing_blob", field = "recent[].attacker" } }
            end
            if typeof(attacker_blob_2) ~= "Instance" or not attacker_blob_2:IsA("Player") then
              return { ok = false, error = { kind = "invalid_blob", field = "recent[].attacker" } }
            end
            attacker_2 = attacker_blob_2
          end
          recent_element_3 = { kind = "Damaged", amount = buffer_readbits(b, recent_bits + (i_2 - 1) * 14 + 2, 10), attacker = attacker_2 }
        elseif recent_element_3_tag == 1 then
          if buffer_len(b) < offset + 6 then
            return { ok = false, error = { kind = "truncated", field = "recent[]" } }
          end
          const read_15 = buffer_readi16(b, offset)
          offset += 2
          const read_16 = buffer_readi16(b, offset)
          offset += 2
          const read_17 = buffer_readi16(b, offset)
          offset += 2
          recent_element_3 = { kind = "Respawned", at = Vector3.new(read_15, read_16, read_17) }
        else
          return { ok = false, error = { kind = "invalid_tag", field = "recent[]" } }
        end
        recent_element_2 = recent_element_3
      else
        const recent_element_tag_b_value = baseline.recent[i_2]
        local recent_element_tag_b = 0
        if recent_element_tag_b_value.kind == "Respawned" then
          recent_element_tag_b = 1
        end
        if recent_element_tag_b == 0 then
          const amount_changed = buffer_readbits(b, recent_bits + (i_2 - 1) * 14 + 1, 1) == 1
          local amount = nil
          if amount_changed then
            amount = buffer_readbits(b, recent_bits + (i_2 - 1) * 14 + 2, 10)
          else
            amount = baseline.recent[i_2].amount
          end
          const attacker_presence_changed = buffer_readbits(b, recent_bits + (i_2 - 1) * 14 + 12, 1) == 1
          local attacker_3 = nil
          if attacker_presence_changed then
            const attacker_4_present = buffer_readbits(b, recent_bits + (i_2 - 1) * 14 + 13, 1) == 1
            local attacker_4 = nil
            if attacker_4_present then
              const attacker_blob_3 = blobs[blob_index]
              blob_index += 1
              if attacker_blob_3 == nil then
                return { ok = false, error = { kind = "missing_blob", field = "recent[].attacker" } }
              end
              if typeof(attacker_blob_3) ~= "Instance" or not attacker_blob_3:IsA("Player") then
                return { ok = false, error = { kind = "invalid_blob", field = "recent[].attacker" } }
              end
              attacker_4 = attacker_blob_3
            end
            attacker_3 = attacker_4
          else
            if baseline.recent[i_2].attacker ~= nil then
              const attacker_changed = buffer_readbits(b, recent_bits + (i_2 - 1) * 14 + 13, 1) == 1
              local attacker_5 = nil
              if attacker_changed then
                const attacker_blob_4 = blobs[blob_index]
                blob_index += 1
                if attacker_blob_4 == nil then
                  return { ok = false, error = { kind = "missing_blob", field = "recent[].attacker" } }
                end
                if typeof(attacker_blob_4) ~= "Instance" or not attacker_blob_4:IsA("Player") then
                  return { ok = false, error = { kind = "invalid_blob", field = "recent[].attacker" } }
                end
                attacker_5 = attacker_blob_4
              else
                attacker_5 = baseline.recent[i_2].attacker
              end
              attacker_3 = attacker_5
            end
          end
          recent_element_2 = { kind = "Damaged", amount = amount, attacker = attacker_3 }
        elseif recent_element_tag_b == 1 then
          const at_changed = buffer_readbits(b, recent_bits + (i_2 - 1) * 14 + 1, 1) == 1
          local at = nil
          if at_changed then
            const read_18 = buffer_readi16(b, offset)
            offset += 2
            const read_19 = buffer_readi16(b, offset)
            offset += 2
            const read_20 = buffer_readi16(b, offset)
            offset += 2
            at = Vector3.new(read_18, read_19, read_20)
          else
            at = baseline.recent[i_2].at
          end
          recent_element_2 = { kind = "Respawned", at = at }
        end
      end
      recent[i_2] = recent_element_2
    end
  end
  const character_presence_changed = buffer_readbits(b, 24, 1) == 1
  local character = nil
  if character_presence_changed then
    const character_2_present = buffer_readbits(b, 25, 1) == 1
    local character_2 = nil
    if character_2_present then
      const character_blob = blobs[blob_index]
      blob_index += 1
      if character_blob == nil then
        return { ok = false, error = { kind = "missing_blob", field = "character" } }
      end
      if typeof(character_blob) ~= "Instance" or not character_blob:IsA("Model") then
        return { ok = false, error = { kind = "invalid_blob", field = "character" } }
      end
      character_2 = character_blob
    end
    character = character_2
  else
    if baseline.character ~= nil then
      const character_changed = buffer_readbits(b, 25, 1) == 1
      local character_3 = nil
      if character_changed then
        const character_blob_2 = blobs[blob_index]
        blob_index += 1
        if character_blob_2 == nil then
          return { ok = false, error = { kind = "missing_blob", field = "character" } }
        end
        if typeof(character_blob_2) ~= "Instance" or not character_blob_2:IsA("Model") then
          return { ok = false, error = { kind = "invalid_blob", field = "character" } }
        end
        character_3 = character_blob_2
      else
        character_3 = baseline.character
      end
      character = character_3
    end
  end
  return { ok = true, value = {
    user_id = user_id,
    display_name = display_name,
    health = health,
    alive = alive,
    team = team,
    velocity = velocity,
    aim = aim,
    loadout = { primary = primary, secondary = secondary },
    recent = recent,
    character = character,
  } }
end
const function PlayerSnapshot_apply_diff_binary(baseline: PlayerSnapshot, diff: Loom.Serialized): Loom.Result<PlayerSnapshot, Loom.DeserializeError>
  const ok, result = pcall(PlayerSnapshot_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const PlayerSnapshot_serializer: Loom.Serializer<PlayerSnapshot> = { serialize = PlayerSnapshot_serialize_binary, deserialize = PlayerSnapshot_deserialize_binary, diff = PlayerSnapshot_diff_binary, applyDiff = PlayerSnapshot_apply_diff_binary }