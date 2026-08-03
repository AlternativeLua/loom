const collection_service = game:GetService("CollectionService")
for _, obj in collection_service:GetTagged("Lava") do
  if not (typeof(obj) == "Instance" and obj:IsA("BasePart")) then continue end
  obj.Touched:Connect(function(hit)
    return if if hit.parent ~= nil then hit.parent:FindFirstChildOfClass("Humanoid") else nil ~= nil then (if hit.parent ~= nil then hit.parent:FindFirstChildOfClass("Humanoid") else nil):TakeDamage(100) else nil
  end)
end