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
        "corps 40.7x19.7x36 + oreilles + arbre + palonnier O19.5 (avec la piece)"),
    "tilt_ds3218": ("Servo TILT RDS3218 (orange)",
        {"face": "48 x 40", "dessus": "48 x 20", "gauche": "20 x 40"},
        "rework atelier : arbre cote +X a -8, palonnier O19.5, sans idler"),
    "rds_haut": ("Chape base RDS (orange)",
        {"face": "56 x 34", "dessus": "56 x 20", "gauche": "20 x 34"},
        "rework atelier : plaque 55.6 + 2 joues 31.5 en bas, sans percages"),
    "rds_bas": ("Chape haute RDS (orange)",
        {"face": "45 x 34", "dessus": "45 x 20", "gauche": "20 x 34"},
        "rework atelier : plaque 44.5 + 2 joues 27 en haut, sans percages"),
    "u_court_48": ("U court rework (vert)",
        {"face": "56 x 42", "dessus": "56 x 20", "gauche": "20 x 42"},
        "repere assemblage : plaque 40x42 + plancher 40x20 + bandes + bras"),
    "support_pince": ("Pince X ressort (cyan)",
        {"face": "79 x 156", "dessus": "79 x 35", "gauche": "35 x 156"},
        "4.7x3.1in + rotule + filetage 1/4"),
    "tablette": ("Tablette Dell Venue 8 Pro",
        {"face": "216 x 130", "dessus": "216 x 9", "gauche": "9 x 130"},
        "ecran bleu = dalle emissive"),
    "base_bois": ("Base bois (marron)",
        {"face": "200 x 18", "dessus": "200 x 200", "gauche": "200 x 18"},
        "ep. 18mm, tout est visse dessus"),
}

CELL_W, CELL_H = 400, 300
thumbs = []


def cell_from(png, label, sub):
    im = Image.open(png).convert("RGBA")
    bbox = im.split()[3].getbbox()
    if bbox:
        im = im.crop(bbox)
    im.thumbnail((CELL_W - 40, CELL_H - 80), Image.LANCZOS)
    cell = Image.new("RGB", (CELL_W, CELL_H), "white")
    cell.paste(im, ((CELL_W - im.width) // 2, 30), im)
    d = ImageDraw.Draw(cell)
    d.text((10, 5), label, fill="black", font=FS)
    d.text((10, CELL_H - 22), sub, fill=(90, 90, 90), font=FS)
    return cell


for piece, (title, dims, note) in FICHES.items():
    cells = []
    for view in ("face", "dessus", "gauche"):
        p = os.path.join(VUES, f"{piece}_{view}.png")
        cells.append(cell_from(p, view.upper(), dims[view] + " mm"))
    p = os.path.join(VUES, f"{piece}_iso.png")
    cells.append(cell_from(p, "ISO", "volume"))
    W = CELL_W * 3 + 40
    H = CELL_H * 2 + 110
    fiche = Image.new("RGB", (W, H), "white")
    d = ImageDraw.Draw(fiche)
    d.text((15, 8), title, fill="black", font=F)
    d.text((15, 36), note + "  |  cotes modele (mm) - a valider au pied a coulisse",
           fill=(110, 110, 110), font=FS)
    for i, c in enumerate(cells[:3]):
        fiche.paste(c, (10 + i * (CELL_W + 10), 65))
    fiche.paste(cells[3], (10 + (CELL_W + 10), 65 + CELL_H + 10))
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
