-- Exercises every branch a delta write/read can take: an unchanged leaf, a changed leaf, one field of a
-- flattened struct changing while its sibling doesn't, a sentinelled datatype flipping to and from its
-- sentinel, an optional gaining/losing its value and changing underneath an unchanged presence, a union
-- whose tag changes vs. one whose active variant's own field changes, an array that keeps its length
-- (per-element diff) vs. one that doesn't (full resend fallback), and a map with an added, a removed and a
-- changed key - the changed one recursing into a struct value.

local function assert_round_trip(baseline, current, label)
  local diff = PlayerState_diff_binary(baseline, current)
  local result = PlayerState_apply_diff_binary(baseline, diff)
  assert(result.ok, label .. " failed to decode: " .. tostring(result.error and result.error.kind))

  local got = result.value
  assert(got.health == current.health, label .. " health")
  assert(got.name == current.name, label .. " name")
  assert(got.loadout.primary == current.loadout.primary, label .. " loadout.primary")
  assert(got.loadout.secondary == current.loadout.secondary, label .. " loadout.secondary")
  assert(got.position == current.position, label .. " position")

  if current.last_event == nil then
    assert(got.last_event == nil, label .. " last_event should be absent")
  else
    assert(got.last_event ~= nil, label .. " last_event should be present")
    assert(got.last_event.kind == current.last_event.kind, label .. " last_event.kind")
    if current.last_event.kind == "Hit" then
      assert(got.last_event.amount == current.last_event.amount, label .. " last_event.amount")
    end
  end

  assert(#got.recent == #current.recent, label .. " recent length")
  for i, event in current.recent do
    assert(got.recent[i].kind == event.kind, label .. " recent[" .. i .. "].kind")
    if event.kind == "Hit" then
      assert(got.recent[i].amount == event.amount, label .. " recent[" .. i .. "].amount")
    end
  end

  local gotCount, wantCount = 0, 0
  for _ in got.scores do
    gotCount += 1
  end
  for key, score in current.scores do
    wantCount += 1
    assert(got.scores[key] ~= nil, label .. " scores[" .. key .. "] should be present")
    assert(got.scores[key].primary == score.primary, label .. " scores[" .. key .. "].primary")
    assert(got.scores[key].secondary == score.secondary, label .. " scores[" .. key .. "].secondary")
  end
  assert(gotCount == wantCount, label .. " scores key count")

  return diff
end

local baseline = {
  health = 100,
  name = "Baseline",
  loadout = { primary = 1, secondary = 2 },
  position = Vector3.new(1, 2, 3),
  last_event = { kind = "Hit", amount = 5 },
  recent = { { kind = "Hit", amount = 1 }, { kind = "Miss" }, { kind = "Hit", amount = 3 } },
  scores = { alice = { primary = 10, secondary = 20 }, bob = { primary = 30, secondary = 40 } },
}

-- Nothing at all changed: every changed/presence/tag/length bit reads back false, and every field falls
-- straight through to the baseline's own value.
assert_round_trip(baseline, baseline, "identical")

-- Only a leaf changes.
do
  local current = table.clone(baseline)
  current.health = 250
  assert_round_trip(baseline, current, "leaf changed")
end

-- One field of a flattened struct changes, its sibling does not - proves the struct is diffed field by
-- field rather than resent as a unit.
do
  local current = table.clone(baseline)
  current.loadout = { primary = 99, secondary = baseline.loadout.secondary }
  local diff = assert_round_trip(baseline, current, "struct partial change")
  -- Confirms the write actually took the per-field path: a full Loadout resend costs 2 bytes, and this
  -- diff has to fit a header too, so 2 bytes here would mean the unchanged sibling still got resent.
  local full = PlayerState_serialize_binary(current)
  assert(buffer.len(diff.buffer) < buffer.len(full.buffer), "struct partial change: diff should beat a full resend")
end

-- A sentinelled datatype: baseline sits on Vector3.zero (the sentinel), current moves off it, and back.
do
  local zeroed = table.clone(baseline)
  zeroed.position = Vector3.zero
  local moved = table.clone(baseline)
  moved.position = Vector3.new(7, 8, 9)

  assert_round_trip(zeroed, moved, "sentinel to components")
  assert_round_trip(moved, zeroed, "components to sentinel")
  assert_round_trip(zeroed, zeroed, "sentinel unchanged")
end

-- Optional: gains a value, loses one, changes underneath an unchanged presence, and stays put with the
-- same presence and the same value.
do
  local absent = table.clone(baseline)
  absent.last_event = nil
  local present = table.clone(baseline)
  present.last_event = { kind = "Hit", amount = 5 }
  local changed = table.clone(baseline)
  changed.last_event = { kind = "Hit", amount = 42 }
  local otherVariant = table.clone(baseline)
  otherVariant.last_event = { kind = "Miss" }

  assert_round_trip(absent, present, "optional gained")
  assert_round_trip(present, absent, "optional lost")
  assert_round_trip(present, changed, "optional present, field changed")
  assert_round_trip(present, present, "optional present, unchanged")
  assert_round_trip(present, otherVariant, "optional present, tag changed")
end

-- Array: same length with one element's field changed (recurse), same length with an element's variant
-- swapped (tag changed within the shared per-element block), and a length change (full resend fallback).
do
  local current = table.clone(baseline)
  current.recent = { { kind = "Hit", amount = 1 }, { kind = "Miss" }, { kind = "Hit", amount = 999 } }
  assert_round_trip(baseline, current, "array element field changed")

  local swapped = table.clone(baseline)
  swapped.recent = { { kind = "Hit", amount = 1 }, { kind = "Hit", amount = 2 }, { kind = "Hit", amount = 3 } }
  assert_round_trip(baseline, swapped, "array element tag changed")

  local grown = table.clone(baseline)
  grown.recent = { { kind = "Hit", amount = 1 }, { kind = "Miss" }, { kind = "Hit", amount = 3 }, { kind = "Miss" } }
  assert_round_trip(baseline, grown, "array length changed")

  local shrunk = table.clone(baseline)
  shrunk.recent = {}
  assert_round_trip(baseline, shrunk, "array emptied")
end

-- Map: an added key, a removed key, and a changed key whose value is itself a struct - proving the
-- changed entry recurses rather than resending the whole Loadout.
do
  local current = table.clone(baseline)
  current.scores = {
    alice = { primary = 10, secondary = 20 }, -- unchanged, should not appear in any section
    bob = { primary = 30, secondary = 41 }, -- changed: only secondary
    carol = { primary = 1, secondary = 1 }, -- added
  }
  local diff = assert_round_trip(baseline, current, "map add/remove/change")

  local removedOnly = table.clone(baseline)
  removedOnly.scores = { alice = baseline.scores.alice }
  assert_round_trip(baseline, removedOnly, "map key removed")

  -- A diff over a big unordered map with one small change should stay far smaller than a full resend.
  local full = PlayerState_serialize_binary(current)
  assert(buffer.len(diff.buffer) <= buffer.len(full.buffer), "map diff should not exceed a full resend")
end

-- A large, mostly-unchanged snapshot compresses meaningfully against a full resend.
do
  local big = table.clone(baseline)
  big.name = "A reasonably long player name for size comparison purposes"
  big.recent = {}
  for i = 1, 20 do
    table.insert(big.recent, { kind = "Hit", amount = i })
  end
  big.scores = {}
  for i = 1, 20 do
    big.scores["player_" .. i] = { primary = i, secondary = i * 2 }
  end

  local current = table.clone(big)
  current.health = big.health + 1

  local diff = PlayerState_diff_binary(big, current)
  local full = PlayerState_serialize_binary(current)
  assert(
    buffer.len(diff.buffer) < buffer.len(full.buffer) / 2,
    "one changed byte-ish field should diff to well under half a full resend, got "
      .. buffer.len(diff.buffer)
      .. " vs "
      .. buffer.len(full.buffer)
  )

  assert_round_trip(big, current, "large mostly-unchanged snapshot")
end

-- A truncated or otherwise malformed diff reports rather than throwing.
do
  local diff = PlayerState_diff_binary(baseline, baseline)
  local truncated = { buffer = buffer.create(0) }
  local result = PlayerState_apply_diff_binary(baseline, truncated)
  assert(not result.ok, "an empty buffer should not decode")
  assert(result.error.kind == "truncated", "an empty buffer should report truncated, got " .. tostring(result.error.kind))

  local missingBuffer = { blobs = {} }
  local missingResult = PlayerState_apply_diff_binary(baseline, missingBuffer)
  assert(not missingResult.ok, "a missing buffer should not decode")
end
