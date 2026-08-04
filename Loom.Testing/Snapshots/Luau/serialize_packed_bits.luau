const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writebits = buffer.writebits
const buffer_len = buffer.len
const buffer_readbits = buffer.readbits
const buffer_copy = buffer.copy
type Flags = {
  read kind: "spawn",
  read alive: boolean,
  read ready: boolean,
  read health: number,
}
const function Flags_serialize_binary(value: Flags): Loom.Serialized
  const b = buffer_create(2)
  buffer_writebits(b, 0, 1, if value.alive then 1 else 0)
  buffer_writebits(b, 1, 1, if value.ready then 1 else 0)
  buffer_writebits(b, 2, 7, math.round(value.health))
  return { buffer = b }
end
const function Flags_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Flags, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 2 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  return { ok = true, value = { kind = "spawn", alive = buffer_readbits(b, 0, 1) == 1, ready = buffer_readbits(b, 1, 1) == 1, health = buffer_readbits(b, 2, 7) } }
end
const function Flags_diff_binary_write(baseline: Flags, value: Flags, b: buffer)
  local offset = 2
  const alive_changed = not Loom.deep_equal(baseline.alive, value.alive)
  buffer_writebits(b, 0, 1, if alive_changed then 1 else 0)
  if alive_changed then
    buffer_writebits(b, 1, 1, if value.alive then 1 else 0)
  end
  const ready_changed = not Loom.deep_equal(baseline.ready, value.ready)
  buffer_writebits(b, 2, 1, if ready_changed then 1 else 0)
  if ready_changed then
    buffer_writebits(b, 3, 1, if value.ready then 1 else 0)
  end
  const health_changed = not Loom.deep_equal(baseline.health, value.health)
  buffer_writebits(b, 4, 1, if health_changed then 1 else 0)
  if health_changed then
    buffer_writebits(b, 5, 7, math.round(value.health))
  end
  return offset
end
const function Flags_diff_binary_attempt(baseline: Flags, value: Flags, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Flags_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Flags_diff_binary_attempt(baseline, value, size * 2)
end
const function Flags_diff_binary(baseline: Flags, value: Flags): Loom.Serialized
  const baseline_s = Flags_serialize_binary(baseline)
  const value_s = Flags_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 2
  const scratch, written = Flags_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Flags_apply_diff_binary_read(baseline: Flags, diff: Loom.Serialized): Loom.Result<Flags, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 2 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 2
  const alive_changed = buffer_readbits(b, 0, 1) == 1
  local alive = nil
  if alive_changed then
    alive = buffer_readbits(b, 1, 1) == 1
  else
    alive = baseline.alive
  end
  const ready_changed = buffer_readbits(b, 2, 1) == 1
  local ready = nil
  if ready_changed then
    ready = buffer_readbits(b, 3, 1) == 1
  else
    ready = baseline.ready
  end
  const health_changed = buffer_readbits(b, 4, 1) == 1
  local health = nil
  if health_changed then
    health = buffer_readbits(b, 5, 7)
  else
    health = baseline.health
  end
  return { ok = true, value = { kind = "spawn", alive = alive, ready = ready, health = health } }
end
const function Flags_apply_diff_binary(baseline: Flags, diff: Loom.Serialized): Loom.Result<Flags, Loom.DeserializeError>
  const ok, result = pcall(Flags_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Flags_serializer: Loom.Serializer<Flags> = { serialize = Flags_serialize_binary, deserialize = Flags_deserialize_binary, diff = Flags_diff_binary, applyDiff = Flags_apply_diff_binary }