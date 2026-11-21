"""Plan 2D v3 : version BOM-budget 2 axes (pan + tilt, sans roll).
Base bois 200x200 ep18. Kit short pan-tilt alu 2mm. Tablette Dell Venue 8" 216x130 direct sur tilt.
Cotes indicatives : a relever au pied a coulisse sur kit recu. Unites mm."""
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
     "label": (70, 70, 70), "servo": (200, 200, 205),
     "pan": (230, 30, 30), "tilt": (255, 130, 0), "kit": (30, 180, 60),
     "horn": (250, 230, 30), "plot": (170, 60, 220), "sup": (40, 200, 230),
     "gold": (220, 180, 60),
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
txt(OX, -170, 470, "VUE FACE v3 (mm) - 2 axes budget", color=C["label"])
txt(OX, -160, 330, "rouge=PAN  orange=TILT  vert=KIT  jaune=PALONNIER", font=FS, color=C["label"])
txt(OX, -160, 310, "violet=PLOT  or=ROULEMENT  cyan=SUPPORT", font=FS, color=C["label"])
# base bois 200x200 ep18
rect(OX, -100, 0, 100, 18, fill=C["wood"]); txt(OX, 105, 5, "base bois 200x200 ep.18")
# --- PAN MG996R 40.7x19.7x42.9 : visse par oreilles sous plateau ---
rect(OX, -20, 18, 20, 54, fill=C["pan"])  # corps 40 large x 36 haut
rect(OX, -27, 50, -20, 54, fill=C["pan"]); rect(OX, 20, 50, 27, 54, fill=C["pan"])  # oreilles
for x in (-24, 24):
    bolt(OX, x, 52)
txt(OX, 55, 26, "PAN MG996R visse par oreilles")
# arbre + palonnier -> plateau tournant (interface VISSEE)
rect(OX, -3, 54, 3, 60, fill=C["horn"])  # arbre
rect(OX, -15, 60, 15, 63, fill=C["horn"])  # palonnier
bolt(OX, -8, 61.5); bolt(OX, 8, 61.5)
txt(OX, 70, 60, "palonnier VISSE (2 vis)")
rect(OX, -31, 63, 31, 65, fill=C["kit"])  # plateau kit
txt(OX, -95, 66, "plateau kit")
# plot passif + roulement a droite (appui sous bord plateau, degage des oreilles)
rect(OX, 28, 18, 43, 59, fill=C["plot"])
rect(OX, 28, 59, 43, 63, fill=C["gold"])
txt(OX, 48, 44, "plot + roulement")
# --- U tilt court : fond 62 large, bras H60 (tole 2mm) ---
d.rectangle([X(-31, OX), Z(125), X(31, OX), Z(65)], fill=(200, 240, 200))
rect(OX, -31, 65, -29, 125, fill=C["kit"]); rect(OX, 29, 65, 31, 125, fill=C["kit"])
rect(OX, -31, 63, 31, 65, fill=C["kit"])  # fond U
txt(OX, -155, 95, "U kit (H60)")
# --- TILT DS3218 40x20x40.5 : bride par oreilles dans U ---
rect(OX, -20, 72, 20, 112, fill=C["tilt"])
rect(OX, -27, 88, -20, 95, fill=C["tilt"]); rect(OX, 20, 88, 27, 95, fill=C["tilt"])
bolt(OX, -23.5, 91.5); bolt(OX, 23.5, 91.5)
txt(OX, 36, 90, "TILT DS3218 bride (4 vis)")
# arbre tilt cote gauche + palonnier vertical VISSE au support tablette
rect(OX, -31, 92, -20, 98, fill=C["horn"])
rect(OX, -34, 80, -31, 110, fill=C["horn"])
bolt(OX, -32.5, 86); bolt(OX, -32.5, 104)
txt(OX, -155, 125, "palonnier VISSE")
txt(OX, -155, 112, "support (2 vis)")
# palier laiton cote droit (pivot libre)
rect(OX, 29, 92, 34, 98, fill=(200, 170, 80))
txt(OX, 48, 102, "palier laiton")
# support tablette + pince
rect(OX, -40, 117, 40, 125, fill=C["sup"])
txt(OX, 48, 116, "support + pince")
# tablette Dell Venue 8" 216x130 : base a 125 pour chaine continue 18+47+60+130=255
rect(OX, -108, 125, 108, 255, fill=(40, 40, 45))
rect(OX, -98, 135, 98, 245, fill=C["screen"])
for ex in (-50, 50):
    d.ellipse([X(ex - 14, OX), Z(212), X(ex + 14, OX), Z(184)], fill=C["eye"])
    d.ellipse([X(ex + 1, OX), Z(207), X(ex + 9, OX), Z(199)], fill="white")
d.ellipse([X(-4, OX), Z(252), X(4, OX), Z(244)], fill="black")
d.rectangle([X(-12, OX), Z(144), X(12, OX), Z(138)], fill=(230, 230, 230))
txt(OX, 112, 185, "Dell 216x130")
# axe PAN vertical
d.line([(X(0, OX), Z(0)), (X(0, OX), Z(125))], fill=C["axis"], width=1)
txt(OX, -42, 128, "axe PAN", color=C["axis"])
# cotes face (somme 18+47+60+130=255)
dim_v(OX, -180, 0, 18, "18")
dim_v(OX, -180, 18, 65, "47")
dim_v(OX, -180, 65, 125, "60")
dim_v(OX, -180, 125, 255, "130")

# ================= VUE PROFIL =================
OX2 = 1050
txt(OX2, -140, 470, "VUE PROFIL v3 (mm)", color=C["label"])
rect(OX2, -100, 0, 100, 18, fill=C["wood"])
rect(OX2, -10, 18, 10, 54, fill=C["pan"])  # pan vu de cote (20 prof)
rect(OX2, -31, 63, 31, 65, fill=C["kit"])  # plateau
rect(OX2, 28, 18, 43, 59, fill=C["plot"])
rect(OX2, 28, 59, 43, 63, fill=C["gold"])
txt(OX2, 48, 35, "roulement")
rect(OX2, -25, 65, 25, 125, fill=C["kit"])  # U prof
txt(OX2, -115, 70, "U (prof)")
rect(OX2, -10, 72, 10, 112, fill=C["tilt"])  # tilt prof
txt(OX2, 30, 88, "tilt")
rect(OX2, -15, 117, 15, 125, fill=C["sup"])  # support
rect(OX2, -7, 125, 7, 255, fill=(40, 40, 45))  # tablette tranche
d.line([(X(7, OX2), Z(135)), (X(7, OX2), Z(245))], fill=C["screen"], width=4)
txt(OX2, 12, 185, "ecran (+Y)")
# axes
d.line([(X(0, OX2), Z(0)), (X(0, OX2), Z(125))], fill=C["axis"], width=1)
txt(OX2, -42, 128, "axe PAN", color=C["axis"])
x = X(0, OX2); z = 95
d.ellipse([(x - 8, Z(z) - 8), (x + 8, Z(z) + 8)], outline=C["axis"], width=2)
d.line([(x - 8, Z(z)), (x + 8, Z(z))], fill=C["axis"], width=2)
txt(OX2, -115, z + 8, "axe TILT", color=C["axis"])
dim_v(OX2, 130, 0, 18, "18")
dim_v(OX2, 130, 18, 65, "47")
dim_v(OX2, 130, 65, 125, "60")
dim_v(OX2, 130, 125, 255, "130")
dim_v(OX2, 170, 0, 255, "TOTAL 255")

img.save("C:/Users/mathias/Work/Projects/personal_bot/hardware/plan-2d-v3.png")
print("PLAN-V3-OK")
