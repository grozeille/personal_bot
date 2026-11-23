extends Node3D
## Jumeau numerique 3-DOF de la tete (tablette).
## Ecoute en TCP (JSON delimite par \n, voir PROTOCOL.md), applique
## yaw/pitch/roll avec butees et lissage. Repond l'etat apres chaque commande.
## Pistes `moves` : la tablette envoie des keyframes horodatees (ms).
## (Pas de WebSocketServer dans Godot 4 : TCP brut, plus simple et fiable en LAN.)

const PORT := 9080
const LIMITS := {"yaw": [-90.0, 90.0], "pitch": [-30.0, 45.0], "roll": [-30.0, 30.0]}
const SMOOTH := 8.0

var _server := TCPServer.new()
var _peer: StreamPeerTCP = null
var _rx := ""
var _target := {"yaw": 0.0, "pitch": 0.0, "roll": 0.0}
var _current := {"yaw": 0.0, "pitch": 0.0, "roll": 0.0}
var _track: Array = []
var _track_t := 0.0
var _track_active := false
var _cam_yaw := deg_to_rad(45.0)
var _cam_pitch := deg_to_rad(12.0)
var _cam_dist := 0.8
const CAM_TARGET := Vector3(0, 0.15, 0.0)

@onready var _pan_pivot: Node3D = $Head/base/u_court_48/pan_mg996r/PanPivot
@onready var _tilt_pivot: Node3D = $Head/base/u_court_48/pan_mg996r/PanPivot/TiltPivot
@onready var _camera: Camera3D = $Camera3D


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseMotion:
		var motion: InputEventMouseMotion = event
		if (motion.button_mask & MOUSE_BUTTON_MASK_LEFT) != 0:
			_cam_yaw -= motion.relative.x * 0.005
			_cam_pitch = clampf(_cam_pitch + motion.relative.y * 0.005, deg_to_rad(-60.0), deg_to_rad(85.0))
			_update_camera()
	elif event is InputEventMouseButton:
		var mb: InputEventMouseButton = event
		if mb.pressed and mb.button_index == MOUSE_BUTTON_WHEEL_UP:
			_cam_dist = clampf(_cam_dist * 0.9, 0.25, 3.0)
			_update_camera()
		elif mb.pressed and mb.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			_cam_dist = clampf(_cam_dist * 1.1, 0.25, 3.0)
			_update_camera()


func _update_camera() -> void:
	if not is_node_ready():
		return
	var cp := cos(_cam_pitch)
	var pos := CAM_TARGET + Vector3(_cam_dist * cp * sin(_cam_yaw), _cam_dist * sin(_cam_pitch), _cam_dist * cp * cos(_cam_yaw))
	_camera.position = pos
	_camera.look_at(CAM_TARGET)


func _ready() -> void:
	_apply_pose()
	_update_camera()
	var err := _server.listen(PORT)
	if err != OK:
		push_error("TCP listen %d: %s" % [PORT, err])
		return
	print("SIM-READY tcp://0.0.0.0:%d" % PORT)


func _process(delta: float) -> void:
	if _server.is_connection_available():
		if _peer != null:
			_peer.disconnect_from_host()
		_peer = _server.take_connection()
		_rx = ""
		print("SIM-PEER connected")
	if _peer != null:
		if _peer.get_status() != StreamPeerTCP.STATUS_CONNECTED:
			_peer = null
			_rx = ""
			print("SIM-PEER closed")
		else:
			_drain()
	# Clavier (debug) : fleches = yaw/pitch, R = neutre. Relatif ecran.
	_keyboard_move(delta)
	# Lissage vers la cible.
	var k: float = minf(1.0, SMOOTH * delta)
	if _track_active:
		_track_t += delta * 1000.0
		_step_track()
	for axis in ["yaw", "pitch", "roll"]:
		_current[axis] = lerpf(float(_current[axis]), float(_target[axis]), k)
	_apply_pose()


func _drain() -> void:
	var n: int = _peer.get_available_bytes()
	if n <= 0:
		return
	var data: PackedByteArray = _peer.get_partial_data(n)[1]
	_rx += data.get_string_from_utf8()
	while true:
		var i: int = _rx.find("\n")
		if i < 0:
			break
		var line: String = _rx.substr(0, i).strip_edges()
		_rx = _rx.substr(i + 1)
		if not line.is_empty():
			_handle_line(line)


func _handle_line(line: String) -> void:
	var msg: Variant = JSON.parse_string(line)
	if typeof(msg) != TYPE_DICTIONARY:
		push_error("SIM-BADLINE %s" % line.left(200))
		_send({"ok": false, "error": "JSON objet attendu"})
		return
	if msg.has("ping"):
		_send({"ok": true, "pong": true, "state": _state()})
		return
	var applied := {}
	for axis in ["yaw", "pitch", "roll"]:
		if msg.has(axis):
			var v := clampf(float(msg[axis]), LIMITS[axis][0], LIMITS[axis][1])
			if axis == "roll":
				v = 0.0  # BOM-budget 2 axes : roll ignore, protocole inchange.
			_target[axis] = v
			applied[axis] = v
	if not applied.is_empty():
		_track_active = false
		print("SIM-CMD %s" % JSON.stringify(applied))
		_send({"ok": true, "state": _state()})
		return
	if msg.has("moves"):
		if _load_track(msg["moves"]):
			_track_active = true
			_track_t = 0.0
			print("SIM-TRACK %d keys" % _track.size())
			_send({"ok": true, "track_accepted": _track.size(), "state": _state()})
		else:
			_send({"ok": false, "error": "piste invalide (liste de {yaw?,pitch?,roll?,t} avec t croissant)"})
		return
	_send({"ok": false, "error": "rien a appliquer (yaw/pitch/roll/moves ?)"})


func _load_track(raw: Variant) -> bool:
	if typeof(raw) != TYPE_DICTIONARY and typeof(raw) != TYPE_ARRAY:
		return false
	if typeof(raw) != TYPE_ARRAY or (raw as Array).is_empty():
		return false
	var keys: Array = []
	var last_t := -1.0
	for k in raw:
		if typeof(k) != TYPE_DICTIONARY or not (k as Dictionary).has("t"):
			return false
		var t := float((k as Dictionary)["t"])
		if t < last_t:
			return false
		last_t = t
		var pose := {}
		for axis in ["yaw", "pitch", "roll"]:
			if axis == "roll":
				pose[axis] = 0.0  # BOM-budget 2 axes.
			elif (k as Dictionary).has(axis):
				pose[axis] = clampf(float((k as Dictionary)[axis]), LIMITS[axis][0], LIMITS[axis][1])
			else:
				pose[axis] = float(_target[axis])
		keys.append({"t": t, "pose": pose})
	_track = keys
	return true


func _step_track() -> void:
	if _track.is_empty():
		_track_active = false
		return
	var i := 0
	while i < _track.size() - 1 and float(_track[i + 1]["t"]) <= _track_t:
		i += 1
	if i >= _track.size() - 1 and _track_t >= float(_track[_track.size() - 1]["t"]):
		for axis in ["yaw", "pitch", "roll"]:
			_target[axis] = float(_track[_track.size() - 1]["pose"][axis])
		_track_active = false
		return
	var a: Dictionary = _track[i]
	var b: Dictionary = _track[i + 1]
	var span: float = maxf(1.0, float(b["t"]) - float(a["t"]))
	var f: float = clampf((_track_t - float(a["t"])) / span, 0.0, 1.0)
	for axis in ["yaw", "pitch", "roll"]:
		_target[axis] = lerpf(float(a["pose"][axis]), float(b["pose"][axis]), f)


func _state() -> Dictionary:
	return {
		"yaw": snappedf(float(_target["yaw"]), 0.1),
		"pitch": snappedf(float(_target["pitch"]), 0.1),
		"roll": snappedf(float(_target["roll"]), 0.1),
	}


const KEY_SPEED := 30.0  # deg/s aux fleches (fenetre Godot au premier plan requise)
var _key_last_print := 0


func _keyboard_move(delta: float) -> void:
	var dx := 0.0
	var dy := 0.0
	if Input.is_key_pressed(KEY_LEFT):
		dx -= 1.0
	if Input.is_key_pressed(KEY_RIGHT):
		dx += 1.0
	if Input.is_key_pressed(KEY_UP):
		dy += 1.0
	if Input.is_key_pressed(KEY_DOWN):
		dy -= 1.0
	if Input.is_key_pressed(KEY_R):
		_target["yaw"] = 0.0
		_target["pitch"] = 0.0
		_target["roll"] = 0.0
		_track_active = false
	elif dx != 0.0 or dy != 0.0:
		_track_active = false
		_target["yaw"] = clampf(float(_target["yaw"]) + dx * KEY_SPEED * delta, LIMITS["yaw"][0], LIMITS["yaw"][1])
		_target["pitch"] = clampf(float(_target["pitch"]) + dy * KEY_SPEED * delta, LIMITS["pitch"][0], LIMITS["pitch"][1])
	else:
		return
	var now_ms: int = Time.get_ticks_msec()
	if now_ms - _key_last_print > 500:
		_key_last_print = now_ms
		print("SIM-KEY yaw=%.1f pitch=%.1f" % [float(_target["yaw"]), float(_target["pitch"])])


func _apply_pose() -> void:
	if not is_node_ready():
		return
	_pan_pivot.rotation.y = deg_to_rad(float(_current["yaw"]))
	# Ecran face -Z (modele kit reel) : pitch>0 = lever la tete = rotation.x positive.
	_tilt_pivot.rotation.x = deg_to_rad(float(_current["pitch"]))
# 2 axes : pas de roll, le protocole reste inchange (roll force a 0).


func _send(payload: Dictionary) -> void:
	if _peer != null and _peer.get_status() == StreamPeerTCP.STATUS_CONNECTED:
		_peer.put_data((JSON.stringify(payload) + "\n").to_utf8_buffer())
