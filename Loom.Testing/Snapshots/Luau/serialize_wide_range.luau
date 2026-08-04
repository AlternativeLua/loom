const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writebits = buffer.writebits
const buffer_len = buffer.len
const buffer_readbits = buffer.readbits
const buffer_copy = buffer.copy
type Counter = {
  read total: number,
}
const function Counter_serialize_binary(value: Counter): Loom.Serialized
  const b = buffer_create(4)
  buffer_writebits(b, 0, 32, math.round(value.total))
  return { buffer = b }
end
const function Counter_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Counter, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 4 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  return { ok = true, value = { total = buffer_readbits(b, 0, 32) } }
end
const function Counter_diff_binary_write(baseline: Counter, value: Counter, b: buffer)
  local offset = 5
  const total_changed = not Loom.deep_equal(baseline.total, value.total)
  buffer_writebits(b, 0, 1, if total_changed then 1 else 0)
  if total_changed then
    buffer_writebits(b, 1, 32, math.round(value.total))
  end
  return offset
end
const function Counter_diff_binary_attempt(baseline: Counter, value: Counter, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Counter_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Counter_diff_binary_attempt(baseline, value, size * 2)
end
const function Counter_diff_binary(baseline: Counter, value: Counter): Loom.Serialized
  const baseline_s = Counter_serialize_binary(baseline)
  const value_s = Counter_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 5
  const scratch, written = Counter_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Counter_apply_diff_binary_read(baseline: Counter, diff: Loom.Serialized): Loom.Result<Counter, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 5 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 5
  const total_changed = buffer_readbits(b, 0, 1) == 1
  local total = nil
  if total_changed then
    total = buffer_readbits(b, 1, 32)
  else
    total = baseline.total
  end
  return { ok = true, value = { total = total } }
end
const function Counter_apply_diff_binary(baseline: Counter, diff: Loom.Serialized): Loom.Result<Counter, Loom.DeserializeError>
  const ok, result = pcall(Counter_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Counter_serializer: Loom.Serializer<Counter> = { serialize = Counter_serialize_binary, deserialize = Counter_deserialize_binary, diff = Counter_diff_binary, applyDiff = Counter_apply_diff_binary }