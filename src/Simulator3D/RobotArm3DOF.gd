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

@onready var _pan_pivot: Node3D = $PanPivot
@onready var _tilt_pivot: Node3D = $PanPivot/TiltPivot
@onready var _roll_pivot: Node3D = $PanPivot/TiltPivot/RollPivot


func _ready() -> void:
	_apply_pose()
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
			if (k as Dictionary).has(axis):
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


func _apply_pose() -> void:
	if not is_node_ready():
		return
	_pan_pivot.rotation.y = deg_to_rad(float(_current["yaw"]))
	_tilt_pivot.rotation.x = deg_to_rad(-float(_current["pitch"]))
	_roll_pivot.rotation.z = deg_to_rad(float(_current["roll"]))


func _send(payload: Dictionary) -> void:
	if _peer != null and _peer.get_status() == StreamPeerTCP.STATUS_CONNECTED:
		_peer.put_data((JSON.stringify(payload) + "\n").to_utf8_buffer())
