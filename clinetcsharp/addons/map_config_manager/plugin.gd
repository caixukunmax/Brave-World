@tool
extends EditorPlugin
## 地图配置管理插件入口：把一个 Dock 面板加到编辑器底部。
## 面板逻辑见 map_panel.gd。

const PanelScript := preload("res://addons/map_config_manager/map_panel.gd")

var _panel: Control = null


func _enter_tree() -> void:
	_panel = PanelScript.new()
	_panel.name = "MapConfigManagerPanel"
	add_control_to_dock(EditorPlugin.DOCK_SLOT_RIGHT_UL, _panel)


func _exit_tree() -> void:
	if _panel != null:
		remove_control_from_docks(_panel)
		_panel.queue_free()
		_panel = null
