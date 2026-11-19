# Visages — SVG

13 visages : 12 expressions redessinées à partir de l'image de référence
+ 1 dialogue de confirmation de mise à jour (pas une émotion).

## Grille commune (ne pas changer d'un fichier à l'autre)

- `viewBox="0 0 800 550"` — ratio 1,45, proche des cadres de l'image (413 × 286 px)
- Cadre : `rect x=10 y=10 w=780 h=530 rx=30`, contour blanc 5 px, fond noir
- Yeux : centres ≈ (246, 269) et (552, 269) pour happy — mesurés sur le
  cœur cyan de l'image (sans le halo), pas estimés à l'œil.
- Bouche : centrée sur x=400, y ≈ 350–375
- Couleur cyan : `#8EDCE9` — cœur des yeux mesuré sur l'image (142, 220, 233).
- Groupes `id="eyes"` / `id="mouth"` dans chaque fichier, pour retrouver
  facilement les formes à animer. Variantes suffixées autorisées :
  `eyes-left`, `eyes-right`, ... (tout `eyes-*` rejoint le calque des yeux,
  tout `mouth-*` celui de la bouche) ; tout autre groupe fait échouer le
  chargement (garde-fou anti-fautes de frappe).

## Fichiers (référence auteur)

| Fichier | Expression |
|---|---|
| 01-happy.svg | yeux ovales, petit sourire |
| 02-dead.svg | yeux X X, bouche plate |
| 03-angry.svg | yeux inclinés vers le nez, petit sourire |
| 04-excited.svg | yeux &gt; &lt;, sourire |
| 05-surprised.svg | yeux ovales, bouche ouverte en D |
| 06-bored.svg | yeux mi-clos, bouche plate |
| 07-love.svg | yeux cœurs, sourire |
| 08-joyful.svg | yeux fermés en ^ ^, bouche grande ouverte |
| 09-wink.svg | œil gauche ovale, œil droit &lt;, sourire |
| 10-sleepy.svg | yeux fermés en arc, bouche plate |
| 11-thinking.svg | regard vers le haut à gauche, bouche en biais |
| 12-shocked.svg | yeux dilatés, bouche en O |
| 13-confirm.svg | œil gauche X (annuler/rester), œil droit ✓ (confirmer/quitter), bouche plate |

Ces fichiers ne sont plus lus par l'application : ils restent la référence
visuelle d'auteur. Le rendu utilise la bibliothèque de parties ci-dessous.

## Bibliothèque de parties (`parts/`, source du rendu)

Un fichier SVG par élément et par déclinaison, tous dans le même repère
`viewBox="0 0 800 550"` (coordonnées directement composables, sans
transformation). Chaque visage est composé de 3 parties partagées : deux
visages utilisant la même partie affichent exactement les mêmes pixels,
ce qui saute la transition entre elles.

| Partie | Donneur canonique | Utilisée par |
|---|---|---|
| eye-left-oval | 01 (absorbe les décalages de 5, 9 et 11) | 1, 5, 9, 11 |
| eye-left-big-oval | 12 | 12 |
| eye-left-x | 02 | 2, 13 |
| eye-left-blade | 03 | 3 |
| eye-left-chevron | 04 | 4 |
| eye-left-dome | 06 | 6 |
| eye-left-heart | 07 | 7 |
| eye-left-arc-up | 08 | 8 |
| eye-left-arc-down | 10 | 10 |
| eye-right-oval | 01 (absorbe le décalage de 5 et 11) | 1, 5, 11 |
| eye-right-big-oval | 12 | 12 |
| eye-right-x | 02 | 2 |
| eye-right-blade | 03 | 3 |
| eye-right-chevron | 04 (09 avait un chevron plus grand, unifié) | 4, 9 |
| eye-right-check | 13 | 13 |
| eye-right-dome | 06 | 6 |
| eye-right-heart | 07 | 7 |
| eye-right-arc-up | 08 | 8 |
| eye-right-arc-down | 10 | 10 |
| mouth-smile | 01 (03 décalé de 2 px, unifié) | 1, 3, 4, 7, 9 |
| mouth-flat | 02 (= 10 à l'octet ; 06 plus court/fin/bas, unifié) | 2, 6, 10, 13 |
| mouth-open-d | 05 | 5 |
| mouth-big-open | 08 | 8 |
| mouth-slant | 11 | 11 |
| mouth-ring | 12 | 12 |

## Utilisation dans TabletApp

`SvgFaceLoader` embarque `parts/*.svg` en ressources, les met en cache
(`SKPicture` partagés) et compose chaque émotion via sa table de
composition ; `FacePainter` dessine les 3 calques à 60 FPS.
Le self-test (`--self-test`) vérifie que les partages tiennent au pixel
(`FaceEquivalence`) et rend la planche des 12 visages.

## Glow

Volontairement absent des SVG : style plat façon EVE. Voir la discussion
dans le chat — le glow est un effet à ajouter au rendu SkiaSharp
(`SKMaskFilter.CreateBlur`), pas une propriété de la forme.

## Corrections v2 (18 nov.)

- **#3 angry** : les yeux ne sont pas des coins mais des demi-disques :
  bord supérieur droit en diagonale vers le nez, pointe à l'extérieur,
  bas en demi-ellipse. Mesuré : 81 × 63, pente du bord sup. ≈ 27 px
  de chute sur la largeur.
- **#5 / #8 bouche en D, #6 yeux demi-disque** : coins supérieurs
  arrondis avec des `Q` (rayon 12–22 px) au lieu d'angles à 90°.
  Technique : un demi-cercle rempli ne s'arrondit pas tout seul, il
  faut arrondir explicitement les deux coins du haut.
- **#8 yeux** : vrai demi-cercle avec la commande `A 35 35` (arc
  elliptique), pas un `Q` qui donne une parabole en pointe. Épaisseur
  du trait 22 px, extrémités `round`.
- **Tous les visages** : positions des yeux corrigées d'après la mesure
  (ils étaient trop rapprochés en v1 : 305/495 au lieu de ~246/552).
- Renders de vérification (CairoSVG) dans `render/` — les 4 visages
  corrigés ont été comparés visuellement aux crops de l'image source
  dans `crops/`.

## Bibliothèque de parties (19 nov.)

- Extraction des 24 parties canoniques dans `parts/` depuis les visages
  ci-dessus (géométrie copiée à l'identique du donneur, titre du fichier
  = donneur + utilisateurs).
- Unifications : ovales (01 absorbe 05/09/11, positions incluses),
  chevron droit (04 absorbe 09), sourire (01 absorbe 03), bouche plate
  (02 absorbe 06).
