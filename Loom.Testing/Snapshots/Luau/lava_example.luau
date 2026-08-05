const collection_service = game:GetService("CollectionService")
for _, obj in collection_service:GetTagged("Lava") do
  if not (typeof(obj) == "Instance" and obj:IsA("BasePart")) then continue end
  obj.Touched:Connect(function(hit)
    local _optionalResult
    const _optional = hit.parent
    if _optional ~= nil then
      _optionalResult = _optional:FindFirstChildOfClass("Humanoid")
    else
      _optionalResult = nil
    end
    local _optionalResult_1
    if _optionalResult ~= nil then
      _optionalResult_1 = _optionalResult:TakeDamage(100)
    else
      _optionalResult_1 = nil
    end
    return _optionalResult_1
  end)
end