@tool
extends EditorPlugin

var _session_id: String = ""
var _base_url: String = "http://127.0.0.1:18789"
var _connected: bool = false
var _heartbeat_timer: float = 0.0
var _poll_timer: float = 0.0
var _http_reg: HTTPRequest
var _http_heartbeat: HTTPRequest
var _http_poll: HTTPRequest
var _http_cmd: HTTPRequest
var _control: VBoxContainer
var _status_label: Label
var _url_edit: LineEdit
var _connect_btn: Button


func _enter_tree():
	_control = VBoxContainer.new()
	_control.custom_minimum_size = Vector2(200, 100)

	var title = Label.new()
	title.text = "OpenClaw"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_control.add_child(title)

	_status_label = Label.new()
	_status_label.text = "Not connected"
	_status_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_control.add_child(_status_label)

	var url_label = Label.new()
	url_label.text = "Gateway URL:"
	_control.add_child(url_label)

	_url_edit = LineEdit.new()
	_url_edit.text = _base_url
	_url_edit.placeholder_text = "http://127.0.0.1:18789"
	_control.add_child(_url_edit)

	_connect_btn = Button.new()
	_connect_btn.text = "Connect"
	_connect_btn.pressed.connect(_on_connect_pressed)
	_control.add_child(_connect_btn)

	add_control_to_dock(DOCK_SLOT_LEFT_UL, _control)

	_http_reg = HTTPRequest.new()
	_http_heartbeat = HTTPRequest.new()
	_http_poll = HTTPRequest.new()
	_http_cmd = HTTPRequest.new()
	add_child(_http_reg)
	add_child(_http_heartbeat)
	add_child(_http_poll)
	add_child(_http_cmd)

	_http_reg.request_completed.connect(_on_register_completed)
	_http_poll.request_completed.connect(_on_poll_completed)
	_http_cmd.request_completed.connect(_on_cmd_completed)


func _exit_tree():
	if _control:
		remove_control_from_docks(_control)
		_control.queue_free()
	if _http_reg:
		_http_reg.queue_free()
	if _http_heartbeat:
		_http_heartbeat.queue_free()
	if _http_poll:
		_http_poll.queue_free()
	if _http_cmd:
		_http_cmd.queue_free()
	_disconnect()


func _process(delta):
	if not _connected:
		return
	_heartbeat_timer += delta
	_poll_timer += delta
	if _heartbeat_timer >= 15.0:
		_heartbeat_timer = 0.0
		_send_heartbeat()
	if _poll_timer >= 0.5:
		_poll_timer = 0.0
		_poll_commands()


func _on_connect_pressed():
	if _connected:
		_disconnect()
	else:
		_connect()


func _connect():
	_base_url = _url_edit.text.rstrip("/")
	_status_label.text = "Connecting..."
	_connect_btn.disabled = true
	var url = _base_url + "/godot/register"
	var json = JSON.new()
	var body = json.stringify({
		"project": ProjectSettings.get_setting("application/config/name"),
		"version": Engine.get_version_info().get("string", "unknown"),
		"platform": "GodotEditor",
		"tools": 20
	})
	_http_reg.request(url, ["Content-Type: application/json"], HTTPClient.METHOD_POST, body)


func _disconnect():
	_connected = false
	_session_id = ""
	_status_label.text = "Not connected"
	_connect_btn.text = "Connect"
	_connect_btn.disabled = false


func _on_register_completed(result, code, headers, body):
	_connect_btn.disabled = false
	if result != HTTPRequest.RESULT_SUCCESS or code != 200:
		_status_label.text = "Failed: HTTP " + str(code)
		return
	var json = JSON.new()
	var parsed = json.parse(body.get_string_from_utf8())
	if parsed != OK or json.data == null:
		_status_label.text = "Failed: bad response"
		return
	var data = json.data
	if not data.has("sessionId"):
		_status_label.text = "Failed: no sessionId"
		return
	_session_id = data["sessionId"]
	_connected = true
	_status_label.text = "Connected"
	_connect_btn.text = "Disconnect"
	print("[OpenClaw] Connected: ", _session_id)


func _send_heartbeat():
	if not _connected:
		return
	var url = _base_url + "/godot/heartbeat"
	var json = JSON.new()
	var body = json.stringify({"sessionId": _session_id})
	_http_heartbeat.request(url, ["Content-Type: application/json"], HTTPClient.METHOD_POST, body)


func _poll_commands():
	if not _connected:
		return
	if _http_poll.get_http_client_status() != HTTPClient.STATUS_DISCONNECTED:
		return
	var url = _base_url + "/godot/poll?sessionId=" + _session_id.uri_encode()
	_http_poll.request(url, PackedStringArray(), HTTPClient.METHOD_GET)


func _on_poll_completed(result, code, headers, body):
	if result != HTTPRequest.RESULT_SUCCESS:
		return
	if code == 204:
		return
	if code != 200:
		return
	var json = JSON.new()
	var parsed = json.parse(body.get_string_from_utf8())
	if parsed != OK or json.data == null:
		return
	var data = json.data
	var tool_call_id = str(data.get("toolCallId", ""))
	var tool_name = str(data.get("tool", ""))
	var args = data.get("arguments", {})
	var cmd_result = _execute_tool(tool_name, args)
	_send_result(tool_call_id, cmd_result)


func _send_result(tool_call_id, result):
	var url = _base_url + "/godot/result"
	var json = JSON.new()
	var body = json.stringify({"sessionId": _session_id, "toolCallId": tool_call_id, "result": result})
	_http_cmd.request(url, ["Content-Type: application/json"], HTTPClient.METHOD_POST, body)


func _on_cmd_completed(result, code, headers, body):
	pass


func _execute_tool(tool_name, args):
	match tool_name:
		"editor.play":
			EditorInterface.play_current_scene()
			return {"success": true, "result": "Playing"}
		"editor.stop":
			EditorInterface.stop_playing_scene()
			return {"success": true, "result": "Stopped"}
		"editor.getState":
			return {"success": true, "result": {
				"playing": EditorInterface.is_playing_scene()
			}}
		"scene.getCurrent":
			var s = EditorInterface.get_edited_scene_root()
			if s != null:
				return {"success": true, "result": {"name": s.name, "path": s.scene_file_path}}
			return {"success": false, "error": "No scene open"}
		"node.find":
			return _find_nodes(args)
		"node.getProperty":
			return _get_node_property(args)
		"node.setProperty":
			return _set_node_property(args)
		"debug.tree":
			return _get_scene_tree()
		"debug.screenshot":
			return _take_screenshot()
		"console.getLogs":
			return _get_console_logs(args)
		"script.read":
			var p = str(args.get("path", ""))
			if p == "":
				return {"success": false, "error": "Missing path"}
			var f = FileAccess.open(p, FileAccess.READ)
			if f == null:
				return {"success": false, "error": "Cannot open file"}
			var c = f.get_as_text()
			f.close()
			return {"success": true, "result": c}
		_:
			return {"success": false, "error": "Unknown tool: " + tool_name}


func _find_nodes(args):
	var scene = EditorInterface.get_edited_scene_root()
	if scene == null:
		return {"success": false, "error": "No scene"}
	var name_f = str(args.get("name", ""))
	var type_f = str(args.get("type", ""))
	var results = []
	_find_nodes_recursive(scene, name_f, type_f, results)
	return {"success": true, "result": results}


func _find_nodes_recursive(node, name_f, type_f, results):
	var ok = true
	if name_f != "" and name_f.to_lower() not in node.name.to_string().to_lower():
		ok = false
	if type_f != "" and node.get_class() != type_f:
		ok = false
	if ok:
		results.append({"name": node.name, "type": node.get_class(), "path": str(node.get_path())})
	for c in node.get_children():
		_find_nodes_recursive(c, name_f, type_f, results)


func _get_node_property(args):
	var scene = EditorInterface.get_edited_scene_root()
	if scene == null:
		return {"success": false, "error": "No scene"}
	var node_path = str(args.get("path", ""))
	var node = scene.get_node_or_null(node_path)
	if node == null:
		return {"success": false, "error": "Node not found"}
	var prop = str(args.get("property", ""))
	var v = node.get(prop)
	return {"success": true, "result": str(v)}


func _set_node_property(args):
	var scene = EditorInterface.get_edited_scene_root()
	if scene == null:
		return {"success": false, "error": "No scene"}
	var node_path = str(args.get("path", ""))
	var node = scene.get_node_or_null(node_path)
	if node == null:
		return {"success": false, "error": "Node not found"}
	var prop = str(args.get("property", ""))
	var value = args.get("value", "")
	node.set(prop, value)
	return {"success": true, "result": "Set"}


func _get_scene_tree():
	var scene = EditorInterface.get_edited_scene_root()
	if scene == null:
		return {"success": false, "error": "No scene"}
	var lines = []
	_build_tree(scene, "", lines)
	return {"success": true, "result": lines}


func _build_tree(node, indent, lines):
	lines.append(indent + str(node.name) + " [" + node.get_class() + "]")
	for c in node.get_children():
		_build_tree(c, indent + "  ", lines)


func _take_screenshot():
	var vp = get_viewport()
	if vp == null:
		return {"success": false, "error": "No viewport"}
	var tex = vp.get_texture()
	if tex == null:
		return {"success": false, "error": "No texture"}
	var img = tex.get_image()
	if img == null:
		return {"success": false, "error": "No image"}
	var path = "user://openclaw_screenshot.png"
	var err = img.save_png(path)
	if err != OK:
		return {"success": false, "error": "Save failed"}
	var abs_path = ProjectSettings.globalize_path(path)
	return {"success": true, "result": {"path": abs_path, "width": img.get_width(), "height": img.get_height()}}


func _get_console_logs(args):
	var editor_paths = EditorInterface.get_editor_paths()
	if editor_paths == null:
		return {"success": false, "error": "No editor paths"}
	var log_dir: String = editor_paths.get_log_dir()
	var files = DirAccess.get_files_at(log_dir)
	if files.size() == 0:
		return {"success": false, "error": "No logs"}
	var latest = files[files.size() - 1]
	var f = FileAccess.open(log_dir + "/" + latest, FileAccess.READ)
	if f == null:
		return {"success": false, "error": "Cannot read log"}
	var content = f.get_as_text()
	f.close()
	var all_lines = content.split("\n")
	var limit = int(args.get("limit", 50))
	var start_idx = maxi(0, all_lines.size() - limit)
	var result_lines = []
	for i in range(start_idx, all_lines.size()):
		result_lines.append(all_lines[i])
	return {"success": true, "result": result_lines}