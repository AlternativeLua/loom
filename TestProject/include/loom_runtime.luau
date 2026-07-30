--!strict
--!native
--!optimize 2
const Event = require("./event")
const Loom = {}

export type CreatableInstance = Instance
export type ServiceInstance = Instance
export type Range = { minimum: number, maximum: number }
export type ResultOk<T> = { ok: true, value: T }
export type ResultError<Error> = { ok: false, error: Error }
export type Result<T, Error> = ResultOk<T> | ResultError<Error>
export type Event<T... = ...any> = Event.Event<T...>
export type UserEvent<T... = ...any> = Event.UserEvent<T...>
export type EventConnection = Event.Connection

Loom.Event = Event

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

table.freeze(Loom)
return Loom