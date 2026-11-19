#language: fr
Fonctionnalité: Quitter sans clavier
  Un appui long dans le coin haut-gauche quitte l'application
  (pas de clavier sur la tablette, et l'enfant tapote partout).

  Scénario: Appui long dans le coin quitte
    Soit un geste de sortie
    Quand on appuie dans le coin haut-gauche
    Et 3 secondes s'écoulent en maintenant l'appui
    Alors la fermeture est demandée

  Scénario: Appui court ne quitte pas
    Soit un geste de sortie
    Quand on appuie dans le coin haut-gauche
    Et 1 seconde s'écoule en maintenant l'appui
    Et on relâche l'appui
    Alors la fermeture n'est pas demandée

  Scénario: Appui hors du coin ne quitte pas
    Soit un geste de sortie
    Quand on appuie au milieu de l'écran
    Et 5 secondes s'écoulent en maintenant l'appui
    Et on relâche l'appui
    Alors la fermeture n'est pas demandée

  Scénario: Un appui hors zone annule l'appui précédent
    Soit un geste de sortie
    Quand on appuie dans le coin haut-gauche
    Et on appuie au milieu de l'écran
    Et 5 secondes s'écoulent en maintenant l'appui
    Et on relâche l'appui
    Alors la fermeture n'est pas demandée
