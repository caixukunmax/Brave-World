@tool
extends VBoxContainer
## 地图配置管理面板（编辑器 Dock）。
##
## 职责边界：只管理“地图清单 + 每张图的元数据（显示名/尺寸/出生点/版本）”，
## 只写数据源 tables/datas/maps/，改完靠“同步”按钮跑 npm run sync:maps 传播到各端。
## 不做可视化逐格编辑（那是运行时 MapEditor 的职责）。

const SPAWN_BUILDING_ID := 60000

var _repo_root: String = ""
var _source_dir: String = ""      # 绝对路径，指向 maps.source_dir
var _maps: PackedStringArray = [] # 与 _map_list 行一一对应的地图名（文件夹名）

var _current_map: String = ""
var _current_data: Dictionary = {}
var _spawn_locked: bool = false   # 该图有 60000 出生点建筑时锁定 spawn 编辑

# UI 引用
var _map_list: ItemList
var _detail_box: VBoxContainer
var _display_name_edit: LineEdit
var _width_spin: SpinBox
var _height_spin: SpinBox
var _spawn_x_spin: SpinBox
var _spawn_y_spin: SpinBox
var _version_label: Label
var _spawn_warning: Label
var _new_name_edit: LineEdit
var _new_w_spin: SpinBox
var _new_h_spin: SpinBox
var _output_edit: TextEdit
var _status_label: Label
var _editor_window: Window = null


func _ready() -> void:
	custom_minimum_size = Vector2(340, 200)
	_resolve_paths()
	_build_ui()
	if _source_dir == "":
		_set_status("未找到地图数据源（paths.json / tables/datas/maps）。请确认仓库结构。")
	else:
		_refresh_map_list()


# ================= 路径 =================

func _resolve_paths() -> void:
	var client_dir := ProjectSettings.globalize_path("res://")
	_repo_root = client_dir.trim_suffix("/").get_base_dir()
	var paths_file := _abs("paths.json")
	if not FileAccess.file_exists(paths_file):
		_source_dir = ""
		return
	var f := FileAccess.open(paths_file, FileAccess.READ)
	if f == null:
		_source_dir = ""
		return
	var parsed: Variant = JSON.parse_string(f.get_as_text())
	f.close()
	if typeof(parsed) != TYPE_DICTIONARY or not parsed.has("maps"):
		_source_dir = ""
		return
	_source_dir = _abs(String(parsed["maps"].get("source_dir", "tables/datas/maps")))


func _abs(rel: String) -> String:
	return _repo_root + "/" + rel


func _map_json_path(name: String) -> String:
	return _source_dir + "/" + name + "/map.json"


# ================= UI 构建 =================

func _build_ui() -> void:
	var top := HBoxContainer.new()
	top.add_child(_mk_label("地图配置管理（仅元数据；改完记得点同步）"))
	var refresh := Button.new()
	refresh.text = "刷新"
	refresh.pressed.connect(_refresh_map_list)
	top.add_child(refresh)
	var open_ed := Button.new()
	open_ed.text = "🗺 打开地图编辑器（单元）"
	open_ed.pressed.connect(_open_editor)
	top.add_child(open_ed)
	add_child(top)

	_status_label = _mk_label("")
	_status_label.add_theme_color_override("font_color", Color(0.8, 0.9, 0.5))
	add_child(_status_label)

	var main := HBoxContainer.new()
	main.size_flags_vertical = Control.SIZE_EXPAND_FILL
	add_child(main)

	# 左：地图列表
	var left := VBoxContainer.new()
	left.custom_minimum_size = Vector2(220, 0)
	left.add_child(_mk_label("地图列表"))
	_map_list = ItemList.new()
	_map_list.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_map_list.select_mode = ItemList.SELECT_SINGLE
	_map_list.item_selected.connect(_on_map_selected)
	left.add_child(_map_list)
	main.add_child(left)

	# 右：详情 + 操作
	var right := VBoxContainer.new()
	right.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	main.add_child(right)

	_detail_box = VBoxContainer.new()
	_detail_box.add_child(_mk_label("—— 选中一张地图查看/编辑元数据 ——"))
	right.add_child(_detail_box)

	right.add_child(HSeparator.new())

	# 新建
	var newbox := VBoxContainer.new()
	newbox.add_child(_mk_label("新建地图"))
	var namer := HBoxContainer.new()
	namer.add_child(_mk_label("名称"))
	_new_name_edit = LineEdit.new()
	_new_name_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	namer.add_child(_new_name_edit)
	newbox.add_child(namer)
	var dims := HBoxContainer.new()
	dims.add_child(_mk_label("宽"))
	_new_w_spin = _mk_spin(1, 4096)
	_new_w_spin.value = 50
	dims.add_child(_new_w_spin)
	dims.add_child(_mk_label("高"))
	_new_h_spin = _mk_spin(1, 4096)
	_new_h_spin.value = 50
	dims.add_child(_new_h_spin)
	var create := Button.new()
	create.text = "➕ 新建地图"
	create.pressed.connect(_new_map)
	dims.add_child(create)
	newbox.add_child(dims)
	right.add_child(newbox)

	var del := Button.new()
	del.text = "🗑 删除当前地图（移入 .trash，不硬删）"
	del.pressed.connect(_delete_map)
	right.add_child(del)

	right.add_child(HSeparator.new())

	# 同步 / 校验
	var ops := HBoxContainer.new()
	var sync := Button.new()
	sync.text = "🔄 同步 sync:maps"
	sync.pressed.connect(func(): _run_npm("sync:maps"))
	ops.add_child(sync)
	var verify := Button.new()
	verify.text = "✅ 校验 verify:maps"
	verify.pressed.connect(func(): _run_npm("verify:maps"))
	ops.add_child(verify)
	var clear := Button.new()
	clear.text = "清空输出"
	clear.pressed.connect(func(): _output_edit.text = "")
	ops.add_child(clear)
	right.add_child(ops)

	_output_edit = TextEdit.new()
	_output_edit.custom_minimum_size = Vector2(0, 140)
	_output_edit.editable = false
	_output_edit.size_flags_vertical = Control.SIZE_EXPAND_FILL
	right.add_child(_output_edit)


func _mk_label(t: String) -> Label:
	var l := Label.new()
	l.text = t
	return l


func _mk_spin(minv: int, maxv: int) -> SpinBox:
	var s := SpinBox.new()
	s.min_value = minv
	s.max_value = maxv
	s.step = 1
	return s


# ================= 列表 / 选择 =================

func _refresh_map_list() -> void:
	_maps.clear()
	_map_list.clear()
	if _source_dir == "" or not DirAccess.dir_exists_absolute(_source_dir):
		_set_status("地图源目录不存在：%s" % _source_dir)
		return
	var da := DirAccess.open(_source_dir)
	if da == null:
		_set_status("无法打开地图源目录：%s" % _source_dir)
		return
	da.list_dir_begin()
	var names: Array[String] = []
	var dir := da.get_next()
	while dir != "":
		if da.current_is_dir() and FileAccess.file_exists(_source_dir + "/" + dir + "/map.json"):
			names.append(dir)
		dir = da.get_next()
	da.list_dir_end()
	names.sort()
	for n in names:
		_maps.append(n)
		_map_list.add_item(n)
	_set_status("共 %d 张地图（源：%s）" % [names.size(), _source_dir])


func _on_map_selected(index: int) -> void:
	if index < 0 or index >= _maps.size():
		return
	_load_map(_maps[index])


func _load_map(name: String) -> void:
	var path := _map_json_path(name)
	var data: Variant = _read_json(path)
	if typeof(data) != TYPE_DICTIONARY:
		_set_status("读取失败：%s" % path)
		return
	_current_map = name
	_current_data = data

	for c in _detail_box.get_children():
		_detail_box.remove_child(c)
		c.queue_free()
	_detail_box.add_child(_mk_label("编辑地图：%s" % name))

	_display_name_edit = LineEdit.new()
	_display_name_edit.text = String(data.get("display_name", name))
	_detail_box.add_child(_mk_label("显示名 display_name"))
	_detail_box.add_child(_display_name_edit)

	var bounds: Dictionary = data.get("bounds", {"w": 0, "h": 0})
	var wh := HBoxContainer.new()
	wh.add_child(_mk_label("宽 width"))
	_width_spin = _mk_spin(1, 4096)
	_width_spin.value = int(bounds.get("w", 0))
	wh.add_child(_width_spin)
	wh.add_child(_mk_label("高 height"))
	_height_spin = _mk_spin(1, 4096)
	_height_spin.value = int(bounds.get("h", 0))
	wh.add_child(_height_spin)
	_detail_box.add_child(wh)

	var spawn: Dictionary = data.get("spawn", {})
	var sp := HBoxContainer.new()
	sp.add_child(_mk_label("出生点 spawn.x"))
	_spawn_x_spin = _mk_spin(0, 4096)
	_spawn_x_spin.value = int(spawn.get("x", 0))
	sp.add_child(_spawn_x_spin)
	sp.add_child(_mk_label("spawn.y"))
	_spawn_y_spin = _mk_spin(0, 4096)
	_spawn_y_spin.value = int(spawn.get("y", 0))
	sp.add_child(_spawn_y_spin)
	_detail_box.add_child(sp)

	_spawn_warning = _mk_label("")
	_spawn_warning.add_theme_color_override("font_color", Color(1.0, 0.6, 0.3))
	_detail_box.add_child(_spawn_warning)

	_spawn_locked = _has_spawn_building(data.get("cells", {}))
	if _spawn_locked:
		_spawn_warning.text = "⚠ 该图存在出生点建筑(60000)，同步时以建筑位置为准，spawn 输入已禁用。"
		_spawn_warning.visible = true
		_spawn_x_spin.editable = false
		_spawn_y_spin.editable = false
	else:
		_spawn_warning.visible = false

	_version_label = _mk_label("version: %s（只读）" % str(data.get("version", "?")))
	_detail_box.add_child(_version_label)

	var save := Button.new()
	save.text = "💾 保存到源文件"
	save.pressed.connect(_save_current)
	_detail_box.add_child(save)


# ================= 保存 =================

func _save_current() -> void:
	if _current_map == "":
		_set_status("未选中地图")
		return
	var data: Dictionary = _current_data.duplicate(true)
	data["display_name"] = _display_name_edit.text.strip_edges()

	var bounds: Dictionary = data.get("bounds", {"x": 0, "y": 0})
	var old_w := int(bounds.get("w", 0))
	var old_h := int(bounds.get("h", 0))
	var new_w := int(_width_spin.value)
	var new_h := int(_height_spin.value)
	bounds["w"] = new_w
	bounds["h"] = new_h
	data["bounds"] = bounds

	var resized := false
	if new_w != old_w or new_h != old_h:
		data["cells"] = _resize_cells(data.get("cells", {}), new_w, new_h)
		resized = true

	if not _spawn_locked:
		data["spawn"] = {"x": int(_spawn_x_spin.value), "y": int(_spawn_y_spin.value)}

	var path := _map_json_path(_current_map)
	if not _write_json(path, data):
		_set_status("写入失败：%s" % path)
		return
	_current_data = data

	var msg := "已保存 %s" % _current_map
	if resized:
		msg += "（尺寸 %d×%d → %d×%d，已补齐/裁剪边缘格）" % [old_w, old_h, new_w, new_h]
	msg += "。记得点“同步”传播到各端。"
	_set_status(msg)
	_load_map(_current_map)


## 只新增默认格 / 丢弃越界格，已有格的 terrain/height/custom/decoration 保持不变。
func _resize_cells(cells: Dictionary, new_w: int, new_h: int) -> Dictionary:
	var out := {}
	for y in range(new_h):
		for x in range(new_w):
			var key := "%d_%d" % [x, y]
			if cells.has(key):
				out[key] = cells[key]
			else:
				out[key] = {"terrain": 0, "height": 0, "custom": ""}
	return out


func _has_spawn_building(cells: Dictionary) -> bool:
	for key in cells:
		var cell: Variant = cells[key]
		if typeof(cell) == TYPE_DICTIONARY and int(cell.get("decoration", -1)) == SPAWN_BUILDING_ID:
			return true
	return false


# ================= 新建 =================

func _new_map() -> void:
	var name := _new_name_edit.text.strip_edges()
	var err := _validate_new_name(name)
	if err != "":
		_set_status("新建失败：" + err)
		return
	var w := int(_new_w_spin.value)
	var h := int(_new_h_spin.value)
	var dir := _source_dir + "/" + name
	DirAccess.make_dir_recursive_absolute(dir)
	var data := {
		"bounds": {"x": 0, "y": 0, "w": w, "h": h},
		"cells": _resize_cells({}, w, h),
		"display_name": name,
		"spawn": {"x": int(w / 2.0), "y": int(h / 2.0)},
		"version": 3,
	}
	if not _write_json(dir + "/map.json", data):
		_set_status("新建失败：无法写入 map.json")
		return
	_new_name_edit.text = ""
	_refresh_map_list()
	for i in range(_maps.size()):
		if _maps[i] == name:
			_map_list.select(i)
			_load_map(name)
			break
	_set_status("已新建地图 %s（%d×%d）。记得点“同步”。" % [name, w, h])


func _validate_new_name(name: String) -> String:
	if name == "":
		return "名称不能为空"
	if name == "." or name == "..":
		return "非法名称"
	for ch in ["/", "\\", ":", "*", "?", "\"", "<", ">", "|"]:
		if name.contains(ch):
			return "名称含非法字符：" + ch
	if DirAccess.dir_exists_absolute(_source_dir + "/" + name):
		return "同名地图已存在：" + name
	return ""


# ================= 删除（移入 .trash） =================

func _delete_map() -> void:
	if _current_map == "":
		_set_status("未选中地图")
		return
	var name := _current_map
	var dlg := AcceptDialog.new()
	dlg.title = "确认删除"
	dlg.dialog_text = "把地图“%s”移入 .trash/maps（不硬删）。删除后需点“同步”才会从各端与注册表移除。确认？" % name
	add_child(dlg)
	dlg.confirmed.connect(_do_delete_map.bind(name))
	dlg.confirmed.connect(dlg.queue_free)
	dlg.canceled.connect(dlg.queue_free)
	dlg.popup_centered()


func _do_delete_map(name: String) -> void:
	var ts := str(Time.get_unix_time_from_system())
	DirAccess.make_dir_recursive_absolute(_abs(".trash/maps"))
	var da := DirAccess.open(_repo_root)
	var rel_src := _source_dir.trim_prefix(_repo_root + "/") + "/" + name
	var rel_dst := ".trash/maps/" + name + "_" + ts
	if da == null or da.rename(rel_src, rel_dst) != OK:
		_set_status("删除失败：无法移动 %s" % rel_src)
		return
	if _current_map == name:
		_current_map = ""
		_current_data = {}
	_refresh_map_list()
	_set_status("已将“%s”移入 %s。记得点“同步”。" % [name, rel_dst])


# ================= 同步 / 校验 =================

func _run_npm(script: String) -> void:
	if _repo_root == "":
		_set_status("仓库根未定位，无法运行 npm")
		return
	if _repo_root.contains(" "):
		_set_status("仓库路径含空格，同步命令会失败；请把项目放到无空格路径，或手动跑 npm run " + script)
		return
	_append_output("$ npm run %s   (cwd=%s)" % [script, _repo_root])
	var out := []
	var win_root := _repo_root.replace("/", "\\")
	var command := "cd /d %s && npm run %s" % [win_root, script]
	var code := OS.execute("cmd.exe", PackedStringArray(["/c", command]), out, true)
	if out.size() > 0:
		_append_output(String(out[0]))
	_append_output("[exit code] %d" % code)
	if code == 0:
		_refresh_map_list()
	_set_status("npm run %s 完成，退出码 %d" % [script, code])


func _append_output(t: String) -> void:
	_output_edit.text += t + "\n"
	_output_edit.scroll_vertical = 9999999


func _open_editor() -> void:
	if _editor_window != null and is_instance_valid(_editor_window):
		_editor_window.popup()
		_editor_window.grab_focus()
		return
	if _source_dir == "":
		_set_status("未找到地图数据源，无法打开编辑器")
		return
	_editor_window = Window.new()
	_editor_window.title = "地图单元编辑器"
	_editor_window.size = Vector2i(1280, 820)
	_editor_window.min_size = Vector2i(640, 400)
	_editor_window.unresizable = false
	_editor_window.mode = Window.MODE_WINDOWED
	_editor_window.close_requested.connect(_editor_window.hide)
	var ed := preload("res://addons/map_config_manager/map_editor_window.gd").new()
	ed.repo_root = _repo_root
	ed.source_dir = _source_dir
	_editor_window.add_child(ed)
	ed.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(_editor_window)
	_editor_window.popup_centered()


# ================= 文件读写 / 状态 =================

func _read_json(path: String) -> Variant:
	if not FileAccess.file_exists(path):
		return null
	var f := FileAccess.open(path, FileAccess.READ)
	if f == null:
		return null
	var txt := f.get_as_text()
	f.close()
	if txt.begins_with("\uFEFF"):
		txt = txt.substr(1)
	return JSON.parse_string(txt)


func _write_json(path: String, data: Dictionary) -> bool:
	var f := FileAccess.open(path, FileAccess.WRITE)
	if f == null:
		return false
	f.store_string(JSON.stringify(data, "\t") + "\n")
	f.close()
	return true


func _set_status(t: String) -> void:
	if _status_label != null:
		_status_label.text = t
