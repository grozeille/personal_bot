"""Fiches de controle par piece: 3 vues ortho (face/dessus/gauche) par piece meca.
Usage: Blender 5.2 --background --python fiches_pieces.py
Sorties: scratch/vues/<piece>_{face,dessus,gauche}.png (fond transparent).
Compositing + cotes: compo_fiches.py (python systeme + PIL).
Cotes = modele (a valider au pied a coulisse sur pieces recues).
"""
import bpy, math, os
from mathutils import Vector

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
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=h, location=loc, vertices=20)
    o = bpy.context.active_object
    o.name = name
    o.rotation_euler = rot
    o.data.materials.append(material)
    return o

def zhole(x, y, z, r, h=0.006):
    return cyl("hole", r, h, (x, y, z), m_hole)

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
    o.append(cyl("PanShaft", 0.003, 0.006, (0.0032, 0, 0.021), m_hole))
    return o

def build_tilt():
    # Config photo: R_y(-90), arbre axe X cote -X, corps X36.6/Y20/Z40.3,
    # arbre decale +3.2 en Z (standard), oreilles haut/bas.
    o = []
    o.append(box("TiltBody", (0.0366, 0.020, 0.0403), (0, 0, 0), m_tilt))
    o.append(cyl("TiltShaft", 0.003, 0.010, (-0.0233, 0, 0.00325), m_hole, rot=(0, math.radians(90), 0)))
    o.append(cyl("TiltHornF", 0.012, 0.003, (-0.030, 0, 0.00325), m_horn, rot=(0, math.radians(90), 0)))
    for sz in (0.0235, -0.0235):
        o.append(box("Ear", (0.003, 0.013, 0.007), (0.010, 0, sz), m_tilt))
        for sy in (-0.005, 0.005):
            o.append(xhole(0.010, sy, sz, 0.0016))
    return o

def build_bracket():
    # photo2: plateau ~60x60 (A-VALIDER) + 2 pattes, trous pattes entraxe 48, 10mm vertical
    o = []
    o.append(box("Bracket", (0.060, 0.060, 0.002), (0, 0, 0), m_kit))
    o.append(zhole(0, 0, 0, 0.004))
    for sx in (-0.012, 0.012):
        for sy in (-0.012, 0.012):
            o.append(zhole(sx, sy, 0, 0.0015))
    for sx in (-0.022, 0.022):
        o.append(zhole(sx, 0, 0, 0.0015))
        o.append(zhole(0, sx, 0, 0.0015))
    for sx in (-0.024, 0.024):
        o.append(box("Tab", (0.002, 0.020, 0.022), (sx, 0, 0.012), m_kit))
        o.append(xhole(sx, 0, 0.011, 0.0015))
        o.append(xhole(sx, 0, 0.021, 0.0015))
    return o

def build_plateau():
    o = []
    o.append(box("Plateau", (0.062, 0.062, 0.002), (0, 0, 0), m_kit))
    o.append(zhole(0, 0, 0, 0.003))
    for sx in (-0.02, 0.02):
        for sy in (-0.02, 0.02):
            o.append(zhole(sx, sy, 0, 0.0015))
    return o

def build_u():
    # photo1: interieur 52 (ext 56), axe (gros trou) a 52 du socle, trous a 16.5/33.5,
    # sommets arrondis R=profondeur/2. Profondeur 40 A-VALIDER. Origine = axe.
    o = []
    o.append(box("UFond", (0.056, 0.040, 0.002), (0, 0, -0.051), m_kit))
    o.append(zhole(0, 0, -0.051, 0.004))
    for sx, sy in ((0.0065, 0), (-0.0065, 0), (0, 0.0065), (0, -0.0065)):
        o.append(zhole(sx, sy, -0.051, 0.0012))
    for sx in (-0.027, 0.027):
        o.append(box("UArm", (0.002, 0.040, 0.052), (sx, 0, -0.026), m_kit))
        o.append(cyl("UArc", 0.020, 0.002, (sx, 0, 0), m_kit, rot=(0, math.radians(90), 0)))
        o.append(xhole(sx, 0, 0, 0.004))
        for dy, dz in ((0.010, 0), (-0.010, 0), (0, 0.010), (0, -0.010)):
            o.append(xhole(sx, dy, dz, 0.0015))
        o.append(xhole(sx, 0, -0.0185, 0.0015))
        o.append(xhole(sx, 0, -0.0355, 0.0015))
    return o

def build_horn_pan():
    # Palonnier rond fourni O21 : centre + 4 trous (visse sous socle U).
    o = []
    o.append(cyl("HornPan", 0.0105, 0.003, (0, 0, 0), m_horn))
    o.append(zhole(0, 0, 0, 0.002))
    for sx, sy in ((0.0065, 0), (-0.0065, 0), (0, 0.0065), (0, -0.0065)):
        o.append(zhole(sx, sy, 0, 0.0012))
    return o

def build_horn_tilt():
    # Palonnier rond fourni O21 : centre + 2 vis (visse au bras du U).
    o = []
    o.append(cyl("HornTilt", 0.0105, 0.003, (0, 0, 0), m_horn))
    o.append(zhole(0, 0, 0, 0.002))
    for sx in (-0.008, 0.008):
        o.append(zhole(sx, 0, 0, 0.0015))
    return o

def build_bras_alu():
    # Bras alu rouge 25T (photo cotation): L35 x l~10 x ep6, alesage O6 25T,
    # trous M3 a 15/20/24 du centre, serrage fendu + vis.
    o = []
    o.append(cyl("Moyeu", 0.0065, 0.006, (-0.014, 0, 0), m_horn))
    o.append(cyl("Alesage", 0.003, 0.007, (-0.014, 0, 0), m_hole))
    import math as _m
    for _i in range(25):
        _a = 2 * _m.pi * _i / 25
        o.append(box(f"Cannelure{_i}", (0.0012, 0.0009, 0.006),
                     (-0.014 + 0.003 * _m.cos(_a), 0.003 * _m.sin(_a), 0),
                     m_horn, rot=(0, 0, _a)))
    o.append(box("Fente", (0.008, 0.002, 0.006), (-0.018, 0, 0), m_hole))
    o.append(cyl("VisSerrage", 0.0015, 0.012, (-0.020, 0, 0), m_steel, rot=(math.radians(90), 0, 0)))
    o.append(box("Bras", (0.030, 0.010, 0.006), (0.001, 0, 0), m_horn))
    o.append(cyl("BoutRond", 0.005, 0.006, (0.016, 0, 0), m_horn))
    for hx in (0.001, 0.006, 0.010):
        o.append(zhole(hx, 0, 0, 0.0015))
    return o

def build_support():
    # pince BOM-budget: dos + 4 machoires serrant le bord bas de la tablette
    o = []
    o.append(box("SupDos", (0.003, 0.060, 0.024), (0, 0, 0), m_sup))
    for sy in (-0.020, 0.020):
        o.append(xhole(0, sy, -0.006, 0.0015))
    for sx in (-0.030, 0.030):
        for sy in (-0.0075, 0.0075):
            o.append(box("Machoires", (0.020, 0.006, 0.020), (sx, sy, 0.028), m_sup))
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
    "platine_bracket": build_bracket,
    "plateau": build_plateau,
    "U_kit": build_u,
    "palonnier_pan": build_horn_pan,
    "palonnier_tilt": build_horn_tilt,
    "bras_alu_25t": build_bras_alu,
    "support_pince": build_support,
    "tablette": build_tablette,
    "base_bois": build_base,
}

# etendues max par piece et par vue (m) pour cadrage ortho
EXT = {
    "pan_mg996r": {"face": 0.055, "dessus": 0.055, "gauche": 0.042},
    "tilt_ds3218": {"face": 0.054, "dessus": 0.047, "gauche": 0.054},
    "platine_bracket": {"face": 0.060, "dessus": 0.060, "gauche": 0.060},
    "plateau": {"face": 0.062, "dessus": 0.062, "gauche": 0.062},
    "U_kit": {"face": 0.072, "dessus": 0.056, "gauche": 0.072},
    "palonnier_pan": {"face": 0.021, "dessus": 0.021, "gauche": 0.021},
    "palonnier_tilt": {"face": 0.021, "dessus": 0.021, "gauche": 0.021},
    "bras_alu_25t": {"face": 0.035, "dessus": 0.035, "gauche": 0.013},
    "support_pince": {"face": 0.080, "dessus": 0.080, "gauche": 0.060},
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
bpy.context.scene.render.resolution_x = 640
bpy.context.scene.render.resolution_y = 480
bpy.context.scene.render.image_settings.file_format = 'PNG'

CAMS = {
    "face": (0, 2, 0.15),
    "dessus": (0, 0, 2),
    "gauche": (-2, 0, 0.15),
}

flat = [o for lst in all_objs.values() for o in lst]
for name, objs in all_objs.items():
    for o in flat:
        o.hide_render = (o not in objs)
    for view, pos in CAMS.items():
        co.location = pos
        tgt = Vector((0 - pos[0], 0 - pos[1], 0 - pos[2]))
        co.rotation_euler = tgt.to_track_quat('-Z', 'Y').to_euler()
        cam.ortho_scale = EXT[name][view] * 1.45
        bpy.context.scene.render.filepath = f"{OUT}/{name}_{view}.png"
        bpy.ops.render.render(write_still=True)
        print(f"VUE-OK {name}_{view}")

print("FICHES-OK")
