#language: fr
Fonctionnalité: Debug audio des discussions
  Chaque tour peut être enregistré (micro entendu + réplique robot +
  sidecar) pour diagnostiquer les problèmes d'écoute. Tampon tournant
  local, purge par âge puis par taille.

  Scénario: Tours enregistrés en WAV lisibles
    Soit un dossier debug temporaire
    Quand on enregistre deux tours debug
    Alors six fichiers de tours existent
    Et le WAV micro se recharge avec les bons échantillons
    Et le sidecar contient les transcriptions

  Scénario: Purge des vieux tours debug
    Soit un dossier debug avec des tours anciens et récents
    Quand un enregistreur à purge stricte démarre
    Alors les vieux tours sont purgés
    Et les tours récents sont conservés
