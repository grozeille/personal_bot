#language: fr
Fonctionnalité: Mise à jour depuis le NAS
  Quand une maj est disponible sur le NAS, une icône discrète apparaît
  en haut à droite ; un tap dessus affiche le visage de confirmation
  (œil X = annuler, œil ✓ = confirmer), au regard figé.

  Scénario: Tap sur l'icône affiche la confirmation
    Soit une source de maj proposant la version "1.0.1"
    Quand le robot vérifie les mises à jour
    Alors une mise à jour est proposée
    Et l'icône de mise à jour est visible
    Quand on appuie sur l'icône de mise à jour
    Alors le visage de confirmation est affiché
    Et le regard est figé

  Scénario: Œil gauche annule
    Soit une source de maj proposant la version "1.0.1"
    Quand le robot vérifie les mises à jour
    Et on appuie sur l'icône de mise à jour
    Et on appuie sur l'œil gauche
    Alors la confirmation est refermée
    Et le visage précédent est restauré
    Et la version courante est conservée

  Scénario: Œil droit confirme et installe
    Soit une source de maj proposant la version "1.0.1"
    Quand le robot vérifie les mises à jour
    Et on appuie sur l'icône de mise à jour
    Et on appuie sur l'œil droit
    Alors la version "1.0.1" est installée

  Scénario: Tap hors des yeux ignoré
    Soit une source de maj proposant la version "1.0.1"
    Quand le robot vérifie les mises à jour
    Et on appuie sur l'icône de mise à jour
    Et on appuie loin des yeux
    Alors la confirmation est toujours affichée

  Scénario: NAS hors-ligne silencieux
    Soit une source de maj hors-ligne
    Quand le robot vérifie les mises à jour
    Alors aucune mise à jour n'est proposée
    Et l'icône de mise à jour est masquée
    Quand on appuie sur l'icône de mise à jour
    Alors aucune confirmation n'est affichée

  Scénario: Paquet corrompu refusé
    Soit une source de maj corrompue proposant la version "9.9.9"
    Quand le robot vérifie les mises à jour
    Alors une mise à jour est proposée
    Quand on appuie sur l'icône de mise à jour
    Et on appuie sur l'œil droit
    Alors la version courante est conservée

  Scénario: Points animés pendant l'installation
    Soit une source de maj proposant la version "1.0.1"
    Quand le robot vérifie les mises à jour
    Alors des points d'attente remplacent le visage
    Et les points d'attente sont animés
