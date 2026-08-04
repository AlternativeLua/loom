const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const buffer_create = buffer.create
const buffer_writeu8 = buffer.writeu8
const buffer_writebits = buffer.writebits
const buffer_writeu32 = buffer.writeu32
const buffer_writestring = buffer.writestring
const buffer_len = buffer.len
const buffer_readu8 = buffer.readu8
const buffer_readbits = buffer.readbits
const buffer_readu32 = buffer.readu32
const buffer_readstring = buffer.readstring
const buffer_copy = buffer.copy
type Profile = {
  read level: number,
  read nickname: string?,
}
const function Profile_serialize_binary(value: Profile): Loom.Serialized
  local size = 2
  if value.nickname ~= nil then
    size += 4 + #value.nickname
  end
  const b = buffer_create(size)
  buffer_writeu8(b, 1, value.level)
  buffer_writebits(b, 0, 1, if value.nickname ~= nil then 1 else 0)
  local offset = 2
  if value.nickname ~= nil then
    const nickname_value = value.nickname
    const nickname_length = #nickname_value
    buffer_writeu32(b, offset, nickname_length)
    offset += 4
    buffer_writestring(b, offset, nickname_value)
    offset += nickname_length
  end
  return { buffer = b }
end
const function Profile_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Profile, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 2 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const nickname_present = buffer_readbits(b, 0, 1) == 1
  local nickname = nil
  local offset = 2
  if nickname_present then
    const read_0 = buffer_readu32(b, offset)
    offset += 4
    const nickname_2_length = read_0
    if buffer_len(b) < offset + nickname_2_length then
      return { ok = false, error = { kind = "invalid_length", field = "nickname" } }
    end
    const nickname_2 = buffer_readstring(b, offset, nickname_2_length)
    offset += nickname_2_length
    nickname = nickname_2
  end
  return { ok = true, value = { level = buffer_readu8(b, 1), nickname = nickname } }
end
const function Profile_diff_binary_write(baseline: Profile, value: Profile, b: buffer)
  local offset = 1
  const level_changed = not Loom.deep_equal(baseline.level, value.level)
  buffer_writebits(b, 0, 1, if level_changed then 1 else 0)
  if level_changed then
    buffer_writeu8(b, offset, value.level)
    offset += 1
  end
  const nickname_presence_changed = (baseline.nickname ~= nil) ~= (value.nickname ~= nil)
  buffer_writebits(b, 1, 1, if nickname_presence_changed then 1 else 0)
  if nickname_presence_changed then
    buffer_writebits(b, 2, 1, if value.nickname ~= nil then 1 else 0)
    if value.nickname ~= nil then
      const nickname_value = value.nickname
      const nickname_length = #nickname_value
      buffer_writeu32(b, offset, nickname_length)
      offset += 4
      buffer_writestring(b, offset, nickname_value)
      offset += nickname_length
    end
  else
    if value.nickname ~= nil then
      const nickname_changed = not Loom.deep_equal(baseline.nickname, value.nickname)
      buffer_writebits(b, 2, 1, if nickname_changed then 1 else 0)
      if nickname_changed then
        const nickname_value_2 = value.nickname
        const nickname_length_2 = #nickname_value_2
        buffer_writeu32(b, offset, nickname_length_2)
        offset += 4
        buffer_writestring(b, offset, nickname_value_2)
        offset += nickname_length_2
      end
    end
  end
  return offset
end
const function Profile_diff_binary_attempt(baseline: Profile, value: Profile, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Profile_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Profile_diff_binary_attempt(baseline, value, size * 2)
end
const function Profile_diff_binary(baseline: Profile, value: Profile): Loom.Serialized
  const baseline_s = Profile_serialize_binary(baseline)
  const value_s = Profile_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written = Profile_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Profile_apply_diff_binary_read(baseline: Profile, diff: Loom.Serialized): Loom.Result<Profile, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 1
  const level_changed = buffer_readbits(b, 0, 1) == 1
  local level = nil
  if level_changed then
    const read_0 = buffer_readu8(b, offset)
    offset += 1
    level = read_0
  else
    level = baseline.level
  end
  const nickname_presence_changed = buffer_readbits(b, 1, 1) == 1
  local nickname = nil
  if nickname_presence_changed then
    const nickname_2_present = buffer_readbits(b, 2, 1) == 1
    local nickname_2 = nil
    if nickname_2_present then
      const read_1 = buffer_readu32(b, offset)
      offset += 4
      const nickname_3_length = read_1
      if buffer_len(b) < offset + nickname_3_length then
        return { ok = false, error = { kind = "invalid_length", field = "nickname" } }
      end
      const nickname_3 = buffer_readstring(b, offset, nickname_3_length)
      offset += nickname_3_length
      nickname_2 = nickname_3
    end
    nickname = nickname_2
  else
    if baseline.nickname ~= nil then
      const nickname_changed = buffer_readbits(b, 3, 1) == 1
      local nickname_4 = nil
      if nickname_changed then
        const read_2 = buffer_readu32(b, offset)
        offset += 4
        const nickname_5_length = read_2
        if buffer_len(b) < offset + nickname_5_length then
          return { ok = false, error = { kind = "invalid_length", field = "nickname" } }
        end
        const nickname_5 = buffer_readstring(b, offset, nickname_5_length)
        offset += nickname_5_length
        nickname_4 = nickname_5
      else
        nickname_4 = baseline.nickname
      end
      nickname = nickname_4
    end
  end
  return { ok = true, value = { level = level, nickname = nickname } }
end
const function Profile_apply_diff_binary(baseline: Profile, diff: Loom.Serialized): Loom.Result<Profile, Loom.DeserializeError>
  const ok, result = pcall(Profile_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Profile_serializer: Loom.Serializer<Profile> = { serialize = Profile_serialize_binary, deserialize = Profile_deserialize_binary, diff = Profile_diff_binary, applyDiff = Profile_apply_diff_binary }
type Account = {
  read profile: Profile,
  read tag: string?,
}
const function Account_serialize_binary(value: Account): Loom.Serialized
  local size = 1
  size += 1
  if value.profile.nickname ~= nil then
    size += 4 + #value.profile.nickname
  end
  if value.tag ~= nil then
    size += 4 + #value.tag
  end
  const b = buffer_create(size)
  buffer_writeu8(b, 1, value.profile.level)
  buffer_writebits(b, 0, 1, if value.profile.nickname ~= nil then 1 else 0)
  local offset = 2
  if value.profile.nickname ~= nil then
    const nickname_value = value.profile.nickname
    const nickname_length = #nickname_value
    buffer_writeu32(b, offset, nickname_length)
    offset += 4
    buffer_writestring(b, offset, nickname_value)
    offset += nickname_length
  end
  buffer_writebits(b, 1, 1, if value.tag ~= nil then 1 else 0)
  if value.tag ~= nil then
    const tag_value = value.tag
    const tag_length = #tag_value
    buffer_writeu32(b, offset, tag_length)
    offset += 4
    buffer_writestring(b, offset, tag_value)
    offset += tag_length
  end
  return { buffer = b }
end
const function Account_deserialize_binary(serialized: Loom.Serialized): Loom.Result<Account, Loom.DeserializeError>
  const b = serialized.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  const nickname_present = buffer_readbits(b, 0, 1) == 1
  local nickname = nil
  local offset = 2
  if nickname_present then
    const read_0 = buffer_readu32(b, offset)
    offset += 4
    const nickname_2_length = read_0
    if buffer_len(b) < offset + nickname_2_length then
      return { ok = false, error = { kind = "invalid_length", field = "profile.nickname" } }
    end
    const nickname_2 = buffer_readstring(b, offset, nickname_2_length)
    offset += nickname_2_length
    nickname = nickname_2
  end
  const tag_present = buffer_readbits(b, 1, 1) == 1
  local tag = nil
  if tag_present then
    const read_1 = buffer_readu32(b, offset)
    offset += 4
    const tag_2_length = read_1
    if buffer_len(b) < offset + tag_2_length then
      return { ok = false, error = { kind = "invalid_length", field = "tag" } }
    end
    const tag_2 = buffer_readstring(b, offset, tag_2_length)
    offset += tag_2_length
    tag = tag_2
  end
  return { ok = true, value = { profile = { level = buffer_readu8(b, 1), nickname = nickname }, tag = tag } }
end
const function Account_diff_binary_write(baseline: Account, value: Account, b: buffer)
  local offset = 1
  const level_changed = not Loom.deep_equal(baseline.profile.level, value.profile.level)
  buffer_writebits(b, 0, 1, if level_changed then 1 else 0)
  if level_changed then
    buffer_writeu8(b, offset, value.profile.level)
    offset += 1
  end
  const nickname_presence_changed = (baseline.profile.nickname ~= nil) ~= (value.profile.nickname ~= nil)
  buffer_writebits(b, 1, 1, if nickname_presence_changed then 1 else 0)
  if nickname_presence_changed then
    buffer_writebits(b, 2, 1, if value.profile.nickname ~= nil then 1 else 0)
    if value.profile.nickname ~= nil then
      const nickname_value = value.profile.nickname
      const nickname_length = #nickname_value
      buffer_writeu32(b, offset, nickname_length)
      offset += 4
      buffer_writestring(b, offset, nickname_value)
      offset += nickname_length
    end
  else
    if value.profile.nickname ~= nil then
      const nickname_changed = not Loom.deep_equal(baseline.profile.nickname, value.profile.nickname)
      buffer_writebits(b, 2, 1, if nickname_changed then 1 else 0)
      if nickname_changed then
        const nickname_value_2 = value.profile.nickname
        const nickname_length_2 = #nickname_value_2
        buffer_writeu32(b, offset, nickname_length_2)
        offset += 4
        buffer_writestring(b, offset, nickname_value_2)
        offset += nickname_length_2
      end
    end
  end
  const tag_presence_changed = (baseline.tag ~= nil) ~= (value.tag ~= nil)
  buffer_writebits(b, 3, 1, if tag_presence_changed then 1 else 0)
  if tag_presence_changed then
    buffer_writebits(b, 4, 1, if value.tag ~= nil then 1 else 0)
    if value.tag ~= nil then
      const tag_value = value.tag
      const tag_length = #tag_value
      buffer_writeu32(b, offset, tag_length)
      offset += 4
      buffer_writestring(b, offset, tag_value)
      offset += tag_length
    end
  else
    if value.tag ~= nil then
      const tag_changed = not Loom.deep_equal(baseline.tag, value.tag)
      buffer_writebits(b, 4, 1, if tag_changed then 1 else 0)
      if tag_changed then
        const tag_value_2 = value.tag
        const tag_length_2 = #tag_value_2
        buffer_writeu32(b, offset, tag_length_2)
        offset += 4
        buffer_writestring(b, offset, tag_value_2)
        offset += tag_length_2
      end
    end
  end
  return offset
end
const function Account_diff_binary_attempt(baseline: Account, value: Account, size: number)
  const scratch = buffer_create(size)
  const ok, written = pcall(Account_diff_binary_write, baseline, value, scratch)
  if ok then
    return scratch, written
  end
  return Account_diff_binary_attempt(baseline, value, size * 2)
end
const function Account_diff_binary(baseline: Account, value: Account): Loom.Serialized
  const baseline_s = Account_serialize_binary(baseline)
  const value_s = Account_serialize_binary(value)
  const size = (if baseline_s.buffer ~= nil then buffer.len(baseline_s.buffer) else 0) + (if value_s.buffer ~= nil then buffer.len(value_s.buffer) else 0) + 1
  const scratch, written = Account_diff_binary_attempt(baseline, value, size)
  const result = buffer_create(written)
  buffer_copy(result, 0, scratch, 0, written)
  return { buffer = result }
end
const function Account_apply_diff_binary_read(baseline: Account, diff: Loom.Serialized): Loom.Result<Account, Loom.DeserializeError>
  const b = diff.buffer
  if b == nil or buffer_len(b) < 1 then
    return { ok = false, error = { kind = "truncated", offset = 0 } }
  end
  local offset = 1
  const level_changed = buffer_readbits(b, 0, 1) == 1
  local level = nil
  if level_changed then
    const read_0 = buffer_readu8(b, offset)
    offset += 1
    level = read_0
  else
    level = baseline.profile.level
  end
  const nickname_presence_changed = buffer_readbits(b, 1, 1) == 1
  local nickname = nil
  if nickname_presence_changed then
    const nickname_2_present = buffer_readbits(b, 2, 1) == 1
    local nickname_2 = nil
    if nickname_2_present then
      const read_1 = buffer_readu32(b, offset)
      offset += 4
      const nickname_3_length = read_1
      if buffer_len(b) < offset + nickname_3_length then
        return { ok = false, error = { kind = "invalid_length", field = "profile.nickname" } }
      end
      const nickname_3 = buffer_readstring(b, offset, nickname_3_length)
      offset += nickname_3_length
      nickname_2 = nickname_3
    end
    nickname = nickname_2
  else
    if baseline.profile.nickname ~= nil then
      const nickname_changed = buffer_readbits(b, 3, 1) == 1
      local nickname_4 = nil
      if nickname_changed then
        const read_2 = buffer_readu32(b, offset)
        offset += 4
        const nickname_5_length = read_2
        if buffer_len(b) < offset + nickname_5_length then
          return { ok = false, error = { kind = "invalid_length", field = "profile.nickname" } }
        end
        const nickname_5 = buffer_readstring(b, offset, nickname_5_length)
        offset += nickname_5_length
        nickname_4 = nickname_5
      else
        nickname_4 = baseline.profile.nickname
      end
      nickname = nickname_4
    end
  end
  const tag_presence_changed = buffer_readbits(b, 4, 1) == 1
  local tag = nil
  if tag_presence_changed then
    const tag_2_present = buffer_readbits(b, 5, 1) == 1
    local tag_2 = nil
    if tag_2_present then
      const read_3 = buffer_readu32(b, offset)
      offset += 4
      const tag_3_length = read_3
      if buffer_len(b) < offset + tag_3_length then
        return { ok = false, error = { kind = "invalid_length", field = "tag" } }
      end
      const tag_3 = buffer_readstring(b, offset, tag_3_length)
      offset += tag_3_length
      tag_2 = tag_3
    end
    tag = tag_2
  else
    if baseline.tag ~= nil then
      const tag_changed = buffer_readbits(b, 6, 1) == 1
      local tag_4 = nil
      if tag_changed then
        const read_4 = buffer_readu32(b, offset)
        offset += 4
        const tag_5_length = read_4
        if buffer_len(b) < offset + tag_5_length then
          return { ok = false, error = { kind = "invalid_length", field = "tag" } }
        end
        const tag_5 = buffer_readstring(b, offset, tag_5_length)
        offset += tag_5_length
        tag_4 = tag_5
      else
        tag_4 = baseline.tag
      end
      tag = tag_4
    end
  end
  return { ok = true, value = { profile = { level = level, nickname = nickname }, tag = tag } }
end
const function Account_apply_diff_binary(baseline: Account, diff: Loom.Serialized): Loom.Result<Account, Loom.DeserializeError>
  const ok, result = pcall(Account_apply_diff_binary_read, baseline, diff)
  if ok then
    return result
  end
  return { ok = false, error = { kind = "truncated" } }
end
const Account_serializer: Loom.Serializer<Account> = { serialize = Account_serialize_binary, deserialize = Account_deserialize_binary, diff = Account_diff_binary, applyDiff = Account_apply_diff_binary }