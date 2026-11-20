"""Plan 2D v2 : logique d'assemblage kit pan-tilt reelle.
Horn visse = interface tournante. Servos brides par oreilles. Roulement sous plateau PAN.
Cotes indicatives : a relever au pied a coulisse sur le kit recu. Unites mm."""
from PIL import Image, ImageDraw, ImageFont

try:
    F = ImageFont.load_default(20)
    FS = ImageFont.load_default(16)
except Exception:
    F = FS = ImageFont.load_default()

W, H = 1700, 1000
img = Image.new("RGB", (W, H), "white")
d = ImageDraw.Draw(img)
S = 1.6
def X(xmm, ox): return ox + xmm * S
def Z(zmm): return H - 60 - zmm * S

C = {"fill": (235, 235, 235), "screen": (150, 210, 255),
     "eye": (20, 170, 230), "axis": "red", "dim": (120, 120, 120),
     "label": (70, 70, 70), "servo": (200, 200, 205), "purple": (150, 80, 170),
     "metal": (190, 195, 200), "wood": (210, 175, 120)}

def rect(ox, x0, z0, x1, z1, fill=None, outline="black", width=2):
    d.rectangle([X(x0, ox), Z(z1), X(x1, ox), Z(z0)], fill=fill, outline=outline, width=width)

def txt(ox, xmm, zmm, s, font=F, color="black"):
    d.text((X(xmm, ox), Z(zmm)), s, fill=color, font=font)

def bolt(ox, xmm, zmm):
    d.ellipse([X(xmm - 2.5, ox), Z(zmm + 2.5), X(xmm + 2.5, ox), Z(zmm - 2.5)], fill="black")

def dim_v(ox, xmm, z0, z1, label):
    x = X(xmm, ox)
    d.line([(x, Z(z0)), (x, Z(z1))], fill=C["dim"], width=1)
    d.line([(x - 5, Z(z0)), (x + 5, Z(z0))], fill=C["dim"], width=1)
    d.line([(x - 5, Z(z1)), (x + 5, Z(z1))], fill=C["dim"], width=1)
    d.text((x + 6, (Z(z0) + Z(z1)) / 2 - 10), label, fill=C["dim"], font=FS)

# ================= VUE FACE =================
OX = 420
txt(OX, -170, 470, "VUE FACE v2 (mm) - assemblage kit", color=C["label"])
# base
rect(OX, -170, 0, 170, 25, fill=C["wood"]); txt(OX, 175, 8, "base bois 340x280 ep.25")
# --- PAN : servo visse par ses oreilles sur la base ---
rect(OX, -80.5, 25, -39.5, 61, fill=C["servo"])
rect(OX, -87, 57, -33, 61, fill=C["fill"])  # oreilles
for x in (-82, -38):
    bolt(OX, x, 59)
txt(OX, -25, 40, "PAN visse par oreilles (4 vis)")
# arbre -> palonnier -> plateau (interface tournante VISSEE)
rect(OX, -63, 61, -57, 70, fill=C["metal"])
rect(OX, -75, 70, -45, 73, fill=C["metal"])
bolt(OX, -68, 73); bolt(OX, -52, 73)
txt(OX, 20, 62, "palonnier VISSE sous plateau (2 vis)")
rect(OX, -91, 73, -29, 79, fill=C["metal"])
txt(OX, -160, 82, "plateau tournant")
# roulement : plot fixe + roulement sous plateau
rect(OX, 30, 25, 50, 67, fill=C["fill"])
rect(OX, 30, 67, 50, 73, fill=C["metal"])
txt(OX, 55, 45, "plot + roulement (charge)")
# --- TILT : chape en U, servo bride par oreilles ---
rect(OX, -91, 79, -85, 160, fill=C["metal"]); rect(OX, -35, 79, -29, 160, fill=C["metal"])
txt(OX, -200, 110, "chape U (kit)")
rect(OX, -70, 117, -50, 158, fill=C["servo"])
rect(OX, -85, 134, -70, 141, fill=C["fill"]); rect(OX, -50, 134, -35, 141, fill=C["fill"])
bolt(OX, -80, 137.5); bolt(OX, -40, 137.5)
txt(OX, 20, 137, "TILT bride par oreilles (4 vis)")
# arbre tilt -X -> palonnier vertical VISSE au bras roll
rect(OX, -84, 134, -70, 141, fill=C["metal"])
rect(OX, -87, 125, -84, 150, fill=C["metal"])
bolt(OX, -85.5, 130); bolt(OX, -85.5, 145)
txt(OX, -200, 165, "palonnier VISSE")
txt(OX, -200, 150, "au bras (2 vis)")
# bras roll + berceau
rect(OX, -95, 125, -89, 205, fill=C["metal"])
rect(OX, -90, 205, -30, 211, fill=C["metal"])
bolt(OX, -89.5, 208)
txt(OX, 20, 190, "bras roll")
# --- ROLL : servo en berceau, palonnier VISSE au support tablette ---
rect(OX, -90, 211, -84, 237, fill=C["metal"]); rect(OX, -36, 211, -30, 237, fill=C["metal"])
rect(OX, -80, 211, -39, 254, fill=C["servo"])
rect(OX, -87, 211, -33, 216, fill=C["fill"])
bolt(OX, -87, 224); bolt(OX, -33, 224)
txt(OX, 20, 225, "ROLL en berceau (vis oreilles)")
rect(OX, -63, 254, -57, 263, fill=C["metal"])
rect(OX, -75, 263, -45, 266, fill=C["metal"])
bolt(OX, -68, 266); bolt(OX, -52, 266)
rect(OX, -100, 266, -20, 276, fill=C["fill"])
txt(OX, 20, 262, "support VISSE au palonnier")
# tablette
rect(OX, -168, 276, 48, 406, fill=(40, 40, 45))
rect(OX, -158, 286, 38, 396, fill=C["screen"])
for ex in (-100, -20):
    d.ellipse([X(ex - 14, OX), Z(365), X(ex + 14, OX), Z(337)], fill=C["eye"])
    d.ellipse([X(ex + 1, OX), Z(360), X(ex + 9, OX), Z(352)], fill="white")
d.ellipse([X(-64, OX), Z(403), X(-56, OX), Z(395)], fill="black")
d.rectangle([X(-72, OX), Z(285), X(-48, OX), Z(279)], fill=(230, 230, 230))
txt(OX, 55, 380, "Dell Venue 8\" 216x130")
# axe PAN
d.line([(X(-60, OX), Z(0)), (X(-60, OX), Z(276))], fill=C["axis"], width=1)
txt(OX, -56, 280, "axe PAN", color=C["axis"])
# cotes face
dim_v(OX, -210, 0, 25, "25")
dim_v(OX, -210, 25, 61, "36")
dim_v(OX, -210, 79, 160, "U-81")
dim_v(OX, -210, 211, 254, "43")
dim_v(OX, -210, 276, 406, "130")

# ================= VUE PROFIL =================
OX2 = 1050
txt(OX2, -140, 470, "VUE PROFIL v2 (mm)", color=C["label"])
rect(OX2, -140, 0, 140, 25, fill=C["wood"])
rect(OX2, -40, 25, -20, 61, fill=C["servo"])
rect(OX2, -60, 73, 0, 79, fill=C["metal"])
rect(OX2, 20, 25, 40, 67, fill=C["fill"])
rect(OX2, 20, 67, 40, 73, fill=C["metal"])
txt(OX2, 45, 40, "roulement")
rect(OX2, -55, 79, -5, 160, fill=C["metal"])
txt(OX2, -135, 115, "chape U")
rect(OX2, -50.5, 117, -9.5, 158, fill=C["servo"])
txt(OX2, 0, 135, "tilt")
rect(OX2, -53, 125, -47, 205, fill=C["metal"])
txt(OX2, -135, 170, "bras roll")
rect(OX2, -60, 205, 0, 211, fill=C["metal"])
rect(OX2, -40, 211, -20, 254, fill=C["servo"])
txt(OX2, 5, 228, "roll")
rect(OX2, -47, 266, -13, 276, fill=C["fill"])
rect(OX2, -37, 276, -23, 406, fill=(40, 40, 45))
d.line([(X(-23, OX2), Z(286)), (X(-23, OX2), Z(396))], fill=C["screen"], width=4)
txt(OX2, -15, 340, "ecran (+Y)")
# axes
d.line([(X(-30, OX2), Z(0)), (X(-30, OX2), Z(276))], fill=C["axis"], width=1)
txt(OX2, -26, 280, "axe PAN", color=C["axis"])
for z, n in ((137.5, "TILT"), (232, "ROLL")):
    x = X(-30, OX2)
    d.ellipse([(x - 8, Z(z) - 8), (x + 8, Z(z) + 8)], outline=C["axis"], width=2)
    d.line([(x - 8, Z(z)), (x + 8, Z(z))], fill=C["axis"], width=2)
    txt(OX2, -130, z + 8, f"axe {n}", color=C["axis"])
dim_v(OX2, 160, 0, 25, "25")
dim_v(OX2, 160, 25, 79, "54")
dim_v(OX2, 160, 79, 160, "81")
dim_v(OX2, 160, 205, 254, "49")
dim_v(OX2, 160, 276, 406, "130")
dim_v(OX2, 200, 0, 406, "TOTAL ~406")

img.save("C:/Users/mathias/AppData/Local/hermes/cache/scratch/plan-2d-v2.png")
print("PLAN-V2-OK")
