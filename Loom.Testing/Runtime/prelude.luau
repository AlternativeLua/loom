-- Stubs for the Roblox surface the emitted serializers touch, plus the Loom runtime helpers.
local realTypeof = typeof

local Vector3Meta = {}
Vector3Meta.__index = Vector3Meta
Vector3Meta.__eq = function(a, b) return a.X == b.X and a.Y == b.Y and a.Z == b.Z end
Vector3Meta.__tostring = function(v) return `Vector3({v.X}, {v.Y}, {v.Z})` end

Vector3 = {}
function Vector3.new(x, y, z)
	return setmetatable({ X = x or 0, Y = y or 0, Z = z or 0 }, Vector3Meta)
end
Vector3.zero = Vector3.new(0, 0, 0)
Vector3.one = Vector3.new(1, 1, 1)
Vector3.xAxis = Vector3.new(1, 0, 0)
Vector3.yAxis = Vector3.new(0, 1, 0)
Vector3.zAxis = Vector3.new(0, 0, 1)

-- Rotation is held as a quaternion so GetComponents can hand back a real matrix.
local CFrameMeta = {}
CFrameMeta.__index = CFrameMeta
CFrameMeta.__eq = function(a, b)
	return a.X == b.X and a.Y == b.Y and a.Z == b.Z
		and math.abs(a.qx * b.qx + a.qy * b.qy + a.qz * b.qz + a.qw * b.qw) > 0.9999
end
function CFrameMeta:GetComponents()
	local x, y, z, qx, qy, qz, qw = self.X, self.Y, self.Z, self.qx, self.qy, self.qz, self.qw
	return x, y, z,
		1 - 2 * (qy * qy + qz * qz), 2 * (qx * qy - qz * qw), 2 * (qx * qz + qy * qw),
		2 * (qx * qy + qz * qw), 1 - 2 * (qx * qx + qz * qz), 2 * (qy * qz - qx * qw),
		2 * (qx * qz - qy * qw), 2 * (qy * qz + qx * qw), 1 - 2 * (qx * qx + qy * qy)
end

CFrame = {}
function CFrame.new(x, y, z, qx, qy, qz, qw)
	if qx == nil then qx, qy, qz, qw = 0, 0, 0, 1 end
	local n = math.sqrt(qx * qx + qy * qy + qz * qz + qw * qw)
	if n == 0 then n = 1 end
	return setmetatable({ X = x or 0, Y = y or 0, Z = z or 0, qx = qx / n, qy = qy / n, qz = qz / n, qw = qw / n }, CFrameMeta)
end
CFrame.identity = CFrame.new(0, 0, 0, 0, 0, 0, 1)

-- Blob stand-ins have to answer typeof/IsA the way an Instance would.
local InstanceMeta = {}
InstanceMeta.__index = InstanceMeta
function InstanceMeta:IsA(className) return self.ClassName == className end
function MakeInstance(className)
	return setmetatable({ ClassName = className }, InstanceMeta)
end

typeof = function(value)
	local mt = realTypeof(value) == "table" and getmetatable(value) or nil
	if mt == InstanceMeta then return "Instance" end
	if mt == Vector3Meta then return "Vector3" end
	if mt == CFrameMeta then return "CFrame" end
	return realTypeof(value)
end

local bor, band, lshift, rshift = bit32.bor, bit32.band, bit32.lshift, bit32.rshift
local abs, max, clamp, round, insert = math.abs, math.max, math.clamp, math.round, table.insert

Loom = {}

function Loom.cframe_to_quaternion(cf)
	local _, _, _, r00, r01, r02, r10, r11, r12, r20, r21, r22 = cf:GetComponents()
	local trace = r00 + r11 + r22
	if trace > 0 then
		local s = (trace + 1) ^ 0.5 * 2
		return (r21 - r12) / s, (r02 - r20) / s, (r10 - r01) / s, 0.25 * s
	elseif r00 > r11 and r00 > r22 then
		local s = (1 + r00 - r11 - r22) ^ 0.5 * 2
		return 0.25 * s, (r01 + r10) / s, (r02 + r20) / s, (r21 - r12) / s
	elseif r11 > r22 then
		local s = (1 + r11 - r00 - r22) ^ 0.5 * 2
		return (r01 + r10) / s, 0.25 * s, (r12 + r21) / s, (r02 - r20) / s
	else
		local s = (1 + r22 - r00 - r11) ^ 0.5 * 2
		return (r02 + r20) / s, (r12 + r21) / s, 0.25 * s, (r10 - r01) / s
	end
end

local QUATERNION_SCALE = 2 ^ 0.5
local QUATERNION_STEPS = 1023

local function dequantize_component(value)
	return (value / QUATERNION_STEPS * 2 - 1) / QUATERNION_SCALE
end

function Loom.pack_quaternion(qx, qy, qz, qw)
	local ax, ay, az, aw = abs(qx), abs(qy), abs(qz), abs(qw)
	local largest, magnitude = 0, ax
	if ay > magnitude then largest, magnitude = 1, ay end
	if az > magnitude then largest, magnitude = 2, az end
	if aw > magnitude then largest, magnitude = 3, aw end

	local components = { qx, qy, qz, qw }
	if components[largest + 1] < 0 then
		components[1], components[2], components[3], components[4] = -qx, -qy, -qz, -qw
	end

	local packed = largest
	for index = 1, 4 do
		if index - 1 ~= largest then
			packed = bor(lshift(packed, 10), clamp(round((components[index] * QUATERNION_SCALE + 1) * 0.5 * QUATERNION_STEPS), 0, QUATERNION_STEPS))
		end
	end
	return packed
end

function Loom.unpack_quaternion(packed)
	local third = dequantize_component(band(packed, 0x3FF))
	local second = dequantize_component(band(rshift(packed, 10), 0x3FF))
	local first = dequantize_component(band(rshift(packed, 20), 0x3FF))
	local largest = band(rshift(packed, 30), 0x3)
	local missing = max(0, 1 - (first * first + second * second + third * third)) ^ 0.5
	local components = { first, second, third }
	insert(components, largest + 1, missing)
	return components[1], components[2], components[3], components[4]
end
