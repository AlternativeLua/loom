--!strict
--!native
--!optimize 2
const Event = require("./event")
const Loom = {}

export type function NonNullable(ty)
	return types.intersectionof(ty, types.negationof(types.singleton(nil)))
end

export type CreatableInstance = Instance
export type ServiceInstance = Instance
export type Range = { minimum: number, maximum: number }
export type ResultOk<T> = { ok: true, value: T }
export type ResultError<Error> = { ok: false, error: Error }
export type Result<T, Error> = ResultOk<T> | ResultError<Error>
export type DeserializeErrorKind = "truncated" | "invalid_tag" | "invalid_length" | "missing_blob" | "invalid_blob"
export type Serialized = { buffer: buffer?, blobs: { unknown }? }
export type DeserializeError = { kind: DeserializeErrorKind, field: string?, offset: number? }
export type Event<T... = ...any> = Event.Event<T...>
export type UserEvent<T... = ...any> = Event.UserEvent<T...>
export type EventConnection = Event.Connection

Loom.Event = Event

function Loom.disconnect_event(connections: { [any]: Event.Connection }, fn: any)
	const connection = connections[fn]
	if connection == nil then
		return
	end

	connections[fn] = nil
	connection:Disconnect()
end

function Loom.merge_meta(...: {})
	const parents = { ... }
	const mt = {}
	mt.__index = function(_self, key)
		for _, parent in parents do
			local value = parent[key]
			if value ~= nil then
				return value
			end
		end
		return nil
	end

	return mt
end

type Task<A... = ...any> = {
	[number]: unknown;
	duration: number,
	elapsed: number,
	condition: (() -> boolean)?,
	body: (A...) -> (),
}

const tasks: { Task } = {}
local heartbeatConn: RBXScriptConnection?

const function stopIfEmpty(): ()
	if #tasks == 0 and heartbeatConn then
		heartbeatConn:Disconnect()
		heartbeatConn = nil
	end
end

const function start(): ()
	if heartbeatConn then
		return
	end

	heartbeatConn = game:GetService("RunService").Heartbeat:Connect(function(dt)
		local i = 1

		while i <= #tasks do
			local task = tasks[i]
			if task.condition and not task.condition() then
				table.remove(tasks, i)
				continue
			end

			task.elapsed += dt
			while task.elapsed >= task.duration do
				task.elapsed -= task.duration
				task.body(table.unpack(task)) -- args stored as array in task
			end

			i += 1
		end

		stopIfEmpty()
	end)
end

function Loom.every<A...>(
	duration: number,
	condition: (() -> boolean)?,
	body: (A...) -> (),
	...: A...
): ()
	table.insert(tasks, {
		duration = duration,
		elapsed = 0,
		body = body,
		condition = condition,
		...
	})

	start()
end

table.freeze(Loom)
return Loom