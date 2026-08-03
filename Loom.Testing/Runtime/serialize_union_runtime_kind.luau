for _, want in { {content=1.5}, {content="hello"}, {content=""} } do
  local p = Cell_serialize_binary(want)
  local r = Cell_deserialize_binary(p)
  assert(r.ok, "ok")
  assert(typeof(r.value.content) == typeof(want.content), "kind preserved")
  if typeof(want.content) == "string" then
    assert(r.value.content == want.content, "string value")
  else
    assert(math.abs(r.value.content - want.content) < 0.001, "number value")
  end
end
