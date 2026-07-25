const math = require("@game/ServerScriptService/Loom/math")
const square = math.square
const PI = math.pi
type Scalar = math.Scalar
const total: Scalar = square(PI)
print(total)
