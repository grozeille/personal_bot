"""Plan 2D v4 : kit reel photo (U interieur 52, pattes 48, trous 10mm),
tilt config R_y(-90) type photo, mur oreille, pince a machoires. TOTAL 275.
Unites mm. A valider au pied a coulisse sur kit recu."""
from PIL import Image, ImageDraw, ImageFont

try:
    F = ImageFont.load_default(20)
    FS = ImageFont.load_default(16)
except Exception:
    F = FS = ImageFont.load_default()

W, H = 1700, 1120
img = Image.new("RGB", (W, H), "white")
d = ImageDraw.Draw(img)
S = 1.6
def X(xmm, ox): return ox + xmm * S
def Z(zmm): return H - 60 - zmm * S

C = {"fill": (235, 235, 235), "screen": (150, 210, 255),
     "eye": (20, 170, 230), "axis": "red", "dim": (120, 120, 120),
     "label": (70, 70, 70), "servo": (200, 200, 205),
     "pan": (230, 30, 30), "tilt": (255, 130, 0), "kit": (30, 180, 60),
     "horn": (250, 230, 30), "sup": (40, 200, 230), "gold": (220, 180, 60),
     "steel": (150, 150, 160), "wash": (200, 240, 200),
     "metal": (190, 195, 200), "wood": (210, 175, 120)}

def rect(ox, x0, z0, x1, z1, fill=None, outline="black", width=2):
    d.rectangle([X(x0, ox), Z(z1), X(x1, ox), Z(z0)], fill=fill, outline=outline, width=width)

def txt(ox, xmm, zmm, s, font=F, color="black"):
    d.text((X(xmm, ox), Z(zmm)), s, fill=color, font=font)

def bolt(ox, xmm, zmm, fill="black"):
    d.ellipse([X(xmm - 2.5, ox), Z(zmm + 2.5), X(xmm + 2.5, ox), Z(zmm - 2.5)], fill=fill)

def dim_v(ox, xmm, z0, z1, label):
    x = X(xmm, ox)
    d.line([(x, Z(z0)), (x, Z(z1))], fill=C["dim"], width=1)
    d.line([(x - 5, Z(z0)), (x + 5, Z(z0))], fill=C["dim"], width=1)
    d.line([(x - 5, Z(z1)), (x + 5, Z(z1))], fill=C["dim"], width=1)
    d.text((x + 6, (Z(z0) + Z(z1)) / 2 - 10), label, fill=C["dim"], font=FS)

# ================= VUE FACE =================
OX = 420
txt(OX, -170, 530, "VUE FACE v4 (mm) - kit reel", color=C["label"])
txt(OX, -160, 390, "rouge=PAN  orange=TILT  vert=KIT  jaune=PALONNIER", font=FS, color=C["label"])
txt(OX, -160, 370, "cyan=PINCE  or=ROULEMENT  gris=VIS", font=FS, color=C["label"])
# base
rect(OX, -100, 0, 100, 18, fill=C["wood"]); txt(OX, 105, 5, "base bois 200x200 ep.18")
# pan (corps decale -3.2, arbre sur axe)
rect(OX, -23.6, 18, 17.2, 54, fill=C["pan"])
rect(OX, -30.5, 50, -23.5, 54, fill=C["pan"]); rect(OX, 17.1, 50, 24.1, 54, fill=C["pan"])
txt(OX, 48, 30, "PAN MG996R (arbre +3.2)")
# bracket + pattes
rect(OX, -30, 54, 30, 56, fill=C["kit"])
rect(OX, -25.5, 56, -22.5, 78, fill=C["kit"]); rect(OX, 22.5, 56, 25.5, 78, fill=C["kit"])
bolt(OX, -24, 67); bolt(OX, -24, 77); bolt(OX, 24, 67); bolt(OX, 24, 77)
txt(OX, -150, 60, "platine + pattes 48")
# palonnier rond pan + U base
# palonnier rond O21 visse sous U base
rect(OX, -10.5, 56, 10.5, 59, fill=C["horn"])
rect(OX, -28, 59, 28, 61, fill=C["kit"])
txt(OX, 48, 56, "palonnier rond VISSE")
# zone U + bras
d.rectangle([X(-28, OX), Z(133), X(28, OX), Z(59)], fill=C["wash"], outline=None)
rect(OX, -28, 61, -26, 113, fill=C["kit"]); rect(OX, 26, 61, 28, 113, fill=C["kit"])
txt(OX, -150, 95, "U kit 56 (interieur 52)")
# pivots pattes (vis dans trous bas U a 77.5)
bolt(OX, -26.5, 77.5, fill=C["gold"]); bolt(OX, 26.5, 77.5, fill=C["gold"])
# mur oreille + vis
rect(OX, -21, 55, -18, 95, fill=C["kit"])
bolt(OX, -19.5, 86.3, fill=C["steel"])
txt(OX, -150, 70, "mur oreille (a valider)")
# tilt corps R_y + arbre + palonnier + roulement
rect(OX, -26, 89.6, 11, 129.9, fill=C["tilt"])
d.line([(X(-33, OX), Z(113)), (X(-26, OX), Z(113))], fill=C["horn"], width=3)
rect(OX, -34, 110, -29, 116, fill=C["horn"])
rect(OX, 26, 110, 28, 116, fill=C["gold"])
txt(OX, 40, 105, "TILT DS3218 (R_y)")
# oreilles haut/bas + trou
rect(OX, -17.5, 129.9, -14.5, 136.6, fill=C["tilt"])
rect(OX, -17.5, 82.9, -14.5, 89.6, fill=C["tilt"])
txt(OX, 40, 128, "oreilles haut/bas")
# axe TILT
d.line([(X(-34, OX), Z(113)), (X(34, OX), Z(113))], fill=C["axis"], width=1)
txt(OX, -75, 117, "axe TILT", color=C["axis"])
# pince + tablette
rect(OX, -34, 121, -31, 145, fill=C["sup"])
rect(OX, -40, 139, -20, 159, fill=C["sup"]); rect(OX, 20, 139, 40, 159, fill=C["sup"])
rect(OX, -108, 145, 108, 275, fill=(40, 40, 45))
rect(OX, -98, 155, 98, 265, fill=C["screen"])
for ex in (-50, 50):
    d.ellipse([X(ex - 14, OX), Z(232), X(ex + 14, OX), Z(204)], fill=C["eye"])
    d.ellipse([X(ex + 1, OX), Z(227), X(ex + 9, OX), Z(219)], fill="white")
d.ellipse([X(-4, OX), Z(272), X(4, OX), Z(264)], fill="black")
d.rectangle([X(-12, OX), Z(164), X(12, OX), Z(158)], fill=(230, 230, 230))
txt(OX, 112, 200, "Dell 216x130")
txt(OX, 44, 142, "pince")
# axe PAN
d.line([(X(0, OX), Z(0)), (X(0, OX), Z(145))], fill=C["axis"], width=1)
txt(OX, -42, 148, "axe PAN", color=C["axis"])
# cotes face : 18+43+52+32+130=275
dim_v(OX, -180, 0, 18, "18")
dim_v(OX, -180, 18, 61, "43")
dim_v(OX, -180, 61, 113, "52")
dim_v(OX, -180, 113, 145, "32")
dim_v(OX, -180, 145, 275, "130")

# ================= VUE PROFIL =================
OX2 = 1050
txt(OX2, -140, 530, "VUE PROFIL v4 (mm)", color=C["label"])
rect(OX2, -100, 0, 100, 18, fill=C["wood"])
rect(OX2, -9.85, 18, 9.85, 54, fill=C["pan"])
rect(OX2, -30, 54, 30, 56, fill=C["kit"])
rect(OX2, -10, 56, 10, 78, fill=C["kit"])  # pattes (2 superposees)
txt(OX2, -135, 62, "pattes")
d.rectangle([X(-20, OX2), Z(133), X(20, OX2), Z(59)], fill=C["wash"], outline=None)
rect(OX2, -20, 61, 20, 113, fill=C["kit"])
d.ellipse([X(-20, OX2), Z(133), X(20, OX2), Z(93)], outline=C["kit"], width=2)
txt(OX2, -135, 100, "U (prof)")
rect(OX2, -10, 89.6, 10, 129.9, fill=C["tilt"])
txt(OX2, 25, 108, "tilt")
d.rectangle([X(-10, OX2), Z(95), X(10, OX2), Z(55)], outline=(30, 180, 60), width=2)
txt(OX2, -135, 80, "mur")
bolt(OX2, 0, 77.5, fill=C["gold"])
d.ellipse([X(-12, OX2), Z(125), X(12, OX2), Z(101)], outline=C["horn"], width=2)
bolt(OX2, 0, 113, fill=C["horn"])
rect(OX2, -30, 137, 30, 145, fill=C["sup"])
rect(OX2, -7, 145, 7, 275, fill=(40, 40, 45))
d.line([(X(7, OX2), Z(155)), (X(7, OX2), Z(265))], fill=C["screen"], width=4)
txt(OX2, 12, 205, "ecran (+Y)")
d.line([(X(0, OX2), Z(0)), (X(0, OX2), Z(145))], fill=C["axis"], width=1)
txt(OX2, -42, 148, "axe PAN", color=C["axis"])
x = X(0, OX2); z = 113
d.ellipse([(x - 8, Z(z) - 8), (x + 8, Z(z) + 8)], outline=C["axis"], width=2)
d.line([(x - 8, Z(z)), (x + 8, Z(z))], fill=C["axis"], width=2)
txt(OX2, -135, z + 8, "axe TILT", color=C["axis"])
dim_v(OX2, 130, 0, 18, "18")
dim_v(OX2, 130, 18, 61, "43")
dim_v(OX2, 130, 61, 113, "52")
dim_v(OX2, 130, 113, 145, "32")
dim_v(OX2, 130, 145, 275, "130")
dim_v(OX2, 170, 0, 275, "TOTAL 275")

img.save("C:/Users/mathias/Work/Projects/personal_bot/hardware/plan-2d-v4.png")
print("PLAN-V4-OK")
