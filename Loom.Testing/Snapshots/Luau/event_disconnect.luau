const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
local _abc_connections = {}
const abc: Loom.Event = Loom.Event.new()
const function handler(): ()
  print("handled")
end
_abc_connections[handler] = abc:Connect(handler)
const my_conn = _abc_connections[handler]
my_conn:Disconnect()
_abc_connections[handler] = abc:Connect(handler)
_abc_connections[handler]:Disconnect()