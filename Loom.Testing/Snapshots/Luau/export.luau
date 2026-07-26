const constant = 69
const function do_something()
  return print("doing something")
end
export type Alias = number
export type Point = {
  read x: number,
  read y: number,
}
export type Direction = number
export type Drawable = {
  draw: (Drawable) -> (),
}
return { constant = constant, do_something = do_something }