const collection_service = game:GetService("CollectionService")
for _, obj in collection_service:GetTagged("Lava") do
  if typeof(obj) == "Instance" and obj:IsA("BasePart") then
    obj = obj :: BasePart
    obj.Touched:Connect(function(hit: BasePart)
      return (if hit.parent ~= nil then hit.parent:FindFirstChildOfClass() else nil) :: never
    end)
  end
end
