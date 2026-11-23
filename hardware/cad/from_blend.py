"""Rendus + GLB directement depuis assemblage_2axes.blend (fichier atelier).
Fix cinematiques (en memoire, fichier non modifie) :
- PanPivot/PanHorn/PanArbre : x -9 -> +6.4 (sur l'arbre du pan).
- TiltBody : reparente sous PanPivot (stator, ne tangue pas), transfo monde conservee.
"""
import bpy
from mathutils import Vector
import math

sc = bpy.context.scene
try:
    sc.render.engine = "BLENDER_EEVEE_NEXT"
except Exception:
    sc.render.engine = "BLENDER_EEVEE"
sc.render.resolution_x = 1280
sc.render.resolution_y = 720
sc.render.film_transparent = False

pan_pivot = bpy.data.objects["PanPivot"]
tilt_pivot = bpy.data.objects["TiltPivot"]

# FIX 1 : axe yaw + disque + arbre sur l'arbre servo (x=+6.4mm)
for n in ("PanPivot", "PanHorn", "PanArbre"):
    o = bpy.data.objects[n]
    o.location.x = 0.0064

# FIX 2 : corps tilt statique en pitch (garde sa transfo monde)
tilt_body = bpy.data.objects["TiltBody"]
mw = tilt_body.matrix_world.copy()
tilt_body.parent = pan_pivot
tilt_body.matrix_world = mw

# cameras face (-Y, cote ecran) et arriere (+Y)
def shoot(name, loc):
    cam_data = bpy.data.cameras.new("T")
    co = bpy.data.objects.new("T", cam_data)
    sc.collection.objects.link(co)
    cam_data.lens = 50
    co.location = loc
    d = Vector((0.003, 0, 0.14)) - co.location
    co.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    sc.camera = co
    sc.render.filepath = name
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(co, do_unlink=True)

shoot(r"C:/Users/mathias/Work/Projects/personal_bot/hardware/rendus/preview_2axes.png",
      (0.35, -0.85, 0.42))
shoot(r"C:/Users/mathias/Work/Projects/personal_bot/hardware/rendus/preview_2axes_arriere.png",
      (0.35, 0.85, 0.42))
print("PREVIEW-OK x2")

bpy.ops.export_scene.gltf(
    filepath=r"C:/Users/mathias/Work/Projects/personal_bot/hardware/cad/tete_2axes.glb",
    export_format="GLB",
    use_selection=False,
    export_yup=True,
)
print("GLB-OK")
