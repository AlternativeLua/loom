const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writebits = buffer.writebits
const buffer_len = buffer.len
const buffer_readbits = buffer.readbits
const buffer_copy = buffer.copy
type Dials = {
  read opacity: number,
  read offset: number,
  read health: number,
}
const function Dials_serialize_binary(value: Dials): Loom.Serialized
  const b = buffer_create(3)
  buffer_writebits(b, 0, 7, math.round(value.opacity / 0.01))
  buffer_writebits(b, 7, 9, math.round((value.offset - -100) / 0.5))
  buffer_writebits(b, 16, 7, math.round(value.health))
  return { buffer = b }
end
const function Dials_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Dials, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 3 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  return { ok = true, value = { opacity = buffer_readbits(b, 0, 7) * 0.01, offset = -100 + buffer_readbits(b, 7, 9) * 0.5, health = buffer_readbits(b, 16, 7) } }
end
const function Dials_diff_binary_write(baseline: Dials, value: Dials, b: buffer)
  local offset = 4
  const opacity_changed = not Loom.deep_equal(baseline.opacity, value.opacity)
  buffer_writebits(b, 0, 1, if opacity_changed then 1 else 0)
  if opacity_changed then
    buffer_writebits(b, 1, 7, math.round(value.opacity / 0.01))
  end
  const offset_changed = not Loom.deep_equal(baseline.offset, value.offset)
  buffer_writebits(b, 8, 1, if offset_changed then 1 else 0)
  if offset_changed then
    buffer_writebits(b, 9, 9, math.round((value.offset - -100) / 0.5))
  end
  const health_changed = not Loom.deep_equal(baseline.health, value.health)
  buffer_writebits(b, 18, 1, if health_changed then 1 else 0)
  if health_changed then
    buffer_writebits(b, 19, 7, math.round(value.health))
  end
  return offset
end
const function Dials_diff_binary_attempt(baseline: Dials, value: Dials, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Dials_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Dials_diff_binary_attempt(baseline, value, size * 2)
end
const function Dials_diff_binary(baseline: Dials, value: Dials): Loom.Serialized
  const baseline_s = Dials_serialize_binary(baseline)
  const value_s = Dials_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 4
  const scratch, written = Dials_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Dials_apply_diff_binary_read(baseline: Dials, diff: Loom.Serialized): Loom.Result<Dials, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 4 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 4
  const opacity_changed = buffer_readbits(b, 0, 1) == 1
  local opacity = nil
  if opacity_changed then
    opacity = buffer_readbits(b, 1, 7) * 0.01
  else
    opacity = baseline.opacity
  end
  const offset_changed = buffer_readbits(b, 8, 1) == 1
  local offset = nil
  if offset_changed then
    offset = -100 + buffer_readbits(b, 9, 9) * 0.5
  else
    offset = baseline.offset
  end
  const health_changed = buffer_readbits(b, 18, 1) == 1
  local health = nil
  if health_changed then
    health = buffer_readbits(b, 19, 7)
  else
    health = baseline.health
  end
  return { ok = true, value = { opacity = opacity, offset = offset, health = health } }
end
const function Dials_apply_diff_binary(baseline: Dials, diff: Loom.Serialized): Loom.Result<Dials, Loom.DeserializeError>
  const ok, result = pcall(Dials_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Dials_serializer: Loom.Serializer<Dials> = { serialize = Dials_serialize_binary, deserialize = Dials_deserialize_binary, diff = Dials_diff_binary, applyDiff = Dials_apply_diff_binary }