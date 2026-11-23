"""Plan 2D v4 : rework atelier (assemblage_2axes.blend).
Pan axe x=+6.4, tilt axe z=92 (arbre cote +X). TOTAL 283.
Unites mm."""
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
txt(OX, -170, 570, "VUE FACE v4 (mm) - rework atelier", color=C["label"])
txt(OX, -160, 390, "rouge=PAN  orange=TILT  vert=KIT  jaune=PALONNIER", font=FS, color=C["label"])
txt(OX, -160, 370, "cyan=PINCE  gris=VIS", font=FS, color=C["label"])
# base
rect(OX, -100, 0, 100, 18, fill=C["wood"])
txt(OX, 105, 5, "base bois 200x200 ep.18")
# U rework autour du pan (sol + mur + montants + bras plats)
rect(OX, -16.8, 17, 23.2, 19, fill=C["kit"])
rect(OX, -16.8, -4, 23.2, 38, fill=C["kit"])
rect(OX, 23.2, 24, 31.2, 52, fill=C["kit"]); rect(OX, -24.8, 24, -16.8, 52, fill=C["kit"])
rect(OX, 23.2, 50, 31.2, 52, fill=C["kit"]); rect(OX, -24.8, 50, -16.8, 52, fill=C["kit"])
txt(OX, -150, 30, "U rework (sol+mur+montants)")
# pan MG996R
rect(OX, -17.15, 20, 23.55, 56, fill=C["pan"])
rect(OX, 23.5, 52, 30.5, 56, fill=C["pan"]); rect(OX, -24.1, 52, -17.1, 56, fill=C["pan"])
txt(OX, 48, 30, "PAN MG996R")
# disque rouge + arbre (axe x=+6.4)
rect(OX, -3.35, 60, 16.15, 63, fill=C["pan"])
d.line([(X(6.4, OX), Z(52)), (X(6.4, OX), Z(60))], fill=C["steel"], width=3)
txt(OX, 48, 56, "disque rouge MG996R")
# chape base 55.6
rect(OX, -24.1, 63, 31.5, 65, fill=C["tilt"])
rect(OX, 31.5, 63, 33.5, 94.5, fill=C["tilt"]); rect(OX, -26.1, 63, -24.1, 94.5, fill=C["tilt"])
txt(OX, -150, 80, "chape base 55.6")
# tilt corps + arbre + palonnier (arbre +X, z=92)
rect(OX, -16.3, 80, 23.7, 120, fill=C["tilt"])
d.line([(X(15, OX), Z(92)), (X(29, OX), Z(92))], fill=C["horn"], width=3)
rect(OX, 28.6, 82.25, 31.6, 101.75, fill=C["horn"])
txt(OX, 40, 100, "TILT RDS3218")
# axe TILT
d.line([(X(-34, OX), Z(92)), (X(40, OX), Z(92))], fill=C["axis"], width=1)
txt(OX, -75, 96, "axe TILT", color=C["axis"])
# chape haute 44.5
rect(OX, 25.9, 100, 27.9, 127, fill=C["tilt"]); rect(OX, -20.5, 100, -18.5, 127, fill=C["tilt"])
rect(OX, -18.55, 125, 25.95, 127, fill=C["tilt"])
txt(OX, -150, 140, "chape haute 44.5")
# colonne pince : base 1/4 + rotule + colonne
rect(OX, -12.5, 127, 19.5, 137, fill=C["sup"])
rect(OX, -2.5, 131, 9.5, 151, fill=C["sup"])
d.ellipse([X(-7.5, OX), Z(167), X(14.5, OX), Z(145)], outline=C["sup"], width=2)
rect(OX, -14.5, 150, -6.5, 162, fill=(150, 150, 150))
rect(OX, -6.5, 146, 13.5, 176, fill=C["sup"])
rect(OX, -16.5, 199, 23.5, 269, fill=C["sup"])
txt(OX, 44, 155, "rotule")
# pince : dos + bras X + crochets + levres
rect(OX, -24.5, 166, 31.5, 246, fill=C["sup"])
for _x0, _z0, _x1, _z1 in ((-24, 166, -52, 240), (-24, 246, -52, 172),
                          (31, 166, 59, 240), (31, 246, 59, 172)):
    d.line([(X(_x0, OX), Z(_z0)), (X(_x1, OX), Z(_z1))], fill=C["sup"], width=5)
rect(OX, 36, 129, 50, 147, fill=C["sup"]); rect(OX, -43, 129, -29, 147, fill=C["sup"])
rect(OX, 36, 265, 50, 283, fill=C["sup"]); rect(OX, -43, 265, -29, 283, fill=C["sup"])
rect(OX, 36, 134, 50, 141.5, fill=(150, 150, 150)); rect(OX, -43, 134, -29, 141.5, fill=(150, 150, 150))
rect(OX, 36, 271, 50, 278.5, fill=(150, 150, 150)); rect(OX, -43, 271, -29, 278.5, fill=(150, 150, 150))
rect(OX, -19.5, 175, 26.5, 237, fill=C["sup"])
txt(OX, 60, 200, "pince X")
# tablette + ecran
rect(OX, -104.5, 141, 111.5, 271, fill=(40, 40, 45))
rect(OX, -94.5, 151, 101.5, 261, fill=C["screen"])
for ex in (-50, 50):
    d.ellipse([X(ex - 14, OX), Z(242), X(ex + 14, OX), Z(214)], fill=C["eye"])
    d.ellipse([X(ex + 1, OX), Z(237), X(ex + 9, OX), Z(229)], fill="white")
d.ellipse([X(-4, OX), Z(268), X(4, OX), Z(260)], fill="black")
txt(OX, 112, 210, "Dell 216x130")
# axe PAN
d.line([(X(6.4, OX), Z(0)), (X(6.4, OX), Z(141))], fill=C["axis"], width=1)
txt(OX, -42, 120, "axe PAN", color=C["axis"])
# cotes face : 18+45+29+49+142=283
dim_v(OX, -180, 0, 18, "18")
dim_v(OX, -180, 18, 63, "45")
dim_v(OX, -180, 63, 92, "29")
dim_v(OX, -180, 92, 141, "49")
dim_v(OX, -180, 141, 283, "142")
dim_v(OX, -220, 0, 283, "TOTAL 283")

# ================= VUE PROFIL =================
OX2 = 1050
txt(OX2, -140, 570, "VUE PROFIL v4 (mm)", color=C["label"])
rect(OX2, -100, 0, 100, 18, fill=C["wood"])
rect(OX2, -10, 17, 10, 19, fill=C["kit"])
rect(OX2, 10, -4, 12, 38, fill=C["kit"])
rect(OX2, 10, 24, 12, 52, fill=C["kit"])
rect(OX2, -10, 50, 10, 52, fill=C["kit"])
txt(OX2, -135, 30, "U rework")
rect(OX2, -9.85, 20, 9.85, 56, fill=C["pan"])
d.ellipse([X(-9.75, OX2), Z(63 + 9.75), X(9.75, OX2), Z(63 - 9.75)], outline=C["pan"], width=2)
rect(OX2, -10, 63, 10, 65, fill=C["tilt"])
rect(OX2, -10, 63, 10, 94.5, fill=C["tilt"])
txt(OX2, -135, 80, "chape base")
rect(OX2, -10, 80, 10, 120, fill=C["tilt"])
d.ellipse([X(-10, OX2), Z(92 + 10), X(10, OX2), Z(92 - 10)], outline=C["horn"], width=2)
bolt(OX2, 0, 92, fill=C["horn"])
rect(OX2, -10, 100, 10, 127, fill=C["tilt"])
rect(OX2, -10, 125, 10, 127, fill=C["tilt"])
txt(OX2, -135, 140, "chape haute")
rect(OX2, -16, 127, 16, 137, fill=C["sup"])
rect(OX2, -6, 131, 6, 151, fill=C["sup"])
d.ellipse([X(-11, OX2), Z(167), X(11, OX2), Z(145)], outline=C["sup"], width=2)
rect(OX2, -13, 146, 5, 176, fill=C["sup"])
rect(OX2, -16, 199, -10, 269, fill=C["sup"])
rect(OX2, -19, 166, -11, 246, fill=C["sup"])
rect(OX2, -25.5, 175, -20.5, 237, fill=C["sup"])
rect(OX2, -23, 129, -15, 147, fill=C["sup"])
rect(OX2, -23, 265, -15, 283, fill=C["sup"])
txt(OX2, 20, 160, "rotule+pince")
rect(OX2, -34.5, 141, -25.5, 271, fill=(40, 40, 45))
d.line([(X(-34.5, OX2), Z(151)), (X(-34.5, OX2), Z(261))], fill=C["screen"], width=4)
txt(OX2, 12, 210, "ecran (+Y)")
d.line([(X(6.4, OX2), Z(0)), (X(6.4, OX2), Z(141))], fill=C["axis"], width=1)
x = X(0, OX2); z = 92
d.ellipse([(x - 8, Z(z) - 8), (x + 8, Z(z) + 8)], outline=C["axis"], width=2)
d.line([(x - 8, Z(z)), (x + 8, Z(z))], fill=C["axis"], width=2)
txt(OX2, -135, z + 8, "axe TILT", color=C["axis"])
dim_v(OX2, 130, 0, 18, "18")
dim_v(OX2, 130, 18, 63, "45")
dim_v(OX2, 130, 63, 92, "29")
dim_v(OX2, 130, 92, 141, "49")
dim_v(OX2, 130, 141, 283, "142")
dim_v(OX2, 170, 0, 283, "TOTAL 283")

img.save("C:/Users/mathias/Work/Projects/personal_bot/hardware/plan-2d-v4.png")
print("PLAN-V4-OK")
