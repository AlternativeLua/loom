-- Blobs cost no buffer bytes and are matched by position, so several in one payload must come back in
-- the order they went out - and each is checked as far as its declared type allows.
local part = MakeInstance("Part")
local other = MakeInstance("Model")
local handler = function(n) return n end
local extra = { arbitrary = true }

local p = Payload_serialize_binary({ origin = part, handler = handler, extra = extra, target = other })
assert(p.buffer == nil, "every field is zero-width, so no buffer is sent")
assert(#p.blobs == 4, "four blobs")

local r = Payload_deserialize_binary(p)
assert(r.ok, "ok")
assert(r.value.origin == part, "first blob")
assert(r.value.handler == handler, "second blob")
assert(r.value.extra == extra, "third blob keeps an arbitrary value")
assert(r.value.target == other, "fourth blob")

-- A wrong class is caught; 'unknown' admits anything by definition, so it is not.
local wrongClass = Payload_deserialize_binary({ blobs = { other, handler, extra, other } })
assert(not wrongClass.ok and wrongClass.error.kind == "invalid_blob", "Part is checked by class")
assert(wrongClass.error.field == "origin", "reports the offending field")

local wrongKind = Payload_deserialize_binary({ blobs = { part, "not a function", extra, other } })
assert(not wrongKind.ok and wrongKind.error.kind == "invalid_blob", "function is checked by kind")

local short = Payload_deserialize_binary({ blobs = { part, handler } })
assert(not short.ok and short.error.kind == "missing_blob", "a short array reports rather than yielding nil")
