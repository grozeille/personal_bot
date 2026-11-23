import bpy
from mathutils import Vector
print("=== RDS DUMP ===")
for o in bpy.data.objects:
    if o.type == "MESH":
        ws = [o.matrix_world @ Vector(v.co) for v in o.data.vertices]
        xs = [v.x * 1000 for v in ws]; ys = [v.y * 1000 for v in ws]; zs = [v.z * 1000 for v in ws]
        dx, dy, dz = max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs)
        print("MESH {} par={} x[{:.1f},{:.1f}] y[{:.1f},{:.1f}] z[{:.1f},{:.1f}] d=({:.1f},{:.1f},{:.1f}) nv={}".format(
            o.name, o.parent.name if o.parent else "-",
            min(xs), max(xs), min(ys), max(ys), min(zs), max(zs), dx, dy, dz, len(o.data.vertices)))
    elif o.type == "EMPTY":
        w = o.matrix_world.translation * 1000
        print("EMPTY {} at=({:.1f},{:.1f},{:.1f})".format(o.name, w.x, w.y, w.z))
print("=== END ===")
