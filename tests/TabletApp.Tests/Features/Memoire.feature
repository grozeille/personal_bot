#language: fr
Fonctionnalité: Mémoire des discussions
  Le robot garde un journal local et un carnet consolidé, rappelés
  à chaque session pour se souvenir de l'enfant.

  Scénario: Le journal garde les tours et s'élague
    Soit un dossier mémoire avec un vieux journal
    Quand on y consigne deux tours
    Alors le journal contient les tours
    Et le vieux journal est élagué

  Scénario: Le prompt contient la mémoire
    Soit un carnet avec des souvenirs
    Quand on construit le prompt avec la mémoire
    Alors le prompt contient les souvenirs

  Scénario: La consolidation réécrit le carnet
    Soit un carnet avec un journal
    Quand on consolide avec un résumé factice
    Alors le carnet contient le résumé
