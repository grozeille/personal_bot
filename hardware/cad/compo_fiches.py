"""Assemble les 3 vues ortho par piece en fiche cotee + planche contact.
In: scratch/vues/<piece>_{face,dessus,gauche}.png
Out: hardware/rendus/fiches/<piece>.png + planche_controle.png
"""
from PIL import Image, ImageDraw, ImageFont
import os

VUES = r"C:/Users/mathias/AppData/Local/hermes/cache/scratch/vues"
OUT = r"C:/Users/mathias/Work/Projects/personal_bot/hardware/rendus/fiches"
os.makedirs(OUT, exist_ok=True)

try:
    F = ImageFont.load_default(22)
    FS = ImageFont.load_default(16)
except Exception:
    F = FS = ImageFont.load_default()

FICHES = {
    "pan_mg996r": ("Servo PAN MG996R (rouge)",
        {"face": "54.6 x 42.0 (arbre inclus)", "dessus": "54.6 x 19.7", "gauche": "19.7 x 42.0"},
        "corps 40.7x19.7x36 + oreilles + arbre 25T decale +3.2 (standard)"),
    "tilt_ds3218": ("Servo TILT DS3218 (orange)",
        {"face": "47 x 54", "dessus": "47 x 20", "gauche": "20 x 54"},
        "corps 36.6 + oreilles, arbre decale +3.2 (standard), fixation mur photo"),
    "platine_bracket": ("Platine + pattes kit (vert)",
        {"face": "60 x 24", "dessus": "60 x 60", "gauche": "60 x 24"},
        "pattes entraxe 48, trous 10mm, grille + centre O8"),
    "plateau": ("Plateau tournant (vert)",
        {"face": "62 x 2", "dessus": "62 x 62", "gauche": "62 x 2"},
        "visse sur palonnier pan (4 vis auto)"),
    "U_kit": ("Chape U kit (vert)",
        {"face": "56 x 72", "dessus": "56 x 40", "gauche": "40 x 72"},
        "tole 2mm, interieur 52, axe a 52 du socle"),
    "palonnier_pan": ("Palonnier rond PAN O21 (jaune)",
        {"face": "21 x 3", "dessus": "21 x 21", "gauche": "21 x 3"},
        "centre + 4 trous, visse sous socle U"),
    "palonnier_tilt": ("Palonnier rond TILT O21 (jaune)",
        {"face": "21 x 3", "dessus": "21 x 21", "gauche": "21 x 3"},
        "centre + 2 vis, visse au bras du U"),
    "bras_alu_25t": ("Bras alu 25T 35mm (jaune)",
        {"face": "35 x 6", "dessus": "35 x 13", "gauche": "13 x 6"},
        "alesage O6 + serrage, trous M3 a 15/20/24"),
    "support_pince": ("Pince tablette (cyan)",
        {"face": "80 x 50", "dessus": "80 x 60", "gauche": "60 x 50"},
        "dos + 4 machoires, serrage bord tablette"),
    "tablette": ("Tablette Dell Venue 8 Pro",
        {"face": "216 x 130", "dessus": "216 x 9", "gauche": "9 x 130"},
        "ecran bleu = dalle emissive"),
    "base_bois": ("Base bois (marron)",
        {"face": "200 x 18", "dessus": "200 x 200", "gauche": "200 x 18"},
        "ep. 18mm, tout est visse dessus"),
}

CELL_W, CELL_H = 400, 300
thumbs = []
for piece, (title, dims, note) in FICHES.items():
    cells = []
    for view in ("face", "dessus", "gauche"):
        p = os.path.join(VUES, f"{piece}_{view}.png")
        im = Image.open(p).convert("RGBA")
        bbox = im.split()[3].getbbox()
        if bbox:
            im = im.crop(bbox)
        im.thumbnail((CELL_W - 40, CELL_H - 80), Image.LANCZOS)
        cell = Image.new("RGB", (CELL_W, CELL_H), "white")
        cell.paste(im, ((CELL_W - im.width) // 2, 30), im)
        d = ImageDraw.Draw(cell)
        d.text((10, 5), view.upper(), fill="black", font=FS)
        d.text((10, CELL_H - 22), dims[view] + " mm", fill=(90, 90, 90), font=FS)
        cells.append(cell)
    W = CELL_W * 3 + 40
    H = CELL_H + 90
    fiche = Image.new("RGB", (W, H), "white")
    d = ImageDraw.Draw(fiche)
    d.text((15, 8), title, fill="black", font=F)
    d.text((15, 36), note + "  |  cotes modele (mm) - a valider au pied a coulisse",
           fill=(110, 110, 110), font=FS)
    for i, c in enumerate(cells):
        fiche.paste(c, (10 + i * (CELL_W + 10), 65))
    fp = os.path.join(OUT, f"{piece}.png")
    fiche.save(fp)
    print("FICHE-OK", piece)
    thumbs.append(fiche.resize((fiche.width // 3, fiche.height // 3)))

cols = 2
rows = (len(thumbs) + cols - 1) // cols
tw, th = thumbs[0].size
sheet = Image.new("RGB", (cols * tw, rows * th), "white")
for i, t in enumerate(thumbs):
    sheet.paste(t, ((i % cols) * tw, (i // cols) * th))
sheet.save(os.path.join(OUT, "planche_controle.png"))
print("PLANCHE-OK")
