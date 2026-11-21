// Stub du serveur cou (issue #28) : parle le contrat TCP de
// src/Simulator3D/PROTOCOL.md, affiche les ordres reçus, répond comme le
// simu. Pas de PCA9685 pour l'instant : validation du tuyau uniquement.
package main

import (
	"bufio"
	"encoding/json"
	"flag"
	"fmt"
	"net"
	"os"
	"time"
)

type pose struct {
	Yaw   float64 `json:"yaw"`
	Pitch float64 `json:"pitch"`
	Roll  float64 `json:"roll"`
}

type limits struct{ Min, Max float64 }

var axisLimits = map[string]limits{
	"yaw":   {-90, 90},
	"pitch": {-30, 45},
	"roll":  {-30, 30},
}

func clamp(axis string, v float64) float64 {
	l := axisLimits[axis]
	if v < l.Min {
		return l.Min
	}
	if v > l.Max {
		return l.Max
	}
	return v
}

func stateJSON(yaw, pitch, roll float64) string {
	return fmt.Sprintf(`{"yaw": %.1f, "pitch": %.1f, "roll": %.1f}`, yaw, pitch, roll)
}

func log(format string, args ...any) {
	fmt.Printf("[%s] %s\n", time.Now().Format("15:04:05"), fmt.Sprintf(format, args...))
}

func main() {
	port := flag.Int("port", 9080, "port TCP")
	flag.Parse()
	addr := fmt.Sprintf("0.0.0.0:%d", *port)
	ln, err := net.Listen("tcp", addr)
	if err != nil {
		fmt.Fprintln(os.Stderr, "listen:", err)
		os.Exit(1)
	}
	log("SERVO-STUB prêt sur tcp://%s (Ctrl+C pour arrêter)", addr)
	for {
		conn, err := ln.Accept()
		if err != nil {
			log("accept: %v", err)
			continue
		}
		go handle(conn)
	}
}

func handle(conn net.Conn) {
	defer conn.Close()
	remote := conn.RemoteAddr().String()
	log("client connecté : %s", remote)
	target := pose{}
	scan := bufio.NewScanner(conn)
	scan.Buffer(make([]byte, 64*1024), 64*1024)
	for scan.Scan() {
		line := scan.Text()
		var msg map[string]any
		if err := json.Unmarshal([]byte(line), &msg); err != nil {
			log("<- %s : JSON invalide (%v)", remote, err)
			reply(conn, `{"ok": false, "error": "JSON objet attendu"}`)
			continue
		}
		if _, ok := msg["ping"]; ok {
			log("<- %s : ping", remote)
			reply(conn, `{"ok": true, "pong": true, "state": `+stateJSON(target.Yaw, target.Pitch, target.Roll)+`}`)
			continue
		}
		applied := false
		for _, axis := range []string{"yaw", "pitch", "roll"} {
			if v, ok := num(msg[axis]); ok {
				setAxis(&target, axis, clamp(axis, v))
				applied = true
			}
		}
		if applied {
			log("<- %s : pose yaw=%.1f pitch=%.1f roll=%.1f", remote, target.Yaw, target.Pitch, target.Roll)
			reply(conn, `{"ok": true, "state": `+stateJSON(target.Yaw, target.Pitch, target.Roll)+`}`)
			continue
		}
		if raw, ok := msg["moves"]; ok {
			n, errMsg := checkTrack(raw)
			if errMsg == "" {
				log("<- %s : piste %d keyframes", remote, n)
				reply(conn, fmt.Sprintf(`{"ok": true, "track_accepted": %d, "state": %s}`, n, stateJSON(target.Yaw, target.Pitch, target.Roll)))
			} else {
				log("<- %s : piste invalide (%s)", remote, errMsg)
				reply(conn, `{"ok": false, "error": "piste invalide (liste de {yaw?,pitch?,roll?,t} avec t croissant)"}`)
			}
			continue
		}
		log("<- %s : rien à appliquer", remote)
		reply(conn, `{"ok": false, "error": "rien a appliquer (yaw/pitch/roll/moves ?)"}`)
	}
	log("client parti : %s", remote)
}

func num(v any) (float64, bool) {
	f, ok := v.(float64)
	return f, ok
}

func setAxis(p *pose, axis string, v float64) {
	switch axis {
	case "yaw":
		p.Yaw = v
	case "pitch":
		p.Pitch = v
	case "roll":
		p.Roll = v
	}
}

func checkTrack(raw any) (int, string) {
	list, ok := raw.([]any)
	if !ok || len(list) == 0 {
		return 0, "pas une liste"
	}
	last := -1.0
	for _, k := range list {
		m, ok := k.(map[string]any)
		if !ok {
			return 0, "keyframe non objet"
		}
		t, ok := num(m["t"])
		if !ok {
			return 0, "keyframe sans t"
		}
		if t < last {
			return 0, "t non croissant"
		}
		last = t
	}
	return len(list), ""
}

func reply(conn net.Conn, payload string) {
	fmt.Fprintln(conn, payload)
}
