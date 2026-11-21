"""Assemblage 2 axes budget (pan+tilt, sans roll) - cotes plan v3.
Base 200x200 ep18. MG996R pan. U kit H60. DS3218 tilt. Dell 216x130 direct sur tilt.
Pivots: PanPivot (yaw Z) interface 59mm, TiltPivot (pitch) axe U a 113mm. TOTAL ~275.
Run: Blender 5.2 --background --python assembly_2axes.py
"""
import bpy, math
from mathutils import Vector

# --- Cleanup ---
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

def box(name, dims, loc, material, parent=None, rot=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0))
    o = bpy.context.active_object
    o.name = name
    o.dimensions = dims
    if rot is not None:
        o.rotation_euler = rot
    if parent is not None:
        o.parent = parent
        o.location = loc
    else:
        o.location = loc
    o.data.materials.append(material)
    return o

def cyl(name, r, h, loc, material, parent=None, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=h, location=(0, 0, 0), vertices=20)
    o = bpy.context.active_object
    o.name = name
    o.rotation_euler = rot
    if parent is not None:
        o.parent = parent
        o.location = loc
    else:
        o.location = loc
    o.data.materials.append(material)
    return o

def empty(name, loc, parent=None):
    e = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(e)
    if parent is not None:
        e.parent = parent
        e.location = loc
    else:
        e.location = loc
    return e

m_wood = mat("Wood", (0.55, 0.38, 0.20), 0.0, 0.85)
m_pan = mat("PanRed", (0.9, 0.12, 0.12), 0.1, 0.5)
m_tilt = mat("TiltOrange", (1.0, 0.5, 0.0), 0.1, 0.5)
m_kit = mat("KitGreen", (0.1, 0.7, 0.25), 0.3, 0.5)
m_horn = mat("HornYellow", (1.0, 0.9, 0.1), 0.2, 0.5)
m_plot = mat("PlotPurple", (0.65, 0.2, 0.9), 0.1, 0.5)
m_sup = mat("SupportCyan", (0.1, 0.8, 0.9), 0.1, 0.5)
m_brass = mat("Brass", (0.85, 0.65, 0.2), 0.9, 0.35)
m_steel = mat("Steel", (0.8, 0.8, 0.85), 0.9, 0.3)
m_hole = mat("Hole", (0.02, 0.02, 0.02), 0.0, 1.0)
m_tablet = mat("TabletBody", (0.05, 0.05, 0.06), 0.2, 0.5)
m_screen = mat("Screen", (0.02, 0.10, 0.18), 0.0, 0.4, (0.1, 0.55, 1.0), 2.0)

# --- Statique : base + pan fixe + plot/roulement ---
# Servo visse SOUS la platine kit : corps entre bois et platine, zero jour
box("BaseBois", (0.20, 0.20, 0.018), (0, 0, 0.009), m_wood)
box("PanBody", (0.0407, 0.0197, 0.036), (-0.0032, 0, 0.036), m_pan)
box("PanEarL", (0.007, 0.0197, 0.004), (-0.027, 0, 0.052), m_pan)
box("PanEarR", (0.007, 0.0197, 0.004), (0.0206, 0, 0.052), m_pan)
box("BracketBase", (0.060, 0.060, 0.002), (0, 0, 0.055), m_kit)
box("TabL", (0.002, 0.020, 0.022), (-0.024, 0, 0.067), m_kit)
box("TabR", (0.002, 0.020, 0.022), (0.024, 0, 0.067), m_kit)
cyl("PivotL", 0.0025, 0.006, (-0.0265, 0, 0.077), m_brass, rot=(0, math.radians(90), 0))
cyl("PivotR", 0.0025, 0.006, (0.0265, 0, 0.077), m_brass, rot=(0, math.radians(90), 0))
box("MurOreille", (0.003, 0.020, 0.040), (-0.0195, 0, 0.075), m_kit)
cyl("VisMur1", 0.0015, 0.010, (-0.0195, 0.005, 0.0863), m_steel, rot=(0, math.radians(90), 0))
cyl("VisMur2", 0.0015, 0.010, (-0.0195, -0.005, 0.0863), m_steel, rot=(0, math.radians(90), 0))

# --- PanPivot (yaw, axe Z) interface horn/U fond 60mm, zero jour ---
pan_pivot = empty("PanPivot", (0, 0, 0.059))
cyl("PanArbre", 0.003, 0.008, (0, 0, -0.003), m_horn, parent=pan_pivot)
cyl("PanHorn", 0.0105, 0.003, (0, 0, -0.0005), m_horn, parent=pan_pivot)
# U photo1 : exterieur 56 / interieur 52, axe a 52 du socle, sommets arrondis
box("UFond", (0.056, 0.040, 0.002), (0, 0, 0.001), m_kit, parent=pan_pivot)
box("UArmL", (0.002, 0.040, 0.052), (-0.027, 0, 0.028), m_kit, parent=pan_pivot)
box("UArmR", (0.002, 0.040, 0.052), (0.027, 0, 0.028), m_kit, parent=pan_pivot)
cyl("UArcL", 0.020, 0.002, (-0.027, 0, 0.054), m_kit, parent=pan_pivot, rot=(0, math.radians(90), 0))
cyl("UArcR", 0.020, 0.002, (0.027, 0, 0.054), m_kit, parent=pan_pivot, rot=(0, math.radians(90), 0))

# --- TiltPivot (pitch) = point arbre, trou bras gauche : config photo R_y(-90) ---
tilt_pivot = empty("TiltPivot", (-0.027, 0, 0.054), parent=pan_pivot)
box("TiltBody", (0.0366, 0.020, 0.0403), (0.0193, 0, -0.00325), m_tilt, parent=tilt_pivot)
box("EarUp", (0.003, 0.013, 0.007), (0.011, 0, 0.0202), m_tilt, parent=tilt_pivot)
box("EarDown", (0.003, 0.013, 0.007), (0.011, 0, -0.0267), m_tilt, parent=tilt_pivot)
cyl("TiltArbre", 0.003, 0.010, (-0.002, 0, 0), m_horn, parent=tilt_pivot, rot=(0, math.radians(90), 0))
cyl("TiltHorn", 0.0105, 0.003, (-0.0045, 0, 0), m_horn, parent=tilt_pivot, rot=(0, math.radians(90), 0))
cyl("HornVis1", 0.0015, 0.006, (-0.001, 0.008, 0.008), m_steel, parent=tilt_pivot, rot=(0, math.radians(90), 0))
cyl("HornVis2", 0.0015, 0.006, (-0.001, -0.008, -0.008), m_steel, parent=tilt_pivot, rot=(0, math.radians(90), 0))
cyl("PalierTilt", 0.006, 0.004, (0.054, 0, 0), m_brass, parent=tilt_pivot, rot=(0, math.radians(90), 0))
cyl("VisPalier", 0.0025, 0.020, (0.044, 0, 0), m_steel, parent=tilt_pivot, rot=(0, math.radians(90), 0))
for _i, (_sy, _sz) in enumerate([(-0.005, 0.0202), (0.005, 0.0202), (-0.005, -0.0267), (0.005, -0.0267)]):
    cyl(f"TrouOreille{_i}", 0.0016, 0.005, (0.011, _sy, _sz), m_hole, parent=tilt_pivot, rot=(0, math.radians(90), 0))
# pince BOM-budget : dos + 4 machoires sur bord bas tablette (145-275 monde)
box("PinceDos", (0.003, 0.060, 0.024), (-0.0315, 0, 0.020), m_sup, parent=tilt_pivot)
box("PinceDessus", (0.080, 0.060, 0.008), (0, 0, 0.028), m_sup, parent=tilt_pivot)
for _sx, _sy in ((-0.030, -0.0075), (-0.030, 0.0075), (0.030, -0.0075), (0.030, 0.0075)):
    box("Machoires", (0.020, 0.006, 0.020), (_sx, _sy, 0.036), m_sup, parent=tilt_pivot)
box("Tablette", (0.216, 0.009, 0.130), (0, 0, 0.097), m_tablet, parent=tilt_pivot)
box("Ecran", (0.196, 0.002, 0.110), (0, 0.0055, 0.097), m_screen, parent=tilt_pivot)

# --- Lumieres ---
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

# --- Camera vue AVANT (+Y, cote ecran), cadre large avec marge base ---
bpy.context.view_layer.update()
cam = bpy.data.cameras.new("Cam")
cam.lens = 50
co = bpy.data.objects.new("Cam", cam)
bpy.context.collection.objects.link(co)
co.location = (0.55, 0.82, 0.40)
target = Vector((0 - co.location.x, 0 - co.location.y, 0.13 - co.location.z))
co.rotation_euler = target.to_track_quat('-Z', 'Y').to_euler()
bpy.context.scene.camera = co

# --- Rendu preview ---
bpy.context.scene.render.engine = 'CYCLES'
bpy.context.scene.cycles.device = 'CPU'
bpy.context.scene.cycles.samples = 64
bpy.context.scene.cycles.use_denoising = False
bpy.context.scene.render.resolution_x = 1280
bpy.context.scene.render.resolution_y = 720
bpy.context.scene.render.filepath = r"C:/Users/mathias/Work/Projects/personal_bot/hardware/rendus/preview_2axes.png"
bpy.context.scene.render.image_settings.file_format = 'PNG'
bpy.ops.render.render(write_still=True)
print("PREVIEW-OK")

# --- Export glTF assemble (pivots conserves pour Godot) ---
bpy.ops.export_scene.gltf(
    filepath=r"C:/Users/mathias/Work/Projects/personal_bot/hardware/cad/tete_2axes.glb",
    export_format='GLB',
    use_selection=False,
    export_yup=True,
)
print("GLB-OK")
