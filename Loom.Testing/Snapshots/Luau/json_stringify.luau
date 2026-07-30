type JsonUser = {
  read name: string,
  read age: number,
  read tags: { string },
}
const function stringify(user: JsonUser): string
  return [[{
  ]] .. "  name: '" .. user.name .. [[',
  ]] .. "  age: " .. tostring(user.age) .. [[,
  ]] .. "  tags: [" .. table.concat(user.tags, ", ") .. [[]
  ]] .. "}"
end
const user = { name = "Alice", age = 20, tags = {"admin", "staff"} }
print(stringify(user))