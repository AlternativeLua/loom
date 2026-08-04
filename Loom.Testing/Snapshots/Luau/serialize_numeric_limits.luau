const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writei8 = buffer.writei8
const buffer_writeu8 = buffer.writeu8
const buffer_writei16 = buffer.writei16
const buffer_writeu16 = buffer.writeu16
const buffer_writei32 = buffer.writei32
const buffer_writeu32 = buffer.writeu32
const buffer_writef32 = buffer.writef32
const buffer_writef64 = buffer.writef64
const buffer_len = buffer.len
const buffer_readi8 = buffer.readi8
const buffer_readu8 = buffer.readu8
const buffer_readi16 = buffer.readi16
const buffer_readu16 = buffer.readu16
const buffer_readi32 = buffer.readi32
const buffer_readu32 = buffer.readu32
const buffer_readf32 = buffer.readf32
const buffer_readf64 = buffer.readf64
const buffer_writebits = buffer.writebits
const buffer_copy = buffer.copy
const buffer_readbits = buffer.readbits
type Limits = {
  read i8: number,
  read u8: number,
  read i16: number,
  read u16: number,
  read i32: number,
  read u32: number,
  read f32: number,
  read f64: number,
}
const function Limits_serialize_binary(value: Limits): Loom.Serialized
  const b = buffer_create(26)
  buffer_writei8(b, 0, value.i8)
  buffer_writeu8(b, 1, value.u8)
  buffer_writei16(b, 2, value.i16)
  buffer_writeu16(b, 4, value.u16)
  buffer_writei32(b, 6, value.i32)
  buffer_writeu32(b, 10, value.u32)
  buffer_writef32(b, 14, value.f32)
  buffer_writef64(b, 18, value.f64)
  return { buffer = b }
end
const function Limits_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Limits, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 26 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  return { ok = true, value = {
    i8 = buffer_readi8(b, 0),
    u8 = buffer_readu8(b, 1),
    i16 = buffer_readi16(b, 2),
    u16 = buffer_readu16(b, 4),
    i32 = buffer_readi32(b, 6),
    u32 = buffer_readu32(b, 10),
    f32 = buffer_readf32(b, 14),
    f64 = buffer_readf64(b, 18),
  } }
end
const function Limits_diff_binary_write(baseline: Limits, value: Limits, b: buffer)
  local offset = 1
  const i8_changed = not Loom.deep_equal(baseline.i8, value.i8)
  buffer_writebits(b, 0, 1, if i8_changed then 1 else 0)
  if i8_changed then
    buffer_writei8(b, offset, value.i8)
    offset += 1
  end
  const u8_changed = not Loom.deep_equal(baseline.u8, value.u8)
  buffer_writebits(b, 1, 1, if u8_changed then 1 else 0)
  if u8_changed then
    buffer_writeu8(b, offset, value.u8)
    offset += 1
  end
  const i16_changed = not Loom.deep_equal(baseline.i16, value.i16)
  buffer_writebits(b, 2, 1, if i16_changed then 1 else 0)
  if i16_changed then
    buffer_writei16(b, offset, value.i16)
    offset += 2
  end
  const u16_changed = not Loom.deep_equal(baseline.u16, value.u16)
  buffer_writebits(b, 3, 1, if u16_changed then 1 else 0)
  if u16_changed then
    buffer_writeu16(b, offset, value.u16)
    offset += 2
  end
  const i32_changed = not Loom.deep_equal(baseline.i32, value.i32)
  buffer_writebits(b, 4, 1, if i32_changed then 1 else 0)
  if i32_changed then
    buffer_writei32(b, offset, value.i32)
    offset += 4
  end
  const u32_changed = not Loom.deep_equal(baseline.u32, value.u32)
  buffer_writebits(b, 5, 1, if u32_changed then 1 else 0)
  if u32_changed then
    buffer_writeu32(b, offset, value.u32)
    offset += 4
  end
  const f32_changed = not Loom.deep_equal(baseline.f32, value.f32)
  buffer_writebits(b, 6, 1, if f32_changed then 1 else 0)
  if f32_changed then
    buffer_writef32(b, offset, value.f32)
    offset += 4
  end
  const f64_changed = not Loom.deep_equal(baseline.f64, value.f64)
  buffer_writebits(b, 7, 1, if f64_changed then 1 else 0)
  if f64_changed then
    buffer_writef64(b, offset, value.f64)
    offset += 8
  end
  return offset
end
const function Limits_diff_binary_attempt(baseline: Limits, value: Limits, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Limits_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Limits_diff_binary_attempt(baseline, value, size * 2)
end
const function Limits_diff_binary(baseline: Limits, value: Limits): Loom.Serialized
  const baseline_s = Limits_serialize_binary(baseline)
  const value_s = Limits_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written = Limits_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Limits_apply_diff_binary_read(baseline: Limits, diff: Loom.Serialized): Loom.Result<Limits, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 1
  const i8_changed = buffer_readbits(b, 0, 1) == 1
  local i8 = nil
  if i8_changed then
    const read_0 = buffer_readi8(b, offset)
    offset += 1
    i8 = read_0
  else
    i8 = baseline.i8
  end
  const u8_changed = buffer_readbits(b, 1, 1) == 1
  local u8 = nil
  if u8_changed then
    const read_1 = buffer_readu8(b, offset)
    offset += 1
    u8 = read_1
  else
    u8 = baseline.u8
  end
  const i16_changed = buffer_readbits(b, 2, 1) == 1
  local i16 = nil
  if i16_changed then
    const read_2 = buffer_readi16(b, offset)
    offset += 2
    i16 = read_2
  else
    i16 = baseline.i16
  end
  const u16_changed = buffer_readbits(b, 3, 1) == 1
  local u16 = nil
  if u16_changed then
    const read_3 = buffer_readu16(b, offset)
    offset += 2
    u16 = read_3
  else
    u16 = baseline.u16
  end
  const i32_changed = buffer_readbits(b, 4, 1) == 1
  local i32 = nil
  if i32_changed then
    const read_4 = buffer_readi32(b, offset)
    offset += 4
    i32 = read_4
  else
    i32 = baseline.i32
  end
  const u32_changed = buffer_readbits(b, 5, 1) == 1
  local u32 = nil
  if u32_changed then
    const read_5 = buffer_readu32(b, offset)
    offset += 4
    u32 = read_5
  else
    u32 = baseline.u32
  end
  const f32_changed = buffer_readbits(b, 6, 1) == 1
  local f32 = nil
  if f32_changed then
    const read_6 = buffer_readf32(b, offset)
    offset += 4
    f32 = read_6
  else
    f32 = baseline.f32
  end
  const f64_changed = buffer_readbits(b, 7, 1) == 1
  local f64 = nil
  if f64_changed then
    const read_7 = buffer_readf64(b, offset)
    offset += 8
    f64 = read_7
  else
    f64 = baseline.f64
  end
  return { ok = true, value = {
    i8 = i8,
    u8 = u8,
    i16 = i16,
    u16 = u16,
    i32 = i32,
    u32 = u32,
    f32 = f32,
    f64 = f64,
  } }
end
const function Limits_apply_diff_binary(baseline: Limits, diff: Loom.Serialized): Loom.Result<Limits, Loom.DeserializeError>
  const ok, result = pcall(Limits_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Limits_serializer: Loom.Serializer<Limits> = { serialize = Limits_serialize_binary, deserialize = Limits_deserialize_binary, diff = Limits_diff_binary, applyDiff = Limits_apply_diff_binary }