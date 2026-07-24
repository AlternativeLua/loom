const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
local _eo_consumer_connections = {}
type EventObject = {
  read consumer: Loom.Event<string>,
}
const function on_consumer(p: string): ()
  print("param: " .. p)
end
const eo = (nil :: never) :: EventObject
_eo_consumer_connections[on_consumer] = eo.consumer:Connect(on_consumer)
eo.consumer:Fire("abc")
_eo_consumer_connections[on_consumer]:Disconnect()