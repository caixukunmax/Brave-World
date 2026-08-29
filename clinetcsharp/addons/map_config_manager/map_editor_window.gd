@tool
extends Control
## 地图单元编辑器（独立窗口）。编辑格子内容：刷地面(terrain) + 放建筑(decoration, footprint)。
## 只写数据源 tables/datas/maps/<名>/map.json（只标锚点格），再点同步传播。
## 不迁移数据模型；地面/建筑两套字段自动映射。

var repo_root: String = ""
var source_dir: String = ""

# 数据
var _map_names: PackedStringArray = []
var _current_map: String = ""
var cells: Dictionary = {}
var occupied: Dictionary = {}          # cellKey -> anchorKey（含锚点自身）
var _anchors: Array = []               # [{x,y,sx,sy,id,k}]
var bounds_w: int = 0
var bounds_h: int = 0
var _meta: Dictionary = {}             # version/display_name/bounds/spawn 原样保留

var _terrain_list: Array = []          # [{id,name,color}]
var _terrain_by_id: Dictionary = {}    # id -> Color
var _building_list: Array = []         # [{id,name,sx,sy}]
var _building_by_id: Dictionary = {}   # id -> {name,sx,sy}

# 工具状态
var tool: String = "ground"            # ground | building | erase_building | erase_ground
var sel_terrain: int = 0
var sel_building: int = 10001
var zoom: float = 24.0
var offset: Vector2 = Vector2(16, 16)
var hover: Vector2i = Vector2i(-1, -1)
var painting: bool = false
var panning: bool = false
var last_mouse: Vector2 = Vector2.ZERO
var _dirty: bool = false

# UI
var _canvas: Control
var _map_sel: OptionButton
var _status: Label
var _tool_label: Label
var _out: TextEdit


func _ready() -> void:
	_load_palettes()
	_build_ui()
	_scan_maps()
	_set_status("选一张地图开始编辑。滚轮缩放，中键拖动平移，左键按当前工具操作。")


# ================= 数据加载 =================

func _load_palettes() -> void:
	# 地形：按游戏 UpdateTerrainMask 的取色规则算显示色（普通=黑, 城墙=灰, 空气墙=红, 其余=其色）
	var tp := "res://data/terrain_config.json"
	if FileAccess.file_exists(tp):
		var f := FileAccess.open(tp, FileAccess.READ)
		var ttxt := f.get_as_text()
		f.close()
		if ttxt.begins_with("\uFEFF"):
			ttxt = ttxt.substr(1)
		var arr: Variant = JSON.parse_string(ttxt)
		if typeof(arr) == TYPE_ARRAY:
			for t in arr:
				var id: int = int(t.get("id", 0))
				var col := _terrain_display_color(id, int(t.get("color_r", 0)), int(t.get("color_g", 0)), int(t.get("color_b", 0)), float(t.get("color_a", 0)))
				_terrain_list.append({"id": id, "name": String(t.get("name", str(id))), "color": col})
				_terrain_by_id[id] = col
	if _terrain_by_id.is_empty():
		_terrain_list.append({"id": 0, "name": "普通", "color": Color(0, 0, 0)})
		_terrain_by_id[0] = Color(0, 0, 0)

	# 建筑：读 appearance 的颜色/圆角/缩放/不透明度 + 标签名（与游戏 AppearanceComponent 同源）
	var cf := ConfigFile.new()
	if cf.load("res://debug_panel_config.cfg") == OK:
		for s in cf.get_sections():
			if not s.begins_with("profile_") or s.contains("."):
				continue
			var idstr := s.trim_prefix("profile_")
			if not _is_int(idstr):
				continue
			if String(cf.get_value(s, "entity_type", "")) != "decoration":
				continue
			var id := int(idstr)
			var nm := String(cf.get_value(s + ".labels", "label_0_content", ""))
			if nm == "":
				nm = String(cf.get_value(s, "name", idstr))
			var sec := s + ".appearance"
			var sx := int(cf.get_value(sec, "size_x", 1))
			var sy := int(cf.get_value(sec, "size_y", 1))
			var bg := Color(float(cf.get_value(sec, "bg_color_r", 0.5)), float(cf.get_value(sec, "bg_color_g", 0.5)), float(cf.get_value(sec, "bg_color_b", 0.5)))
			var bd := Color(float(cf.get_value(sec, "border_color_r", 0.6)), float(cf.get_value(sec, "border_color_g", 0.6)), float(cf.get_value(sec, "border_color_b", 0.6)))
			var tx := Color(float(cf.get_value(sec, "text_color_r", 1.0)), float(cf.get_value(sec, "text_color_g", 1.0)), float(cf.get_value(sec, "text_color_b", 0.9)))
			var op := float(cf.get_value(sec, "bg_opacity", 0.9))
			if op > 1.0:
				op = op / 10000.0
			var vs := float(cf.get_value(sec, "visual_size_scale", 1.0))
			if vs > 1.0:
				vs = vs / 10000.0
			var cr := float(cf.get_value(sec, "corner_radius", 8.0))
			bg.a = clamp(op, 0.0, 1.0)
			_building_by_id[id] = {
				"name": nm, "sx": max(1, sx), "sy": max(1, sy),
				"bg": bg, "border": bd, "text": tx,
				"corner": cr, "vscale": clamp(vs, 0.2, 1.0),
			}
		var ids := _building_by_id.keys()
		ids.sort()
		for id in ids:
			var d: Dictionary = _building_by_id[id]
			_building_list.append({"id": int(id), "name": d["name"], "sx": d["sx"], "sy": d["sy"]})


func _is_int(s: String) -> bool:
	if s == "":
		return false
	for i in range(s.length()):
		if s[i] < "0" or s[i] > "9":
			return false
	return true


func _terrain_display_color(id: int, r: int, g: int, b: int, a: float) -> Color:
	if id == 9:
		return Color(0.15, 0.15, 0.15)   # 城墙：与游戏 OutsideMapColor 一致
	if id == 10:
		return Color(1, 0, 0)            # 空气墙：红
	if a > 0.0 and (r > 0 or g > 0 or b > 0):
		return Color(r / 255.0, g / 255.0, b / 255.0)
	return Color(0, 0, 0)                # 普通等：黑（游戏里 alpha=0 走黑色+网格线）


func outside_color() -> Color:
	return Color(0.15, 0.15, 0.15)


func terrain_color(t: int) -> Color:
	return _terrain_by_id.get(t, Color(0, 0, 0))


func building_by_id(id: int) -> Variant:
	return _building_by_id.get(id, null)


func anchors() -> Array:
	return _anchors


func hover_within() -> bool:
	return hover.x >= 0 and hover.y >= 0 and hover.x < bounds_w and hover.y < bounds_h


# ================= 地图 载入/扫描 =================

func _scan_maps() -> void:
	_map_names.clear()
	_map_sel.clear()
	if source_dir == "" or not DirAccess.dir_exists_absolute(source_dir):
		return
	var da := DirAccess.open(source_dir)
	if da == null:
		return
	da.list_dir_begin()
	var names: Array[String] = []
	var d := da.get_next()
	while d != "":
		if da.current_is_dir() and FileAccess.file_exists(source_dir + "/" + d + "/map.json"):
			names.append(d)
		d = da.get_next()
	da.list_dir_end()
	names.sort()
	for n in names:
		_map_names.append(n)
		_map_sel.add_item(n)


func _on_map_selected(index: int) -> void:
	if index < 0 or index >= _map_names.size():
		return
	_load_map(_map_names[index])


func _load_map(name: String) -> void:
	var path := source_dir + "/" + name + "/map.json"
	if not FileAccess.file_exists(path):
		_set_status("找不到 " + path)
		return
	var f := FileAccess.open(path, FileAccess.READ)
	var txt := f.get_as_text()
	f.close()
	if txt.begins_with("\uFEFF"):
		txt = txt.substr(1)
	var data: Variant = JSON.parse_string(txt)
	if typeof(data) != TYPE_DICTIONARY:
		_set_status("解析失败：" + path)
		return
	_current_map = name
	cells = data.get("cells", {})
	_meta = {
		"version": data.get("version", 3),
		"display_name": data.get("display_name", name),
		"bounds": data.get("bounds", {"x": 0, "y": 0, "w": 50, "h": 50}),
		"spawn": data.get("spawn", {"x": 25, "y": 25}),
	}
	var b: Dictionary = _meta["bounds"]
	bounds_w = int(b.get("w", 0))
	bounds_h = int(b.get("h", 0))
	_recompute()
	_dirty = false
	_canvas.queue_redraw()
	_set_status("已载入 %s（%d×%d，%d 格）。" % [name, bounds_w, bounds_h, cells.size()])


func _recompute() -> void:
	_anchors.clear()
	occupied.clear()
	var arr: Array = []
	for k in cells.keys():
		var parts := String(k).split("_")
		if parts.size() != 2:
			continue
		arr.append({"x": int(parts[0]), "y": int(parts[1]), "k": String(k)})
	arr.sort_custom(func(a, b): return a.y < b.y or (a.y == b.y and a.x < b.x))
	for e in arr:
		var cell: Dictionary = cells[e.k]
		var dec := int(cell.get("decoration", 0))
		if dec == 0:
			continue
		if occupied.has(e.k):
			continue
		var def: Variant = building_by_id(dec)
		var sx: int = int(def.sx) if def != null else 1
		var sy: int = int(def.sy) if def != null else 1
		_anchors.append({"x": e.x, "y": e.y, "sx": sx, "sy": sy, "id": dec, "k": e.k})
		for dy in range(sy):
			for dx in range(sx):
				occupied["%d_%d" % [e.x + dx, e.y + dy]] = e.k


# ================= 编辑操作 =================

func apply_at(c: Vector2i) -> void:
	if _current_map == "":
		return
	if c.x < 0 or c.y < 0 or c.x >= bounds_w or c.y >= bounds_h:
		return
	match tool:
		"ground":
			_paint_ground(c)
		"building":
			_place_building(c)
		"erase_building":
			_erase_building(c)
		"erase_ground":
			_erase_ground(c)
	_canvas.queue_redraw()


func _ensure_cell(c: Vector2i) -> Dictionary:
	var key := "%d_%d" % [c.x, c.y]
	if not cells.has(key):
		cells[key] = {"terrain": 0, "height": 0, "custom": ""}
	return cells[key]


func _paint_ground(c: Vector2i) -> void:
	var cell := _ensure_cell(c)
	if int(cell.get("terrain", 0)) != sel_terrain:
		cell["terrain"] = sel_terrain
		_dirty = true


func _erase_ground(c: Vector2i) -> void:
	var key := "%d_%d" % [c.x, c.y]
	if cells.has(key) and int(cells[key].get("terrain", 0)) != 0:
		cells[key]["terrain"] = 0
		_dirty = true


func can_place(x: int, y: int, sx: int, sy: int) -> bool:
	for dy in range(sy):
		for dx in range(sx):
			var cx := x + dx
			var cy := y + dy
			if cx < 0 or cy < 0 or cx >= bounds_w or cy >= bounds_h:
				return false
			if occupied.has("%d_%d" % [cx, cy]):
				return false
	return true


func _place_building(c: Vector2i) -> void:
	var def: Variant = building_by_id(sel_building)
	if def == null:
		_set_status("未选中有效建筑")
		return
	var sx: int = int(def.sx)
	var sy: int = int(def.sy)
	if not can_place(c.x, c.y, sx, sy):
		return
	var cell := _ensure_cell(c)
	cell["decoration"] = sel_building
	# 覆盖格清空 decoration（只存锚点）
	for dy in range(sy):
		for dx in range(sx):
			if dx == 0 and dy == 0:
				continue
			var k := "%d_%d" % [c.x + dx, c.y + dy]
			if cells.has(k):
				cells[k].erase("decoration")
	_dirty = true
	_recompute()


func _erase_building(c: Vector2i) -> void:
	var key := "%d_%d" % [c.x, c.y]
	var akey: Variant = occupied.get(key, key)
	if cells.has(String(akey)) and int(cells[String(akey)].get("decoration", 0)) != 0:
		cells[String(akey)].erase("decoration")
		_dirty = true
		_recompute()


# ================= 存盘 / 同步 =================

func _save_map() -> void:
	if _current_map == "":
		_set_status("没有可保存的地图")
		return
	var out_cells := {}
	var arr: Array = []
	for k in cells.keys():
		var parts := String(k).split("_")
		if parts.size() != 2:
			continue
		arr.append({"x": int(parts[0]), "y": int(parts[1]), "k": String(k)})
	arr.sort_custom(func(a, b): return a.y < b.y or (a.y == b.y and a.x < b.x))
	for e in arr:
		var cell: Dictionary = cells[e.k]
		var cd := {
			"terrain": int(cell.get("terrain", 0)),
			"height": int(cell.get("height", 0)),
			"custom": String(cell.get("custom", "")),
		}
		var dec := int(cell.get("decoration", 0))
		if dec != 0:
			cd["decoration"] = dec
		out_cells[e.k] = cd

	var root := {
		"version": _meta.get("version", 3),
		"display_name": _meta.get("display_name", _current_map),
		"bounds": _meta.get("bounds", {"x": 0, "y": 0, "w": bounds_w, "h": bounds_h}),
		"spawn": _meta.get("spawn", {"x": 0, "y": 0}),
		"cells": out_cells,
	}
	var path := source_dir + "/" + _current_map + "/map.json"
	var f := FileAccess.open(path, FileAccess.WRITE)
	if f == null:
		_set_status("写入失败：" + path)
		return
	f.store_string(JSON.stringify(root, "\t") + "\n")
	f.close()
	_dirty = false
	_set_status("已保存到源文件 " + path + "。记得点同步传播到各端。")


func _sync() -> void:
	if repo_root == "":
		_set_status("仓库根未定位")
		return
	_out.text += "$ npm run sync:maps\n"
	var out := []
	var win_root := repo_root.replace("/", "\\")
	var command := "cd /d %s && npm run sync:maps" % win_root
	var code := OS.execute("cmd.exe", PackedStringArray(["/c", command]), out, true)
	if out.size() > 0:
		_out.text += String(out[0])
	_out.text += "[exit code] %d\n" % code
	_out.scroll_vertical = 9999999
	_set_status("同步完成，退出码 %d" % code)


func _reload() -> void:
	if _current_map != "":
		_load_map(_current_map)


# ================= 视图 =================

func zoom_at(mpos: Vector2, factor: float) -> void:
	var old := zoom
	var nz: float = clamp(zoom * factor, 6.0, 96.0)
	offset = mpos - (mpos - offset) * (nz / old)
	zoom = nz
	_canvas.queue_redraw()


# ================= UI =================

func _build_ui() -> void:
	var root := VBoxContainer.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(root)

	var bar := HBoxContainer.new()
	bar.add_child(_lbl("地图"))
	_map_sel = OptionButton.new()
	_map_sel.item_selected.connect(_on_map_selected)
	bar.add_child(_map_sel)
	_add_btn(bar, "重载", _reload)
	_add_btn(bar, "💾 保存", _save_map)
	_add_btn(bar, "🔄 同步", _sync)
	bar.add_child(VSeparator.new())
	for t in [["ground", "刷地面"], ["building", "放建筑"], ["erase_building", "擦建筑"], ["erase_ground", "擦地面"]]:
		_add_btn(bar, t[1], _set_tool.bind(t[0]))
	bar.add_child(VSeparator.new())
	_add_btn(bar, "－", func(): zoom_at(size * 0.5, 1.0 / 1.2))
	_add_btn(bar, "＋", func(): zoom_at(size * 0.5, 1.2))
	root.add_child(bar)

	_tool_label = _lbl("工具：刷地面 | 地面：普通")
	root.add_child(_tool_label)

	var main := HBoxContainer.new()
	main.size_flags_vertical = Control.SIZE_EXPAND_FILL
	root.add_child(main)

	# 左：调色板
	var scroll := ScrollContainer.new()
	scroll.custom_minimum_size = Vector2(220, 0)
	main.add_child(scroll)
	var pal := VBoxContainer.new()
	pal.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(pal)

	pal.add_child(_lbl("地面单元"))
	var tg := GridContainer.new()
	tg.columns = 2
	pal.add_child(tg)
	for t in _terrain_list:
		var b := Button.new()
		b.text = "%d %s" % [t.id, t.name]
		b.modulate = t.color
		b.pressed.connect(func():
			tool = "ground"
			sel_terrain = int(t.id)
			_update_tool_label())
		tg.add_child(b)

	pal.add_child(_lbl("建筑单元（footprint）"))
	var bg := VBoxContainer.new()
	pal.add_child(bg)
	if _building_list.is_empty():
		bg.add_child(_lbl("（未发现建筑 profile）"))
	for d in _building_list:
		var b := Button.new()
		b.text = "%s  %d×%d" % [d.name, d.sx, d.sy]
		b.pressed.connect(func():
			tool = "building"
			sel_building = int(d.id)
			_update_tool_label())
		bg.add_child(b)

	# 右：画布
	_canvas = preload("res://addons/map_config_manager/map_grid_canvas.gd").new()
	_canvas.ed = self
	main.add_child(_canvas)

	_status = _lbl("")
	root.add_child(_status)
	_out = TextEdit.new()
	_out.custom_minimum_size = Vector2(0, 80)
	_out.editable = false
	root.add_child(_out)


func _set_tool(t: String) -> void:
	tool = t
	_update_tool_label()


func _update_tool_label() -> void:
	var names := {"ground": "刷地面", "building": "放建筑", "erase_building": "擦建筑", "erase_ground": "擦地面"}
	var extra := ""
	if tool == "ground":
		extra = " | 地面：%s" % _terrain_name(sel_terrain)
	elif tool == "building":
		var def: Variant = building_by_id(sel_building)
		extra = " | 建筑：%s" % (String(def.name) if def != null else str(sel_building))
	_tool_label.text = "工具：%s%s" % [String(names.get(tool, tool)), extra]


func _terrain_name(id: int) -> String:
	for t in _terrain_list:
		if int(t.id) == id:
			return String(t.name)
	return str(id)


func _lbl(t: String) -> Label:
	var l := Label.new()
	l.text = t
	return l


func _add_btn(parent: Control, text: String, cb: Callable) -> void:
	var b := Button.new()
	b.text = text
	b.pressed.connect(cb)
	parent.add_child(b)


func _set_status(t: String) -> void:
	if _status != null:
		_status.text = t
