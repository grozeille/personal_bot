# Visages — SVG

10 expressions, redessinées à partir de l'image de référence.

## Grille commune (ne pas changer d'un fichier à l'autre)

- `viewBox="0 0 800 550"` — ratio 1,45, proche des cadres de l'image (413 × 286 px)
- Cadre : `rect x=10 y=10 w=780 h=530 rx=30`, contour blanc 5 px, fond noir
- Yeux : centres ≈ (246, 269) et (552, 269) pour happy — mesurés sur le
  cœur cyan de l'image (sans le halo), pas estimés à l'œil. Les autres
  expressions reprennent leurs propres centres mesurés (ex. angry :
  232 / 562, cy 285). Voir les fichiers, chaque forme porte ses coordonnées.
- Bouche : centrée sur x=400, y ≈ 350–375
- Couleur cyan : `#8EDCE9` — cœur des yeux mesuré sur l'image (142, 220, 233).
  Le code actuel utilise `#33CCFF`, plus saturé : à toi de choisir, la forme ne change pas.
- Groupes `id="eyes"` / `id="mouth"` dans chaque fichier, pour retrouver
  facilement les formes à animer. Variantes suffixées autorisées :
  `eyes-left`, `eyes-right`, ... (tout `eyes-*` rejoint le calque des yeux,
  tout `mouth-*` celui de la bouche) ; tout autre groupe fait échouer le
  chargement (garde-fou anti-fautes de frappe).

## Fichiers

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

## Utilisation dans TabletApp

Sans nouvelle dépendance : copier l'attribut `d` des `<path>` dans
`SKPath.ParseSvgPathData(...)`, les `<ellipse>` dans `DrawOval(...)`.

Avec fichiers SVG tels quels : package `Svg.Skia`, charger une fois au
démarrage (`new SKSvg().Load(...)`), mettre le `SKPicture` en cache,
dessiner avec `canvas.DrawPicture(...)` à 60 FPS.

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
