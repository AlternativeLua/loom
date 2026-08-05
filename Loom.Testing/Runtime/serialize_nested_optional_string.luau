local cases = {
  { profile = { level = 3, nickname = nil }, tag = nil },
  { profile = { level = 200, nickname = "nick" }, tag = "t" },
  { profile = { level = 0, nickname = "" }, tag = "" },
}
for _, want in cases do
  local p = Account_serialize_binary(want)
  local r = Account_deserialize_binary(p)
  assert(r.ok, "ok")
  assert(type(r.value.profile) == "table", "nested struct must be a table, not flattened")
  assert(r.value.profile.level == want.profile.level, "nested number")
  assert(r.value.profile.nickname == want.profile.nickname, "nested optional string")
  assert(r.value.tag == want.tag, "outer optional string")
end
local solo = Profile_serialize_binary({ level = 1, nickname = "abc" })
assert(Profile_deserialize_binary(solo).value.nickname == "abc", "optional string alone")
