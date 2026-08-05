-- A payload with more field interactions than any focused case: sentinels, a packed header shared by a
-- ranged number and several tags, a flattened struct, an array of variants each carrying their own bits
-- and an optional blob, and a trailing optional blob of its own.
local model = MakeInstance("Model")
local attacker = MakeInstance("Player")

local cases = {
  {
    user_id = 0, display_name = "", health = 0, alive = false, team = 0,
    velocity = Vector3.zero, aim = CFrame.identity,
    loadout = { primary = 0, secondary = 0 },
    recent = {}, character = nil,
  },
  {
    user_id = 65535, display_name = "a very long display name indeed", health = 100, alive = true, team = 2,
    velocity = Vector3.new(-30, 12, 7), aim = CFrame.new(4, 5, 6, 0.2, 0.3, 0.4, 0.84),
    loadout = { primary = 12, secondary = 255 },
    recent = {
      { kind = "Damaged", amount = 750, attacker = attacker },
      { kind = "Respawned", at = Vector3.new(1, 2, 3) },
      { kind = "Damaged", amount = 0, attacker = nil },
    },
    character = model,
  },
}

for index, want in cases do
  local p = PlayerSnapshot_serialize_binary(want)
  local r = PlayerSnapshot_deserialize_binary(p)
  assert(r.ok, "case " .. index .. " failed to decode")

  local got = r.value
  assert(got.user_id == want.user_id, "user_id")
  assert(got.display_name == want.display_name, "display_name")
  assert(got.health == want.health, "health")
  assert(got.alive == want.alive, "alive")
  assert(got.team == want.team, "team")
  assert(got.velocity == want.velocity, "velocity")
  assert(got.aim == want.aim, "aim")
  assert(got.loadout.primary == want.loadout.primary, "loadout.primary")
  assert(got.loadout.secondary == want.loadout.secondary, "loadout.secondary")
  assert(got.character == want.character, "character")

  assert(#got.recent == #want.recent, "event count")
  for i, event in want.recent do
    assert(got.recent[i].kind == event.kind, "event kind " .. i)
    assert(got.recent[i].amount == event.amount, "event amount " .. i)
    assert(got.recent[i].attacker == event.attacker, "event attacker " .. i)
    if event.at ~= nil then
      assert(got.recent[i].at == event.at, "event position " .. i)
    end
  end
end

-- An idle player at rest: sentinels erase the velocity and the aim entirely, so what remains is two
-- header bytes (7 health + 1 alive + 2 team + 3 velocity + 1 aim + 1 character = 15 bits) plus the
-- user id, the name and array length prefixes, and the loadout.
local idle = PlayerSnapshot_serialize_binary(cases[1])
assert(buffer.len(idle.buffer) == 2 + 2 + 4 + 2 + 4, "idle snapshot is " .. buffer.len(idle.buffer) .. " bytes")
assert(idle.blobs ~= nil and #idle.blobs == 0, "no blobs when nothing is attached")
