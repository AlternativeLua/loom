const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_writebits = buffer.writebits
const buffer_create = buffer.create
const buffer_copy = buffer.copy
const buffer_len = buffer.len
const buffer_readbits = buffer.readbits
type Marker = {
  read kind: "spawn",
  read target: Part,
}
const function Marker_serialize_binary(value: Marker): Loom.Serialized
  const blobs = {}
  table.insert(blobs, value.target)
  return { blobs = blobs }
end
const function Marker_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Marker, Loom.DeserializeError>
  const blobs = serialized.blobs
  if blobs == nil then
    return { ok = false, error = { kind = "missing_blob" } }
  end
  local blob_index = 1
  const target_blob = blobs[blob_index]
  blob_index += 1
  if target_blob == nil then
    return { ok = false, error = { kind = "missing_blob", field = "target" } }
  end
  if typeof(target_blob) ~= "Instance" or not target_blob:IsA("Part") then
    return { ok = false, error = { kind = "invalid_blob", field = "target" } }
  end
  return { ok = true, value = { kind = "spawn", target = target_blob } }
end
const function Marker_diff_binary_write(baseline: Marker, value: Marker, b: buffer)
  const blobs = {}
  local offset = 1
  const target_changed = not Loom.deep_equal(baseline.target, value.target)
  buffer_writebits(b, 0, 1, if target_changed then 1 else 0)
  if target_changed then
    table.insert(blobs, value.target)
  end
  return offset, blobs
end
const function Marker_diff_binary_attempt(baseline: Marker, value: Marker, size: number)
  const scratch = buffer_create(size)
  const ok, written, blobs = pcall(Marker_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written, blobs
  end
  return Marker_diff_binary_attempt(baseline, value, size * 2)
end
const function Marker_diff_binary(baseline: Marker, value: Marker): Loom.Serialized
  const baseline_s = Marker_serialize_binary(baseline)
  const value_s = Marker_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written, blobs = Marker_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result, blobs = blobs }
end
const function Marker_apply_diff_binary_read(baseline: Marker, diff: Loom.Serialized): Loom.Result<Marker, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const blobs = diff.blobs
  if blobs == nil then
    return { ok = false, error = { kind = "missing_blob" } }
  end
  local blob_index = 1
  local offset = 1
  const target_changed = buffer_readbits(b, 0, 1) == 1
  local target = nil
  if target_changed then
    const target_blob = blobs[blob_index]
    blob_index += 1
    if target_blob == nil then
      return { ok = false, error = { kind = "missing_blob", field = "target" } }
    end
    if typeof(target_blob) ~= "Instance" or not target_blob:IsA("Part") then
      return { ok = false, error = { kind = "invalid_blob", field = "target" } }
    end
    target = target_blob
  else
    target = baseline.target
  end
  return { ok = true, value = { kind = "spawn", target = target } }
end
const function Marker_apply_diff_binary(baseline: Marker, diff: Loom.Serialized): Loom.Result<Marker, Loom.DeserializeError>
  const ok, result = pcall(Marker_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Marker_serializer: Loom.Serializer<Marker> = { serialize = Marker_serialize_binary, deserialize = Marker_deserialize_binary, diff = Marker_diff_binary, applyDiff = Marker_apply_diff_binary }