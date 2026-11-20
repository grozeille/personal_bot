# BOM — tête robotisée 3-DOF (d'après issue #10)

Prix indicatifs Amazon.ca, nov. 2026 — à vérifier au moment de commander.
Statut : à inventorier (cocher ce qui est déjà en stock).

## Électronique

| Pièce | Spec | Qté | Prix indicatif | Note |
|---|---|---|---|---|
| Servo Pan + Roll | MG996R, couple ~10 kg·cm, pignons métal | 2 | ~18 $/pc | Palonniers plastiques inclus ; prévoir palonniers alu 25T séparés |
| Servo Tilt | DS3218, couple ~20 kg·cm, pignons métal | 1 | ~30 $ | Plus fort : il porte la tablette en porte-à-faux |
| Contrôleur PWM | PCA9685 16 canaux, I2C | 1 | ~15 $ | SDA/SCL/3.3V/GND vers le Pi |
| Raspberry Pi | Modèle à confirmer (le projet mentionne Pi 1) | 1 | selon modèle | Pi 3B+ ou 4 conseillé pour FastAPI + marge ; prévoir carte microSD 32 Go + alim USB-C/5V 3A dédiée |
| Alim servos | 5 V / 4 A minimum, jack ou bornier | 1 | ~20 $ | Ligne séparée du Pi (jamais le 5V du Pi pour les servos) |
| Condensateur | 1000 µF / 10 V+ électrolytique | 1 | ~2 $ | Sur V+ du PCA9685, anti-reset du Pi en pointe de courant |

## Mécanique

| Pièce | Spec | Qté | Prix indicatif | Note |
|---|---|---|---|---|
| Base bois lestée | 20×20 cm, ép. ≥ 18 mm | 1 | récup/chute | Percer + visser le socle Pan ; lester si besoin |
| Équerres alu | Profilé ou équerres 30×30 mm | 2–3 | ~10 $ | Articulations Tilt/Roll (ou pièces imprimées 3D — voir CAO) |
| Palonniers alu 25T | Bras métal 25 dents (MG996R/DS3218) | 3 | ~10 $/lot | Mise au neutre 90° avant vissage (cf. issue #10) |
| Pince tablette | Pince universelle 8" (Dell Venue 8 Pro, 216×130 mm) | 1 | ~15 $ | Fixation sur Roll, coins dégagés |
| Visserie | M3/M4 + écrous frein, entretoises laiton M3 | 1 lot | ~12 $ | |
| Câblage | Dupont femelle-femelle 20 cm + fil silicone 18AWG (alim) | 1 lot | ~12 $ | Mou suffisant pour ±60° Pan, ±25° Tilt/Roll |

## Total indicatif (sans Pi ni tablette) : ~120–150 $CA

## Étapes (cf. issue #10)
1. Pi OS Lite + I2C (`raspi-config`), câblage PCA9685, alim + condo.
2. Neutre 90° des 3 servos (canaux 0/1/2) avant vissage des palonniers.
3. Montage bois → équerres → pince tablette, câbles avec mou.
4. Bascule `appsettings.json` : IP simu → `raspberrypi.local`.
5. Test stabilité élec (mouvements 3 axes combinés) puis grandeur nature.
