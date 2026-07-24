const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
local _eo_consumer_connections = {}
type EventObject = {
  read ConsumerEvent: Loom.Event<string>,
}
const function on_consumer(p: string): ()
  print("param: " .. p)
end
const eo = (nil :: never) :: EventObject
_eo_consumer_connections[on_consumer] = eo.ConsumerEvent:Connect(on_consumer)
eo.ConsumerEvent:Fire("abc")
_eo_consumer_connections[on_consumer]:Disconnect()
const my_event: Loom.Event<string> = Loom.Event.new()