import bpy
from mathutils import Vector
bpy.context.view_layer.update()

SOLIDS_B = {"Cube": "USocle", "Cube.003": "UFlasque", "Cube.004": "ULienR",
            "Cube.007": "ULienL", "Cube.005": "UEarR", "Cube.008": "UEarL",
            "Cube.006": "UBrasR", "Cube.009": "UBrasL"}
SOLIDS_A = {"fond plaque": "LUSocle", "Cube.001": "LUBrasR", "Cube.002": "LUBrasL"}
SKIP = {"Cylinder", "Cylinder.001"}  # disques palonnier, deja en fiche

def wbox(o):
    ws = [o.matrix_world @ Vector(v) for v in o.bound_box]
    xs = [v.x * 1000 for v in ws]; ys = [v.y * 1000 for v in ws]; zs = [v.z * 1000 for v in ws]
    return (min(xs), max(xs), min(ys), max(ys), min(zs), max(zs))

def conv_set(solids_map, fname, funcname, comment):
    solids = {}
    for n, newn in solids_map.items():
        o = bpy.data.objects.get(n)
        if o and o.type == "MESH":
            solids[n] = (newn, wbox(o))
    cutters = []
    for o in bpy.data.objects:
        if o.type != "MESH" or o.name in solids or o.name in SOLIDS_B or o.name in SOLIDS_A or o.name in SKIP:
            continue
        if "persage" not in o.name.lower():
            continue
        bb = wbox(o)
        cx = (bb[0] + bb[1]) / 2; cy = (bb[2] + bb[3]) / 2; cz = (bb[4] + bb[5]) / 2
        dx, dy, dz = bb[1] - bb[0], bb[3] - bb[2], bb[5] - bb[4]
        hits = []
        for n, (newn, s) in solids.items():
            if s[0] - 1e-6 <= cx <= s[1] + 1e-6 and s[2] - 1e-6 <= cy <= s[3] + 1e-6 and s[4] - 1e-6 <= cz <= s[5] + 1e-6:
                hits.append((abs(s[1] - s[0]) * abs(s[3] - s[2]) * abs(s[5] - s[4]), newn))
        tgt = min(hits)[1] if hits else "?"
        if tgt == "?":
            continue
        cutters.append((o.name, tgt, cx, cy, cz, dx, dy, dz))
    # recentrage : origine = centre du socle (1er solide), Z = dessous
    anchor = list(solids.values())[0][1]
    ox, oy, oz = (anchor[0] + anchor[1]) / 2, (anchor[2] + anchor[3]) / 2, anchor[4]
    L = ["def %s():" % funcname, "    # %s" % comment, "    o = []"]
    for n, (newn, s) in solids.items():
        L.append('    o.append(box("%s", (%.3f, %.3f, %.3f), (%.3f, %.3f, %.3f), m_kit))'
                 % (newn, (s[1] - s[0]) / 1000, (s[3] - s[2]) / 1000, (s[5] - s[4]) / 1000,
                    ((s[0] + s[1]) / 2 - ox) / 1000, ((s[2] + s[3]) / 2 - oy) / 1000, ((s[4] + s[5]) / 2 - oz) / 1000))
    for n, tgt, cx, cy, cz, dx, dy, dz in cutters:
        r = min([d for d in (dx, dy, dz) if d > 0.01] or [1]) / 2
        if dz >= dx and dz >= dy:
            L.append('    o.append(zhole(%.3f, %.3f, %.3f, %.4f))  # %s -> %s'
                     % ((cx - ox) / 1000, (cy - oy) / 1000, (cz - oz) / 1000, r / 1000, n, tgt))
        elif dy >= dx and dy >= dz:
            L.append('    o.append(yhole(%.3f, %.3f, %.3f, %.4f))  # %s -> %s'
                     % ((cx - ox) / 1000, (cy - oy) / 1000, (cz - oz) / 1000, r / 1000, n, tgt))
        else:
            L.append('    o.append(xhole(%.3f, %.3f, %.3f, %.4f))  # %s -> %s'
                     % ((cx - ox) / 1000, (cy - oy) / 1000, (cz - oz) / 1000, r / 1000, n, tgt))
    L.append("    return o\n")
    print("FUNC-START %s" % funcname)
    print("\n".join(L))
    print("FUNC-END %s" % funcname)

conv_set(SOLIDS_B, None, "build_ucourt_user", "U court B07PQ12TXS converti de Robot.blend (cotes utilisateur, pas a l'echelle)")
conv_set(SOLIDS_A, None, "build_ulong_user", "Grand U B07PQ12TXS converti de Robot.blend (cotes utilisateur, pas a l'echelle)")
print("CONV-OK")
