"""Fiches de controle par piece: 3 vues ortho (face/dessus/gauche) par piece meca.
Usage: Blender 5.2 --background --python fiches_pieces.py
Sorties: scratch/vues/<piece>_{face,dessus,gauche}.png (fond transparent).
Compositing + cotes: compo_fiches.py (python systeme + PIL).
Cotes = modele (a valider au pied a coulisse sur pieces recues).
"""
import bpy, math, os
from mathutils import Vector

_ONLY = os.environ.get("ONLY", "")

OUT = r"C:/Users/mathias/AppData/Local/hermes/cache/scratch/vues"
os.makedirs(OUT, exist_ok=True)

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for col in (bpy.data.meshes, bpy.data.materials):
    for x in list(col):
        col.remove(x)

def mat(name, color, metallic=0.0, rough=0.6, emission=None, estr=1.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = rough
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = estr
    return m

m_pan = mat("PanRed", (0.9, 0.12, 0.12), 0.1, 0.5)
m_tilt = mat("TiltOrange", (1.0, 0.5, 0.0), 0.1, 0.5)
m_kit = mat("KitGreen", (0.1, 0.7, 0.25), 0.3, 0.5)
m_horn = mat("HornYellow", (1.0, 0.9, 0.1), 0.2, 0.5)
m_plot = mat("PlotPurple", (0.65, 0.2, 0.9), 0.1, 0.5)
m_sup = mat("SupportCyan", (0.1, 0.8, 0.9), 0.1, 0.5)
m_brass = mat("Brass", (0.85, 0.65, 0.2), 0.9, 0.35)
m_tablet = mat("TabletBody", (0.05, 0.05, 0.06), 0.2, 0.5)
m_screen = mat("Screen", (0.02, 0.10, 0.18), 0.0, 0.4, (0.1, 0.55, 1.0), 2.0)
m_wood = mat("Wood", (0.55, 0.38, 0.20), 0.0, 0.85)
m_hole = mat("Hole", (0.02, 0.02, 0.02), 0.0, 1.0)
m_steel = mat("Steel", (0.8, 0.8, 0.85), 0.9, 0.3)

def box(name, dims, loc, material, rot=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.active_object
    o.name = name
    o.dimensions = dims
    if rot is not None:
        o.rotation_euler = rot
    o.data.materials.append(material)
    return o

def cyl(name, r, h, loc, material, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=h, location=loc, vertices=48)
    o = bpy.context.active_object
    o.name = name
    o.rotation_euler = rot
    o.data.materials.append(material)
    return o

def zhole(x, y, z, r, h=0.006):
    return cyl("hole", r, h, (x, y, z), m_hole)

def sph(name, r, loc, material):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=loc, segments=24, ring_count=16)
    o = bpy.context.active_object
    o.name = name
    o.data.materials.append(material)
    return o

def xhole(x, y, z, r, h=0.004):
    return cyl("hole", r, h, (x, y, z), m_hole, rot=(0, math.radians(90), 0))

pieces = {}

def build_pan():
    o = []
    o.append(box("PanBody", (0.0407, 0.0197, 0.036), (0, 0, 0), m_pan))
    o.append(box("PanEarL", (0.007, 0.0197, 0.004), (-0.0238, 0, 0.016), m_pan))
    o.append(box("PanEarR", (0.007, 0.0197, 0.004), (0.0238, 0, 0.016), m_pan))
    for sx in (-0.0238, 0.0238):
        for sy in (-0.006, 0.006):
            o.append(zhole(sx, sy, 0.016, 0.0016))
    o.append(cyl("PanShaft", 0.003, 0.006, (0.0032, 0, 0.021), m_steel))
    o.append(cyl("PanHornF", 0.00975, 0.003, (0.0032, 0, 0.0255), m_pan))
    return o

def build_tilt():
    # RDS3218 rework atelier (assemblage_2axes.blend) : arbre cote +X, a -8 du centre.
    # Corps 40 x 20 x 40 + arbre r1.5 + palonnier O19.5 ep3. Pas d'idler.
    o = []
    o.append(box("TiltBody", (0.040, 0.020, 0.040), (0, 0, 0), m_tilt))
    o.append(cyl("TiltShaft", 0.0015, 0.014, (0.0183, 0, -0.008), m_steel, rot=(0, math.radians(90), 0)))
    o.append(cyl("TiltHornU", 0.00975, 0.003, (0.0264, 0, -0.008), m_horn, rot=(0, math.radians(90), 0)))
    return o

def build_rds_haut():
    # Chape haute RDS3218.blend : plaque 55.6 x 20 ep2 + 2 joues 31.5.
    # Origine = centre du corps servo (meme repere que build_tilt).
    o = []
    o.append(box("RDSHautPlaque", (0.0556, 0.020, 0.002), (0, 0, 0.031), m_tilt))
    for sx in (-0.0288, 0.0288):
        o.append(box("RDSHautJoue", (0.002, 0.020, 0.0315), (sx, 0, 0.01625), m_tilt))
    return o

def build_rds_bas():
    # Chape basse RDS3218.blend : plaque 44.5 x 20 ep2 + 2 joues 27.
    # Origine = centre du corps servo (meme repere que build_tilt).
    o = []
    o.append(box("RDSBasPlaque", (0.0445, 0.020, 0.002), (0, 0, -0.030), m_tilt))
    for sx in (-0.02325, 0.02325):
        o.append(box("RDSBasJoue", (0.002, 0.020, 0.027), (sx, 0, -0.0175), m_tilt))
    return o

def yhole(x, y, z, r, h=0.004):
    return cyl("hole", r, h, (x, y, z), m_hole, rot=(math.radians(90), 0, 0))

    return o

    return o

    return o

def build_ucourt():
    # U atelier, cotation exacte du groupe u_court_48, rotation -90 X incluse
    # (repere viewport/assemblage). Plaque verticale 40x42 + plancher 40x20
    # + 2 bandes + 2 bras.
    _RX = (math.radians(-90), 0, 0)
    o = []
    o.append(box("UBaseCourt", (0.040, 0.042, 0.002), (0.000, 0.001, -0.021), m_kit, rot=_RX))
    o.append(box("UFlasque", (0.040, 0.002, 0.020), (0.000, 0.012, -0.020), m_kit, rot=_RX))
    o.append(box("UBande", (0.008, 0.028, 0.002), (-0.024, 0.001, 0.000), m_kit, rot=_RX))
    o.append(box("UBande.001", (0.008, 0.028, 0.002), (0.024, 0.001, 0.000), m_kit, rot=_RX))
    o.append(box("UArmL", (0.008, 0.002, 0.020), (-0.024, 0.012, 0.013), m_kit, rot=_RX))
    o.append(box("UArmR", (0.008, 0.002, 0.020), (0.024, 0.012, 0.013), m_kit, rot=_RX))
    return o

def build_horn_pan():
    # (retire : palonnier integre a la fiche pan_mg996r)
    return []

def build_support():
    # Pince X a ressort 4.7x3.1in (119x79 ferme) : corps X + 4 crochets,
    # pad avant, coulisseau haut, embase + taraudage 1/4 en bas. Position
    # montee (ouverte sur tablette 130).
    o = []
    o.append(box("SupDos", (0.056, 0.008, 0.080), (0, 0, 0), m_sup))
    A = 0.5256
    for sx, sz, ra in ((0.0198, 0.034, A), (0.0198, -0.034, 3.1416 - A),
                       (-0.0198, 0.034, -A), (-0.0198, -0.034, -(3.1416 - A))):
        o.append(box("SupBras", (0.018, 0.007, 0.080), (sx, 0, sz), m_sup, rot=(0, ra, 0)))
    for sx in (-0.0395, 0.0395):
        for sz in (-0.068, 0.068):
            o.append(box("Crochet", (0.014, 0.008, 0.018), (sx, 0.004, sz), m_sup))
            lz = sz - 0.005 if sz > 0 else sz + 0.005
            o.append(box("Levre", (0.014, 0.018, 0.007), (sx, 0.009, lz), m_sup))
    o.append(box("SupPad", (0.046, 0.005, 0.062), (0, 0.0065, 0), m_sup))
    o.append(box("Coulisseau", (0.040, 0.006, 0.070), (0, -0.014, 0.028), m_sup))
    # Rotule centrale + base 1/4 (dos reel) : chaine X -> rotule -> base.
    o.append(box("ColHaut", (0.020, 0.018, 0.030), (0, -0.011, -0.045), m_sup))
    o.append(sph("Rotule", 0.011, (0, -0.015, -0.050), m_sup))
    o.append(cyl("VisRotule", 0.004, 0.012, (0.014, -0.015, -0.050), m_steel,
                 rot=(0, math.radians(90), 0)))
    o.append(cyl("Col14", 0.006, 0.014, (0, -0.015, -0.065), m_sup))
    o.append(cyl("Base14", 0.016, 0.010, (0, -0.015, -0.074), m_sup))
    o.append(cyl("Insert14", 0.005, 0.004, (0, -0.015, -0.072), m_steel))
    o.append(zhole(0, -0.015, -0.074, 0.0032, 0.016))
    return o

def build_tablette():
    o = []
    o.append(box("Tablette", (0.216, 0.009, 0.130), (0, 0, 0), m_tablet))
    o.append(box("Ecran", (0.196, 0.002, 0.110), (0, 0.0055, 0), m_screen))
    return o

def build_base():
    return [box("Base", (0.20, 0.20, 0.018), (0, 0, 0), m_wood)]

BUILDERS = {
    "pan_mg996r": build_pan,
    "tilt_ds3218": build_tilt,
    "rds_haut": build_rds_haut,
    "rds_bas": build_rds_bas,
    "u_court_48": build_ucourt,
    "support_pince": build_support,
    "tablette": build_tablette,
    "base_bois": build_base,
}

# etendues max par piece et par vue (m) pour cadrage ortho
EXT = {
    "pan_mg996r": {"face": 0.055, "dessus": 0.055, "gauche": 0.042},
    "tilt_ds3218": {"face": 0.062, "dessus": 0.062, "gauche": 0.042},
    "rds_haut": {"face": 0.058, "dessus": 0.058, "gauche": 0.034},
    "rds_bas": {"face": 0.047, "dessus": 0.047, "gauche": 0.034},
    "u_court_48": {"face": 0.060, "dessus": 0.058, "gauche": 0.046},
    "support_pince": {"face": 0.156, "dessus": 0.085, "gauche": 0.156},
    "tablette": {"face": 0.216, "dessus": 0.216, "gauche": 0.130},
    "base_bois": {"face": 0.20, "dessus": 0.20, "gauche": 0.20},
}

all_objs = {}
for name, fn in BUILDERS.items():
    all_objs[name] = fn()

# lumieres
sun = bpy.data.lights.new("Sun", 'SUN')
sun.energy = 3.0
so = bpy.data.objects.new("Sun", sun)
bpy.context.collection.objects.link(so)
so.rotation_euler = (math.radians(50), 0, math.radians(30))
fill = bpy.data.lights.new("Fill", 'SUN')
fill.energy = 0.8
fo = bpy.data.objects.new("Fill", fill)
bpy.context.collection.objects.link(fo)
fo.rotation_euler = (math.radians(30), 0, math.radians(-120))

# camera ortho unique
cam = bpy.data.cameras.new("OrthoCam")
cam.type = 'ORTHO'
co = bpy.data.objects.new("OrthoCam", cam)
bpy.context.collection.objects.link(co)
bpy.context.scene.camera = co

bpy.context.scene.render.engine = 'CYCLES'
bpy.context.scene.cycles.device = 'CPU'
bpy.context.scene.cycles.samples = 16
bpy.context.scene.cycles.use_denoising = False
bpy.context.scene.render.film_transparent = True
bpy.context.scene.render.resolution_x = 800
bpy.context.scene.render.resolution_y = 600
bpy.context.scene.render.image_settings.file_format = 'PNG'

CAMS = {
    "face": (0, 2, 0),
    "dessus": (0, 0, 2),
    "gauche": (-2, 0, 0),
}

flat = [o for lst in all_objs.values() for o in lst]

def apply_boolean_cuts(objs):
    # Les objets materiau "Hole" deviennent de vrais percages (booleen DIFFERENCE),
    # puis sont supprimes. Cible = plus petite piece dont la bbox contient le foret.
    parts, cuts = [], []
    for o in objs:
        mats = [m.name for m in o.data.materials] if o.data and hasattr(o.data, "materials") else []
        (cuts if ("Hole" in mats and o.type == "MESH") else parts).append(o)
    if not cuts:
        return
    bpy.ops.object.select_all(action="DESELECT")
    for c in cuts:
        c.scale[2] *= 4.0
        cloc = c.location
        hits = []
        for p in parts:
            if p.type != "MESH":
                continue
            ws = [p.matrix_world @ Vector(v) for v in p.bound_box]
            xs = [v.x for v in ws]; ys = [v.y for v in ws]; zs = [v.z for v in ws]
            if (min(xs) - 1e-6 <= cloc.x <= max(xs) + 1e-6
                    and min(ys) - 1e-6 <= cloc.y <= max(ys) + 1e-6
                    and min(zs) - 1e-6 <= cloc.z <= max(zs) + 1e-6):
                vol = (max(xs) - min(xs)) * (max(ys) - min(ys)) * (max(zs) - min(zs))
                hits.append((vol, p))
        if not hits:
            # Repli : piece la plus proche du foret (evite les pastilles orphelines).
            def _d(p):
                ws = [p.matrix_world @ Vector(v) for v in p.bound_box]
                cx = sum(v.x for v in ws) / 8; cy = sum(v.y for v in ws) / 8; cz = sum(v.z for v in ws) / 8
                return abs(cx - cloc.x) + abs(cy - cloc.y) + abs(cz - cloc.z)
            cands = [p for p in parts if p.type == "MESH"]
            if not cands:
                continue
            hits = [(0, min(cands, key=_d))]
        for _, best in sorted(hits, key=lambda h: h[0], reverse=True):
            bpy.context.view_layer.objects.active = best
            mod = best.modifiers.new("Cut", "BOOLEAN")
            mod.operation = "DIFFERENCE"
            mod.object = c
            bpy.ops.object.modifier_apply(modifier=mod.name)
    for c in cuts:
        if c in objs:
            objs.remove(c)
    bpy.context.view_layer.update()

def aim(pos, target, view=None):
    if view == "dessus":
        co.location = (target[0], target[1], pos[2])
        co.rotation_euler = (0, 0, 0)
        return
    co.location = pos
    tgt = Vector((target[0] - pos[0], target[1] - pos[1], target[2] - pos[2]))
    co.rotation_euler = tgt.to_track_quat('-Z', 'Y').to_euler()

def piece_center(objs):
    pts = []
    for o in objs:
        if o.type != "MESH":
            continue
        pts += [o.matrix_world @ Vector(v) for v in o.bound_box]
    if not pts:
        return (0, 0, 0)
    xs = [v.x for v in pts]; ys = [v.y for v in pts]; zs = [v.z for v in pts]
    return ((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, (min(zs) + max(zs)) / 2)

clay = mat("ClayWhite", (0, 0, 0), 0.0, 1.0, (1, 1, 1), 5.0)
mats_saved = {}

sc = bpy.context.scene
sc.render.use_freestyle = False
vt_default = sc.view_settings.view_transform
_fls = sc.view_layers[0].freestyle_settings.linesets.new("Tech")
_fls.select_silhouette = True
_fls.select_border = True
_fls.select_crease = True
_fls.linestyle.color = (0.05, 0.05, 0.05)
_fls.linestyle.thickness = 1.5

for name, objs in all_objs.items():
    if _ONLY and name != _ONLY:
        continue
    apply_boolean_cuts(objs)
    for o in flat:
        o.hide_render = (o not in objs)
    ctr = piece_center(objs)
    for o in objs:
        if o.type != "MESH":
            continue
        mats_saved[o] = [m for m in o.data.materials]
        o.data.materials.clear()
        o.data.materials.append(clay)
    sc.render.use_freestyle = True
    sc.view_settings.view_transform = "Standard"
    for view, pos in CAMS.items():
        aim(pos, ctr, view)
        cam.ortho_scale = EXT[name][view] * 1.8
        bpy.context.scene.render.filepath = f"{OUT}/{name}_{view}.png"
        bpy.ops.render.render(write_still=True)
        print(f"VUE-OK {name}_{view}")
    sc.render.use_freestyle = False
    sc.view_settings.view_transform = vt_default
    for o, _mats in mats_saved.items():
        o.data.materials.clear()
        for _m in _mats:
            o.data.materials.append(_m)
    mats_saved.clear()
    # iso 3/4 couleurs (materiaux d'origine restaures)
    aim((1.2, 1.2, 1.0), ctr)
    cam.ortho_scale = max(EXT[name].values()) * 1.8
    bpy.context.scene.render.filepath = f"{OUT}/{name}_iso.png"
    bpy.ops.render.render(write_still=True)
    print(f"ISO-OK {name}")

print("FICHES-OK")
