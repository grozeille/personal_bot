import bpy, math

# Nettoyage scene par defaut.
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

m_dark = mat("Dark", (0.16, 0.17, 0.20), 0.3, 0.6)
m_metal = mat("Metal", (0.72, 0.73, 0.76), 0.85, 0.35)
m_servo = mat("Servo", (0.10, 0.10, 0.12), 0.1, 0.55)
m_purple = mat("ServoLabel", (0.48, 0.16, 0.58), 0.0, 0.5)
m_horn = mat("Horn", (0.85, 0.85, 0.88), 0.9, 0.3)
m_wood = mat("Wood", (0.76, 0.60, 0.42), 0.0, 0.85)
m_pcb = mat("PCB", (0.08, 0.38, 0.16), 0.0, 0.6)
m_tablet = mat("TabletBody", (0.05, 0.05, 0.06), 0.2, 0.5)
m_button = mat("WinButton", (0.75, 0.78, 0.82), 0.0, 0.4, (0.5, 0.55, 0.6), 0.6)
m_camdot = mat("CamDot", (0.02, 0.03, 0.05), 0.3, 0.3)
m_screen = mat("Screen", (0.02, 0.10, 0.18), 0.0, 0.4, (0.1, 0.55, 1.0), 2.0)
m_eye = mat("Eye", (0.05, 0.85, 1.0), 0.0, 0.3, (0.1, 0.9, 1.0), 3.0)
m_eyehi = mat("EyeHi", (1.0, 1.0, 1.0), 0.0, 0.3, (1.0, 1.0, 1.0), 2.0)

def box(name, dims, loc, material, rot=None):
    # IMPORTANT : dimensions AVANT rotation (dimensions = bbox alignee monde,
    # la fixer apres rotation fausse l'echelle).
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

CX, CY = -0.06, -0.03  # axe de la colonne

# --- Base bois + embase ---
box("WoodBase", (0.34, 0.28, 0.025), (0, 0, 0.0125), m_wood)
box("Pedestal", (0.10, 0.10, 0.02), (CX, CY, 0.035), m_dark)

# --- Servo Pan (MG996R, axe vertical) : corps 41x20x43 ---
box("PanServo", (0.041, 0.020, 0.043), (CX, CY, 0.0665), m_servo)
box("PanFlange", (0.055, 0.020, 0.004), (CX, CY, 0.047), m_dark)
box("PanLabel", (0.030, 0.0012, 0.020), (CX, CY + 0.0102, 0.0665), m_purple)
cyl("PanShaft", 0.003, 0.012, (CX, CY, 0.094), m_horn)
cyl("PanHorn", 0.011, 0.003, (CX, CY, 0.0995), m_horn)

# --- Chape Pan (U alu) : traverse + 2 flasques + vis ---
box("PanCross", (0.062, 0.05, 0.006), (CX, CY, 0.103), m_metal)
for i, sx in enumerate((-1, 1)):
    box(f"PanPlate{'L' if sx < 0 else 'R'}", (0.006, 0.05, 0.10),
        (CX + sx * 0.031, CY, 0.15), m_metal)
    for j, z in enumerate((0.12, 0.18)):
        cyl(f"PanScrew{i}{j}", 0.0025, 0.010, (CX + sx * 0.036, CY, z), m_dark,
            rot=(0, math.radians(90), 0))

# --- Servo Tilt (DS3218 sur le flanc, axe horizontal X) ---
box("TiltServo", (0.020, 0.041, 0.043), (CX, CY, 0.16), m_servo)
box("TiltLabel", (0.0012, 0.030, 0.020), (CX - 0.0102, CY, 0.16), m_purple)
cyl("TiltShaft", 0.003, 0.014, (CX - 0.016, CY, 0.16), m_horn,
    rot=(0, math.radians(90), 0))
cyl("TiltHorn", 0.011, 0.003, (CX - 0.024, CY, 0.16), m_horn,
    rot=(0, math.radians(90), 0))

# --- Bras Tilt : 2 flasques + vis ---
for i, sx in enumerate((-1, 1)):
    box(f"TiltPlate{'L' if sx < 0 else 'R'}", (0.006, 0.04, 0.12),
        (CX + sx * 0.023, CY, 0.24), m_metal)
    for j, z in enumerate((0.20, 0.28)):
        cyl(f"TiltScrew{i}{j}", 0.0025, 0.010, (CX + sx * 0.028, CY, z), m_dark,
            rot=(0, math.radians(90), 0))

# --- Servo Roll (MG996R, axe vertical) ---
box("RollServo", (0.041, 0.020, 0.043), (CX, CY, 0.3215), m_servo)
box("RollLabel", (0.030, 0.0012, 0.020), (CX, CY + 0.0102, 0.3215), m_purple)
cyl("RollShaft", 0.003, 0.010, (CX, CY, 0.348), m_horn)
cyl("RollHorn", 0.010, 0.003, (CX, CY, 0.3525), m_horn)

# --- Col vers la tablette ---
box("NeckPost", (0.045, 0.035, 0.11), (CX, CY - 0.005, 0.41), m_dark)
box("MountPlate", (0.16, 0.01, 0.10), (CX, CY - 0.012, 0.46), m_dark)
# Tablette 216x130mm posee yaw -30 / pitch 8 (pose du test).
box("Tablet", (0.216, 0.014, 0.13), (-0.06, -0.03, 0.53), m_tablet,
    rot=(math.radians(8), 0, math.radians(-30)))
box("Screen", (0.196, 0.005, 0.11), (-0.06, -0.022, 0.532), m_screen,
    rot=(math.radians(8), 0, math.radians(-30)))
# Details sur un repere neutre (meme pose que la tablette, echelle 1).
bpy.ops.object.empty_add(location=(-0.06, -0.03, 0.53),
                         rotation=(math.radians(8), 0, math.radians(-30)))
tablet = bpy.data.objects["Tablet"]
mount = bpy.context.active_object
mount.name = "TabletMount"
def detail(name, mesh_op, loc, material, rot=(0, 0, 0)):
    mesh_op(location=(0, 0, 0))
    o = bpy.context.active_object
    o.name = name
    o.parent = mount
    o.location = loc
    o.rotation_euler = rot
    o.data.materials.append(material)
    return o
import math as _m
detail("FrontCam",
       lambda location: bpy.ops.mesh.primitive_cylinder_add(radius=0.004, depth=0.002, location=location),
       (0, 0.008, 0.061), m_camdot, rot=(_m.radians(90), 0, 0))
detail("WinButton",
       lambda location: bpy.ops.mesh.primitive_cube_add(size=1, location=location),
       (0, 0.008, -0.060), m_button)
bpy.data.objects["WinButton"].scale = (0.024, 0.002, 0.014)
# Yeux du concept (pastilles emissives sur l'ecran).
for i, sx in enumerate((-1, 1)):
    detail(f"Eye{i}",
           lambda location: bpy.ops.mesh.primitive_cylinder_add(radius=0.016, depth=0.002, location=location),
           (sx * 0.045, 0.0105, 0.012), m_eye, rot=(math.radians(90), 0, 0))
    detail(f"EyeHi{i}",
           lambda location: bpy.ops.mesh.primitive_cylinder_add(radius=0.005, depth=0.002, location=location),
           (sx * 0.045 + 0.005, 0.0115, 0.018), m_eyehi, rot=(math.radians(90), 0, 0))
box("Pi", (0.085, 0.056, 0.015), (0.09, 0.06, 0.033), m_pcb)
box("Pca", (0.062, 0.026, 0.012), (0.02, -0.075, 0.031), m_pcb)
box("Floor", (6, 6, 0.1), (0, 0, -0.05), m_dark)

# Lumiere : soleil + remplissage.
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

# Camera.
cam = bpy.data.cameras.new("Cam")
cam.lens = 50
co = bpy.data.objects.new("Cam", cam)
bpy.context.collection.objects.link(co)
# Camera : plan large 3/4 avant sur l'ensemble.
co.location = (0.80, 1.12, 0.68)
direction = (-0.02 - co.location.x, -0.01 - co.location.y, 0.32 - co.location.z)
from mathutils import Vector
quat = Vector(direction).to_track_quat('-Z', 'Y')
co.rotation_euler = quat.to_euler()
bpy.context.scene.camera = co
bpy.context.scene.render.engine = 'CYCLES'
bpy.context.scene.cycles.device = 'CPU'
bpy.context.scene.cycles.samples = 64
bpy.context.scene.cycles.use_denoising = False
bpy.context.scene.render.resolution_x = 1280
bpy.context.scene.render.resolution_y = 720
bpy.context.scene.render.film_transparent = False
bpy.context.scene.render.filepath = r"C:/Users/mathias/AppData/Local/hermes/cache/scratch/arm-render.png"
bpy.context.scene.render.image_settings.file_format = 'PNG'
bpy.ops.render.render(write_still=True)
print("RENDER-OK")
