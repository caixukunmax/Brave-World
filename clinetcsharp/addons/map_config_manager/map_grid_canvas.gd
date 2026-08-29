@tool
extends Control
## 地图单元编辑器的画布：按地形上色、按 footprint 画建筑、悬停预览、缩放平移。
## 数据与操作都委托给宿主 ed（MapEditorWindow）。

var ed = null  # MapEditorWindow


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_STOP
	size_flags_horizontal = Control.SIZE_EXPAND_FILL
	size_flags_vertical = Control.SIZE_EXPAND_FILL
	clip_contents = true


func _cell_at(pos: Vector2) -> Vector2i:
	var z: float = ed.zoom
	var o: Vector2 = ed.offset
	return Vector2i(int(floor((pos.x - o.x) / z)), int(floor((pos.y - o.y) / z)))


func _gui_input(e: InputEvent) -> void:
	if ed == null:
		return
	if e is InputEventMouseButton:
		var mb := e as InputEventMouseButton
		var mpos := get_local_mouse_position()
		if mb.button_index == MOUSE_BUTTON_WHEEL_UP and mb.pressed:
			ed.zoom_at(mpos, 1.15)
		elif mb.button_index == MOUSE_BUTTON_WHEEL_DOWN and mb.pressed:
			ed.zoom_at(mpos, 1.0 / 1.15)
		elif mb.button_index == MOUSE_BUTTON_MIDDLE:
			ed.panning = mb.pressed
			ed.last_mouse = mpos
		elif mb.button_index == MOUSE_BUTTON_LEFT:
			if mb.pressed:
				ed.painting = true
				ed.apply_at(_cell_at(mpos))
			else:
				ed.painting = false
		queue_redraw()
	elif e is InputEventMouseMotion:
		var mm := e as InputEventMouseMotion
		ed.hover = _cell_at(mm.position)
		if ed.panning:
			ed.offset += mm.position - ed.last_mouse
			ed.last_mouse = mm.position
		elif ed.painting:
			ed.apply_at(ed.hover)
		queue_redraw()


func _draw() -> void:
	if ed == null:
		return
	var z: float = ed.zoom
	var o: Vector2 = ed.offset
	draw_rect(Rect2(Vector2.ZERO, size), Color(0.12, 0.12, 0.14))

	var w: int = ed.bounds_w
	var h: int = ed.bounds_h
	# 可见范围裁剪
	var x0: int = int(max(0, floor(-o.x / z)))
	var y0: int = int(max(0, floor(-o.y / z)))
	var x1: int = int(min(w, ceil((size.x - o.x) / z)))
	var y1: int = int(min(h, ceil((size.y - o.y) / z)))

	for y in range(y0, y1):
		for x in range(x0, x1):
			var key := "%d_%d" % [x, y]
			var cell: Variant = ed.cells.get(key, null)
			var col: Color
			if cell == null:
				col = ed.outside_color()
			else:
				col = ed.terrain_color(int(cell.get("terrain", 0)))
			draw_rect(Rect2(o + Vector2(x * z, y * z), Vector2(z, z)), col)

	# 网格线
	if z >= 8.0:
		var gc := Color(0.7, 0.7, 0.7, 0.22)
		for x in range(x0, x1 + 1):
			var px := o.x + x * z
			draw_line(Vector2(px, o.y + y0 * z), Vector2(px, o.y + y1 * z), gc, 1.0)
		for y in range(y0, y1 + 1):
			var py := o.y + y * z
			draw_line(Vector2(o.x + x0 * z, py), Vector2(o.x + x1 * z, py), gc, 1.0)

	# 建筑（footprint，按游戏 AppearanceComponent 的外观渲染）
	for a in ed.anchors():
		var def: Variant = ed.building_by_id(int(a["id"]))
		if def == null:
			var fr0 := Rect2(o + Vector2(int(a["x"]) * z, int(a["y"]) * z), Vector2(int(a["sx"]) * z, int(a["sy"]) * z))
			draw_rect(fr0, Color(0.8, 0.55, 0.2, 0.35))
			draw_rect(fr0, Color(1.0, 0.8, 0.3, 0.9), false, 2.0)
			continue
		var fr := Rect2(o + Vector2(int(a["x"]) * z, int(a["y"]) * z), Vector2(int(a["sx"]) * z, int(a["sy"]) * z))
		var vs: float = float(def["vscale"])
		var inset := fr.size * (1.0 - vs) * 0.5
		var rr := Rect2(fr.position + inset, fr.size - inset * 2.0)
		var rad: float = min(float(def["corner"]), min(rr.size.x, rr.size.y) * 0.5)
		var pts := _rounded_rect_points(rr, rad)
		draw_colored_polygon(pts, def["bg"])
		var closed := pts.duplicate()
		closed.append(pts[0])
		draw_polyline(closed, def["border"], 2.0)
		if z >= 12.0:
			var nm := String(def["name"])
			var fs := int(clamp(z * 0.32, 8.0, 18.0))
			draw_string(ThemeDB.fallback_font, Vector2(rr.position.x, rr.position.y + fs), nm,
				HORIZONTAL_ALIGNMENT_CENTER, rr.size.x, fs, def["text"])

	# 悬停预览
	if ed.hover_within():
		var hx: int = ed.hover.x
		var hy: int = ed.hover.y
		if ed.tool == "building":
			var def: Variant = ed.building_by_id(ed.sel_building)
			var sx: int = int(def["sx"]) if def != null else 1
			var sy: int = int(def["sy"]) if def != null else 1
			var ok: bool = ed.can_place(hx, hy, sx, sy)
			var col := Color(0.2, 0.9, 0.3, 0.4) if ok else Color(0.9, 0.2, 0.2, 0.45)
			draw_rect(Rect2(o + Vector2(hx * z, hy * z), Vector2(sx * z, sy * z)), col)
		elif ed.tool == "erase_building":
			var key := "%d_%d" % [hx, hy]
			var akey: Variant = ed.occupied.get(key, key)
			draw_rect(Rect2(o + Vector2(hx * z, hy * z), Vector2(z, z)), Color(0.9, 0.2, 0.2, 0.4))
			if ed.occupied.has(key) and String(akey) != key:
				pass
		else:
			draw_rect(Rect2(o + Vector2(hx * z, hy * z), Vector2(z, z)), Color(1, 1, 1, 0.25))


## 生成圆角矩形的轮廓点（顺时针）。rad<=0.5 时退化为直角四点。
func _rounded_rect_points(r: Rect2, rad: float) -> PackedVector2Array:
	var pts := PackedVector2Array()
	if rad <= 0.5:
		pts.append(r.position)
		pts.append(Vector2(r.end.x, r.position.y))
		pts.append(r.end)
		pts.append(Vector2(r.position.x, r.end.y))
		return pts
	var seg := 4
	var tl := r.position + Vector2(rad, rad)
	var tr := Vector2(r.end.x - rad, r.position.y + rad)
	var br := r.end - Vector2(rad, rad)
	var bl := Vector2(r.position.x + rad, r.end.y - rad)
	for i in range(seg + 1):
		var ang := PI + (PI * 0.5) * (float(i) / seg)
		pts.append(tl + Vector2(cos(ang), sin(ang)) * rad)
	for i in range(seg + 1):
		var ang := -PI * 0.5 + (PI * 0.5) * (float(i) / seg)
		pts.append(tr + Vector2(cos(ang), sin(ang)) * rad)
	for i in range(seg + 1):
		var ang := (PI * 0.5) * (float(i) / seg)
		pts.append(br + Vector2(cos(ang), sin(ang)) * rad)
	for i in range(seg + 1):
		var ang := PI * 0.5 + (PI * 0.5) * (float(i) / seg)
		pts.append(bl + Vector2(cos(ang), sin(ang)) * rad)
	return pts
