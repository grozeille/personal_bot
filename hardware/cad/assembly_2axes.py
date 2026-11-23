"""Assemblage 2 axes budget (pan+tilt, sans roll) - cotes plan v3.
Base 200x200 ep18. MG996R pan. U kit H60. DS3218 tilt. Dell 216x130 direct sur tilt.
Pivots: PanPivot (yaw Z) interface 59mm, TiltPivot (pitch) axe U a 113mm. TOTAL ~275.
Run: Blender 5.2 --background --python assembly_2axes.py
"""
import bpy, math, os
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

def sph(name, r, loc, material, parent=None):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=(0, 0, 0), segments=24, ring_count=16)
    o = bpy.context.active_object
    o.name = name
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
# Rework atelier (assemblage_2axes.blend) : positions monde exactes.
# Socle bois 200x200 ep18.
box("BaseBois", (0.20, 0.20, 0.018), (0, 0, 0.009), m_wood)
# U cadre autour du pan (sol + mur + montants + bras plats).
box("UFlasque", (0.040, 0.020, 0.002), (0.0032, 0, 0.018), m_kit)
box("UBaseCourt", (0.040, 0.002, 0.042), (0.0032, 0.011, 0.017), m_kit)
box("UBande", (0.008, 0.002, 0.028), (0.0272, 0.011, 0.038), m_kit)
box("UBande.001", (0.008, 0.002, 0.028), (-0.0208, 0.011, 0.038), m_kit)
box("UArmL", (0.008, 0.020, 0.002), (0.0272, 0, 0.051), m_kit)
box("UArmR", (0.008, 0.020, 0.002), (-0.0208, 0, 0.051), m_kit)
# Servo pan (stator, corps decale +3.2, arbre sur l'axe).
box("PanBody", (0.0407, 0.0197, 0.036), (0.0032, 0, 0.038), m_pan)
box("PanEarL", (0.007, 0.0197, 0.004), (0.027, 0, 0.054), m_pan)
box("PanEarR", (0.007, 0.0197, 0.004), (-0.0206, 0, 0.054), m_pan)
# --- PanPivot (yaw) : disque + arbre sur l'axe x=+6.4 ---
pan_pivot = empty("PanPivot", (0.0064, 0, 0.063))
cyl("PanArbre", 0.003, 0.008, (0, 0, -0.007), m_steel, parent=pan_pivot)
cyl("PanHorn", 0.00975, 0.003, (0, 0, -0.0015), m_pan, parent=pan_pivot)
# Chape base 55.6 (plaque sur le disque) + joues vers l'arbre.
box("RDSHautPlaque", (0.0556, 0.020, 0.002), (-0.0027, 0, 0.001), m_tilt, parent=pan_pivot)
box("RDSHautJoue", (0.002, 0.020, 0.0315), (0.0261, 0, 0.0157), m_tilt, parent=pan_pivot)
box("RDSHautJoue.001", (0.002, 0.020, 0.0315), (-0.0315, 0, 0.0157), m_tilt, parent=pan_pivot)
# Chape haute 44.5 + joues (coiffe au-dessus du servo).
box("RDSBasPlaque", (0.0445, 0.020, 0.002), (-0.0027, 0, 0.063), m_tilt, parent=pan_pivot)
box("RDSBasJoue", (0.002, 0.020, 0.027), (0.0205, 0, 0.0505), m_tilt, parent=pan_pivot)
box("RDSBasJoue.001", (0.002, 0.020, 0.027), (-0.0259, 0, 0.0505), m_tilt, parent=pan_pivot)
# Corps servo tilt (stator yaw, PAS de pitch : ne tourne pas en tangage).
box("TiltBody", (0.040, 0.020, 0.040), (-0.0027, 0, 0.037), m_tilt, parent=pan_pivot)
# --- TiltPivot (pitch) sur l'axe du palonnier ---
tilt_pivot = empty("TiltPivot", (0.0252, 0, 0.029), parent=pan_pivot)
cyl("TiltArbre", 0.003, 0.014, (-0.0096, 0, 0), m_steel, parent=tilt_pivot, rot=(0, math.radians(90), 0))
cyl("TiltHorn", 0.00975, 0.003, (-0.0015, 0, 0), m_horn, parent=tilt_pivot, rot=(0, math.radians(90), 0))
# Pince + tablette (rotor pitch, base 1/4 sur la plaque haute).
cyl("Base14", 0.016, 0.010, (-0.0281, 0, 0.040), m_sup, parent=tilt_pivot)
cyl("Insert14", 0.005, 0.004, (-0.0281, 0, 0.042), m_steel, parent=tilt_pivot)
cyl("Col14", 0.006, 0.020, (-0.0281, 0, 0.049), m_sup, parent=tilt_pivot)
sph("Rotule", 0.011, (-0.0281, 0, 0.064), m_sup, parent=tilt_pivot)
cyl("VisRotule", 0.004, 0.012, (-0.0421, 0, 0.064), m_steel, parent=tilt_pivot, rot=(0, math.radians(90), 0))
box("ColHaut", (0.020, 0.018, 0.030), (-0.0281, -0.004, 0.069), m_sup, parent=tilt_pivot)
box("Coulisseau", (0.040, 0.006, 0.070), (-0.0281, -0.013, 0.142), m_sup, parent=tilt_pivot)
box("PinceDos", (0.056, 0.008, 0.080), (-0.0281, -0.015, 0.114), m_sup, parent=tilt_pivot)
box("PinceBras", (0.018, 0.007, 0.080), (-0.0479, -0.015, 0.148), m_sup, parent=tilt_pivot, rot=(0, 0.5254, 0))
box("PinceBras.001", (0.018, 0.007, 0.080), (-0.0479, -0.015, 0.080), m_sup, parent=tilt_pivot, rot=(0, 2.616, 0))
box("PinceBras.002", (0.018, 0.007, 0.080), (-0.0083, -0.015, 0.148), m_sup, parent=tilt_pivot, rot=(0, -0.5254, 0))
box("PinceBras.003", (0.018, 0.007, 0.080), (-0.0083, -0.015, 0.080), m_sup, parent=tilt_pivot, rot=(0, -2.616, 0))
box("Crochet", (0.014, 0.008, 0.018), (0.0114, -0.019, 0.046), m_sup, parent=tilt_pivot)
box("Crochet.001", (0.014, 0.008, 0.018), (0.0114, -0.019, 0.182), m_sup, parent=tilt_pivot)
box("Crochet.002", (0.014, 0.008, 0.018), (-0.0676, -0.019, 0.046), m_sup, parent=tilt_pivot)
box("Crochet.003", (0.014, 0.008, 0.018), (-0.0676, -0.019, 0.182), m_sup, parent=tilt_pivot)
box("Levre", (0.014, 0.018, 0.007), (0.0114, -0.032, 0.0455), m_sup, parent=tilt_pivot)
box("Levre.001", (0.014, 0.018, 0.007), (0.0114, -0.032, 0.1825), m_sup, parent=tilt_pivot)
box("Levre.002", (0.014, 0.018, 0.007), (-0.0676, -0.032, 0.0455), m_sup, parent=tilt_pivot)
box("Levre.003", (0.014, 0.018, 0.007), (-0.0676, -0.032, 0.1825), m_sup, parent=tilt_pivot)
box("PincePad", (0.046, 0.005, 0.062), (-0.0281, -0.023, 0.114), m_sup, parent=tilt_pivot)
box("Tablette", (0.216, 0.009, 0.130), (-0.0281, -0.030, 0.114), m_tablet, parent=tilt_pivot)
box("Ecran", (0.196, 0.002, 0.110), (-0.0281, -0.0355, 0.114), m_screen, parent=tilt_pivot)

# --- Lumieres ---
sun = bpy.data.lights.new("Sun", 'SUN')
sun.energy = 3.0
so = bpy.data.objects.new("Sun", sun)
bpy.context.collection.objects.link(so)
so.rotation_euler = (math.radians(50), 0, math.radians(-30) if os.environ.get("SUN_R", "") else math.radians(30))
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
_demo = float(os.environ.get("TILT_DEMO", "0"))
if _demo:
    tilt_pivot.rotation_euler = (math.radians(_demo), 0, 0)
    bpy.context.scene.render.filepath = r"C:/Users/mathias/AppData/Local/hermes/cache/scratch/demo_pitch.png"
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
