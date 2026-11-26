# Atlas Uno Express

`atlas.png` (1024×1400, 51 faces de 128×200) + `atlas.json` (nom →
`[x, y, l, h]`) : les vraies faces du jeu, affichées sur la table
(@Mattel, usage personnel/familial uniquement — même régime que les
samples prout, vérifier la licence avant toute redistribution).

Noms : `n1..n9_<couleur>`, `skip_<couleur>`, `reverse_<couleur>`,
`plus1_<couleur>`, `wild`, `wildplus2`, `back`. Couleurs :
red, yellow, green, blue (le 0 n'existe pas en Uno Express).

Régénération (après retouche de `docs/uno-source/`) :

```powershell
dotnet run --project src/TabletApp -- --build-atlas
# contrôler %TEMP%\uno-atlas\contact.png, puis copier atlas.png + atlas.json ici
```
