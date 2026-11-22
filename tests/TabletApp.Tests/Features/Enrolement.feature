#language: fr
Fonctionnalité: Enrôlement du wake word
  Au premier lancement, le robot fait répéter "Hey, Patate !" et choisit
  la variante + les réglages qui reconnaissent la voix, sans recompiler.
  La calibration est stockée en local (elle survit aux mises à jour).

  Scénario: Choix du premier candidat franc
    Soit les scores suivants :
      | Ligne               | Seuil | Porte | Prises | Silence |
      | B AH1 T @A          | 0.25  | 150   | 110    | 0       |
      | B AH1 T DH AE1 T @B | 0.25  | 150   | 111    | 0       |
      | T DH AE1 T @C       | 0.25  | 150   | 111    | 1       |
    Quand on choisit le gagnant
    Alors le gagnant est "B AH1 T DH AE1 T @B"
    Et le seuil est "0.25" et la porte "150"

  Scénario: Échec sans prise
    Quand on calibre sans aucune prise
    Alors la calibration échoue
    Et aucun fichier n'est écrit

  Scénario: Calibration locale prioritaire
    Quand on écrit une calibration avec le seuil "0.15"
    Alors le seuil chargé est "0.15"
    Et sans calibration le seuil est "0.25"

  Scénario: Le wizard suit les phases
    Quand le wizard tourne avec des prises factices
    Alors les phases sont "Ambient, Takes, Thinking, DoneKo"
    Et la calibration échoue

  Scénario: Découpe au dernier épisode
    Quand on découpe une prise avec un clic initial
    Alors la prise découpée commence 0,7 s avant la parole

  Scénario: Prises franches reconnues
    Quand on vérifie des transcriptions :
      | Texte               | Propre |
      | Hey, patate.        | 1      |
      | patate              | 1      |
      | Hein ? Hé, patate.  | 0      |
      | Patate. Hé, patate. | 0      |
      | Hey potato          | 0      |
    Alors les verdicts sont corrects

  Scénario: Consignes pré-synthétisées résolues
    Quand on résout les consignes
    Alors chaque consigne a un asset et l'inconnue n'en a pas
