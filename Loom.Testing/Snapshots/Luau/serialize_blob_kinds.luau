const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_writebits = buffer.writebits
const buffer_create = buffer.create
const buffer_copy = buffer.copy
const buffer_len = buffer.len
const buffer_readbits = buffer.readbits
type Payload = {
  read origin: Part,
  read handler: (number) -> (),
  read extra: unknown,
  read target: Instance,
}
const function Payload_serialize_binary(value: Payload): Loom.Serialized
  const blobs = {}
  table.insert(blobs, value.origin)
  table.insert(blobs, value.handler)
  table.insert(blobs, value.extra)
  table.insert(blobs, value.target)
  return { blobs = blobs }
end
const function Payload_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Payload, Loom.DeserializeError>
  const blobs = serialized.blobs
  if blobs == nil then
    return { ok = false, error = { kind = "missing_blob" } }
  end
  local blob_index = 1
  const origin_blob = blobs[blob_index]
  blob_index += 1
  if origin_blob == nil then
    return { ok = false, error = { kind = "missing_blob", field = "origin" } }
  end
  if typeof(origin_blob) ~= "Instance" or not origin_blob:IsA("Part") then
    return { ok = false, error = { kind = "invalid_blob", field = "origin" } }
  end
  const handler_blob = blobs[blob_index]
  blob_index += 1
  if handler_blob == nil then
    return { ok = false, error = { kind = "missing_blob", field = "handler" } }
  end
  if typeof(handler_blob) ~= "function" then
    return { ok = false, error = { kind = "invalid_blob", field = "handler" } }
  end
  const extra_blob = blobs[blob_index]
  blob_index += 1
  if extra_blob == nil then
    return { ok = false, error = { kind = "missing_blob", field = "extra" } }
  end
  const target_blob = blobs[blob_index]
  blob_index += 1
  if target_blob == nil then
    return { ok = false, error = { kind = "missing_blob", field = "target" } }
  end
  if typeof(target_blob) ~= "Instance" then
    return { ok = false, error = { kind = "invalid_blob", field = "target" } }
  end
  return { ok = true, value = { origin = origin_blob, handler = handler_blob, extra = extra_blob, target = target_blob } }
end
const function Payload_diff_binary_write(baseline: Payload, value: Payload, b: buffer)
  const blobs = {}
  local offset = 1
  const origin_changed = not Loom.deep_equal(baseline.origin, value.origin)
  buffer_writebits(b, 0, 1, if origin_changed then 1 else 0)
  if origin_changed then
    table.insert(blobs, value.origin)
  end
  const handler_changed = not Loom.deep_equal(baseline.handler, value.handler)
  buffer_writebits(b, 1, 1, if handler_changed then 1 else 0)
  if handler_changed then
    table.insert(blobs, value.handler)
  end
  const extra_changed = not Loom.deep_equal(baseline.extra, value.extra)
  buffer_writebits(b, 2, 1, if extra_changed then 1 else 0)
  if extra_changed then
    table.insert(blobs, value.extra)
  end
  const target_changed = not Loom.deep_equal(baseline.target, value.target)
  buffer_writebits(b, 3, 1, if target_changed then 1 else 0)
  if target_changed then
    table.insert(blobs, value.target)
  end
  return offset, blobs
end
const function Payload_diff_binary_attempt(baseline: Payload, value: Payload, size: number)
  const scratch = buffer_create(size)
  const ok, written, blobs = pcall(Payload_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written, blobs
  end
  return Payload_diff_binary_attempt(baseline, value, size * 2)
end
const function Payload_diff_binary(baseline: Payload, value: Payload): Loom.Serialized
  const baseline_s = Payload_serialize_binary(baseline)
  const value_s = Payload_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written, blobs = Payload_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result, blobs = blobs }
end
const function Payload_apply_diff_binary_read(baseline: Payload, diff: Loom.Serialized): Loom.Result<Payload, Loom.DeserializeError>
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
  const origin_changed = buffer_readbits(b, 0, 1) == 1
  local origin = nil
  if origin_changed then
    const origin_blob = blobs[blob_index]
    blob_index += 1
    if origin_blob == nil then
      return { ok = false, error = { kind = "missing_blob", field = "origin" } }
    end
    if typeof(origin_blob) ~= "Instance" or not origin_blob:IsA("Part") then
      return { ok = false, error = { kind = "invalid_blob", field = "origin" } }
    end
    origin = origin_blob
  else
    origin = baseline.origin
  end
  const handler_changed = buffer_readbits(b, 1, 1) == 1
  local handler = nil
  if handler_changed then
    const handler_blob = blobs[blob_index]
    blob_index += 1
    if handler_blob == nil then
      return { ok = false, error = { kind = "missing_blob", field = "handler" } }
    end
    if typeof(handler_blob) ~= "function" then
      return { ok = false, error = { kind = "invalid_blob", field = "handler" } }
    end
    handler = handler_blob
  else
    handler = baseline.handler
  end
  const extra_changed = buffer_readbits(b, 2, 1) == 1
  local extra = nil
  if extra_changed then
    const extra_blob = blobs[blob_index]
    blob_index += 1
    if extra_blob == nil then
      return { ok = false, error = { kind = "missing_blob", field = "extra" } }
    end
    extra = extra_blob
  else
    extra = baseline.extra
  end
  const target_changed = buffer_readbits(b, 3, 1) == 1
  local target = nil
  if target_changed then
    const target_blob = blobs[blob_index]
    blob_index += 1
    if target_blob == nil then
      return { ok = false, error = { kind = "missing_blob", field = "target" } }
    end
    if typeof(target_blob) ~= "Instance" then
      return { ok = false, error = { kind = "invalid_blob", field = "target" } }
    end
    target = target_blob
  else
    target = baseline.target
  end
  return { ok = true, value = { origin = origin, handler = handler, extra = extra, target = target } }
end
const function Payload_apply_diff_binary(baseline: Payload, diff: Loom.Serialized): Loom.Result<Payload, Loom.DeserializeError>
  const ok, result = pcall(Payload_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Payload_serializer: Loom.Serializer<Payload> = { serialize = Payload_serialize_binary, deserialize = Payload_deserialize_binary, diff = Payload_diff_binary, applyDiff = Payload_apply_diff_binary }