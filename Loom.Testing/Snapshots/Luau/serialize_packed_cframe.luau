const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writebits = buffer.writebits
const buffer_writef32 = buffer.writef32
const buffer_writeu32 = buffer.writeu32
const buffer_len = buffer.len
const buffer_readbits = buffer.readbits
const buffer_readf32 = buffer.readf32
const buffer_readu32 = buffer.readu32
const buffer_copy = buffer.copy
type Waypoint = {
  read frame: CFrame,
}
const function Waypoint_serialize_binary(value: Waypoint): Loom.Serialized
  const frame_value = value.frame
  local frame_sentinel = 0
  if frame_value == CFrame.identity then
    frame_sentinel = 1
  end
  const b = buffer_create(1 + (if frame_sentinel == 0 then 16 else 0))
  buffer_writebits(b, 0, 1, frame_sentinel)
  local offset = 1
  if frame_sentinel == 0 then
    buffer_writef32(b, offset, frame_value.X)
    offset += 4
    buffer_writef32(b, offset, frame_value.Y)
    offset += 4
    buffer_writef32(b, offset, frame_value.Z)
    offset += 4
    buffer_writeu32(b, offset, Loom.pack_quaternion(Loom.cframe_to_quaternion(frame_value)))
    offset += 4
  end
  return { buffer = b }
end
const function Waypoint_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Waypoint, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const frame_sentinel = buffer_readbits(b, 0, 1)
  local frame = nil
  local offset = 1
  if frame_sentinel == 0 then
    if buffer_len(b) < offset + 16 then
      return { ok = false, error = { kind = "truncated", field = "frame" } }
    end
    const read_0 = buffer_readf32(b, offset)
    offset += 4
    const read_1 = buffer_readf32(b, offset)
    offset += 4
    const read_2 = buffer_readf32(b, offset)
    offset += 4
    const read_3 = buffer_readu32(b, offset)
    offset += 4
    frame = CFrame.new(read_0, read_1, read_2, Loom.unpack_quaternion(read_3))
  elseif frame_sentinel == 1 then
    frame = CFrame.identity
  else
    return { ok = false, error = { kind = "invalid_tag", field = "frame" } }
  end
  return { ok = true, value = { frame = frame } }
end
const function Waypoint_diff_binary_write(baseline: Waypoint, value: Waypoint, b: buffer)
  local offset = 1
  const frame_changed = not Loom.deep_equal(baseline.frame, value.frame)
  buffer_writebits(b, 0, 1, if frame_changed then 1 else 0)
  if frame_changed then
    buffer_writebits(b, 1, 1, frame_sentinel)
    if frame_sentinel == 0 then
      buffer_writef32(b, offset, frame_value.X)
      offset += 4
      buffer_writef32(b, offset, frame_value.Y)
      offset += 4
      buffer_writef32(b, offset, frame_value.Z)
      offset += 4
      buffer_writeu32(b, offset, Loom.pack_quaternion(Loom.cframe_to_quaternion(frame_value)))
      offset += 4
    end
  end
  return offset
end
const function Waypoint_diff_binary_attempt(baseline: Waypoint, value: Waypoint, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Waypoint_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Waypoint_diff_binary_attempt(baseline, value, size * 2)
end
const function Waypoint_diff_binary(baseline: Waypoint, value: Waypoint): Loom.Serialized
  const baseline_s = Waypoint_serialize_binary(baseline)
  const value_s = Waypoint_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written = Waypoint_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Waypoint_apply_diff_binary_read(baseline: Waypoint, diff: Loom.Serialized): Loom.Result<Waypoint, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 1
  const frame_changed = buffer_readbits(b, 0, 1) == 1
  local frame = nil
  if frame_changed then
    const frame_2_sentinel = buffer_readbits(b, 1, 1)
    local frame_2 = nil
    if frame_2_sentinel == 0 then
      if buffer_len(b) < offset + 16 then
        return { ok = false, error = { kind = "truncated", field = "frame" } }
      end
      const read_0 = buffer_readf32(b, offset)
      offset += 4
      const read_1 = buffer_readf32(b, offset)
      offset += 4
      const read_2 = buffer_readf32(b, offset)
      offset += 4
      const read_3 = buffer_readu32(b, offset)
      offset += 4
      frame_2 = CFrame.new(read_0, read_1, read_2, Loom.unpack_quaternion(read_3))
    elseif frame_2_sentinel == 1 then
      frame_2 = CFrame.identity
    else
      return { ok = false, error = { kind = "invalid_tag", field = "frame" } }
    end
    frame = frame_2
  else
    frame = baseline.frame
  end
  return { ok = true, value = { frame = frame } }
end
const function Waypoint_apply_diff_binary(baseline: Waypoint, diff: Loom.Serialized): Loom.Result<Waypoint, Loom.DeserializeError>
  const ok, result = pcall(Waypoint_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Waypoint_serializer: Loom.Serializer<Waypoint> = { serialize = Waypoint_serialize_binary, deserialize = Waypoint_deserialize_binary, diff = Waypoint_diff_binary, applyDiff = Waypoint_apply_diff_binary }