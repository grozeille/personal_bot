"""Fiches de controle rendues DIRECTEMENT depuis assemblage_2axes.blend.
Usage: Blender 5.2 --background assemblage_2axes.blend --python fiches_from_blend.py
Sorties: scratch/vues/<piece>_{face,dessus,gauche,iso}.png (fond transparent).
Mapping piece -> objets (noms exacts du fichier atelier).
"""
import bpy, math, os
from mathutils import Vector

_ONLY = os.environ.get("ONLY", "")

OUT = r"C:/Users/mathias/AppData/Local/hermes/cache/scratch/vues"
os.makedirs(OUT, exist_ok=True)

# fiche -> noms d'objets du .blend (groupes suivant les parents atelier)
FICHES = {
    "base_bois": ["BaseBois"],
    "u_court_48": ["UBaseCourt", "UBande", "UBande.001",
                   "UArmL", "UArmR", "UFlasque"],
    "pan_mg996r": ["PanBody", "PanEarL", "PanEarR", "PanArbre", "PanHorn"],
    "tilt_ds3218": ["TiltBody", "TiltArbre", "TiltHorn"],
    "rds_haut": ["RDSHautPlaque", "RDSHautJoue", "RDSHautJoue.001"],
    "rds_bas": ["RDSBasPlaque", "RDSBasJoue", "RDSBasJoue.001"],
    "support_pince": ["Base14", "Insert14", "Col14", "Rotule", "VisRotule",
                      "ColHaut", "Coulisseau", "PinceDos", "PincePad",
                      "PinceBras", "PinceBras.001", "PinceBras.002", "PinceBras.003",
                      "Crochet", "Crochet.001", "Crochet.002", "Crochet.003",
                      "Levre", "Levre.001", "Levre.002", "Levre.003"],
    "tablette": ["Tablette", "Ecran"],
}

# cadrage ortho par piece et par vue (m)
EXT = {
    "pan_mg996r": {"face": 0.055, "dessus": 0.055, "gauche": 0.042},
    "tilt_ds3218": {"face": 0.062, "dessus": 0.062, "gauche": 0.042},
    "rds_haut": {"face": 0.058, "dessus": 0.058, "gauche": 0.034},
    "rds_bas": {"face": 0.047, "dessus": 0.047, "gauche": 0.034},
    "u_court_48": {"face": 0.044, "dessus": 0.058, "gauche": 0.046},
    "support_pince": {"face": 0.156, "dessus": 0.085, "gauche": 0.156},
    "tablette": {"face": 0.216, "dessus": 0.216, "gauche": 0.130},
    "base_bois": {"face": 0.20, "dessus": 0.20, "gauche": 0.20},
}


def mat(name, color, metallic, roughness, emission=None, estrength=0.0):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission is not None:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = estrength
    return m


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
    "face": (0, -2, 0),
    "dessus": (0, 0, 2),
    "gauche": (2, 0, 0),
}

all_meshes = [o for o in bpy.data.objects if o.type == "MESH"]


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

missing = []
for name, obj_names in FICHES.items():
    if _ONLY and name != _ONLY:
        continue
    objs = []
    for oname in obj_names:
        o = bpy.data.objects.get(oname)
        if o is None:
            missing.append((name, oname))
            continue
        objs.append(o)
    if not objs:
        print(f"SKIP {name} (rien trouve)")
        continue
    for o in all_meshes:
        o.hide_render = (o not in objs)
    ctr = piece_center(objs)
    for o in objs:
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

if missing:
    print("MANQUANTS:", missing)
print("FICHES-OK")
