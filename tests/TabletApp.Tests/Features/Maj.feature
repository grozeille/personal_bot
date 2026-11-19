#language: fr
Fonctionnalité: Mise à jour depuis le NAS
  Quand une maj est disponible sur le NAS, une icône discrète apparaît
  en haut à droite ; un appui maintenu de 3 secondes lance la maj
  (l'enfant ne peut pas la déclencher par accident).

  Scénario: Maj détectée puis installée par appui long
    Soit une source de maj proposant la version "1.0.1"
    Quand le robot vérifie les mises à jour
    Alors une mise à jour est proposée
    Et l'icône de mise à jour est visible
    Quand on appuie sur l'icône de mise à jour
    Et 3 secondes s'écoulent en maintenant l'appui sur l'icône
    Alors la mise à jour est demandée
    Quand on lance l'installation de la mise à jour
    Alors la version "1.0.1" est installée

  Scénario: Appui court sans effet
    Soit une source de maj proposant la version "1.0.1"
    Quand le robot vérifie les mises à jour
    Et on appuie sur l'icône de mise à jour
    Et 1 seconde s'écoule en maintenant l'appui sur l'icône
    Et on relâche l'icône
    Alors la mise à jour n'est pas demandée

  Scénario: Appui loin de l'icône sans effet
    Soit une source de maj proposant la version "1.0.1"
    Quand le robot vérifie les mises à jour
    Et on appuie loin de l'icône
    Et 5 secondes s'écoulent en maintenant l'appui sur l'icône
    Et on relâche l'icône
    Alors la mise à jour n'est pas demandée

  Scénario: NAS hors-ligne silencieux
    Soit une source de maj hors-ligne
    Quand le robot vérifie les mises à jour
    Alors aucune mise à jour n'est proposée
    Et l'icône de mise à jour est masquée

  Scénario: Paquet corrompu refusé
    Soit une source de maj corrompue proposant la version "9.9.9"
    Quand le robot vérifie les mises à jour
    Alors une mise à jour est proposée
    Quand on lance l'installation de la mise à jour
    Alors la version courante est conservée
