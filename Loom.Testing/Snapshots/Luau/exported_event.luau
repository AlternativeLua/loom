const Loom = require("@game/ReplicatedStorage/include/loom_runtime")
const _message_connections = {}
const message: Loom.Event<string> = Loom.Event.new()
const function handler(text: string): ()
  print(text)
end
_message_connections[handler] = message:Connect(handler)
message:Fire("hello")
Loom.disconnect_event(_message_connections, handler)
return { message = message, _message_connections = _message_connections }