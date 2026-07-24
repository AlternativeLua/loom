local _part_touched_connections = {}
const world = game:GetService("Workspace")
const part = world:FindFirstChildOfClass("Part")
const function on_touch(hit: BasePart): ()
  return print(hit)
end
if part ~= nil then
  _part_touched_connections[on_touch] = part.Touched:Connect(on_touch)
end