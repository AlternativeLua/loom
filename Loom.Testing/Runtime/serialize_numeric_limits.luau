-- Every width at both ends of its range. Truncation and sign errors only show at the boundary, and a
-- width written through the wrong buffer call reads back plausibly at small magnitudes.
local cases = {
  { i8 = -128, u8 = 0, i16 = -32768, u16 = 0, i32 = -2147483648, u32 = 0, f32 = 0, f64 = 0 },
  { i8 = 127, u8 = 255, i16 = 32767, u16 = 65535, i32 = 2147483647, u32 = 4294967295, f32 = 1, f64 = 1 },
  { i8 = -1, u8 = 128, i16 = -1, u16 = 32768, i32 = -1, u32 = 2147483648, f32 = -0.5, f64 = -0.5 },
}

for _, want in cases do
  local r = Limits_deserialize_binary(Limits_serialize_binary(want))
  assert(r.ok, "ok")
  for _, field in { "i8", "u8", "i16", "u16", "i32", "u32" } do
    assert(r.value[field] == want[field], field .. ": " .. tostring(r.value[field]) .. " ~= " .. tostring(want[field]))
  end
  assert(r.value.f32 == want.f32, "f32")
  assert(r.value.f64 == want.f64, "f64")
end

-- f64 keeps precision an f32 cannot; that is the whole reason to ask for it.
local precise = 1.2345678901234
local r = Limits_deserialize_binary(Limits_serialize_binary({ i8 = 0, u8 = 0, i16 = 0, u16 = 0, i32 = 0, u32 = 0, f32 = precise, f64 = precise }))
assert(r.value.f64 == precise, "f64 exact")
assert(r.value.f32 ~= precise, "f32 is expected to lose precision here")
assert(math.abs(r.value.f32 - precise) < 0.0001, "f32 still close")

-- 1 + 1 + 2 + 2 + 4 + 4 + 4 + 8
assert(buffer.len(Limits_serialize_binary(cases[1]).buffer) == 26, "fixed width")
