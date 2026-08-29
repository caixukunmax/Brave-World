@tool
extends EditorPlugin
## 技能特效预览插件：右侧 Dock 放一个按钮，点击弹出独立预览窗口。
## 窗口内容是 C# 的 SkillPreviewPanel（读 SkillEffectRegistry，列出全部技能）。

const WindowScript := preload("res://Scripts/Effects/SkillPreviewPanel.cs")

var _dock: Control = null
var _win: Window = null


func _enter_tree() -> void:
	_dock = VBoxContainer.new()
	_dock.name = "SkillEffectPreviewDock"
	var btn := Button.new()
	btn.text = "🎇 打开技能特效预览"
	btn.pressed.connect(_open_window)
	_dock.add_child(btn)
	add_control_to_dock(EditorPlugin.DOCK_SLOT_RIGHT_UL, _dock)


func _exit_tree() -> void:
	if _win != null and is_instance_valid(_win):
		_win.queue_free()
		_win = null
	if _dock != null:
		remove_control_from_docks(_dock)
		_dock.queue_free()
		_dock = null


func _open_window() -> void:
	if _win != null and is_instance_valid(_win):
		_win.popup()
		_win.grab_focus()
		return
	_win = Window.new()
	_win.title = "技能特效预览"
	_win.size = Vector2i(900, 600)
	_win.min_size = Vector2i(480, 360)
	_win.unresizable = false
	_win.mode = Window.MODE_WINDOWED
	_win.close_requested.connect(_win.hide)
	var content = WindowScript.new()
	_win.add_child(content)
	content.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_dock.add_child(_win)
	_win.popup_centered()
