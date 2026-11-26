#language: fr
Fonctionnalité: Pause et reprise des discussions
  "Attends" suspend sans perdre le contexte ; reprise sous 10 min =
  suite ("Oui ?"), au-delà = nouvelle discussion (bonjour).

  Scénario: Pause puis reprise rapide garde le contexte
    Soit une discussion en pause depuis 2 minutes
    Quand on redémarre une discussion
    Alors c'est une reprise

  Scénario: Pause ancienne = nouvelle discussion
    Soit une discussion en pause depuis 15 minutes
    Quand on redémarre une discussion
    Alors c'est une discussion fraîche

  Scénario: Sans pause = nouvelle discussion
    Soit aucune pause
    Quand on redémarre une discussion
    Alors c'est une discussion fraîche

  Scénario: Reprise sans re-bonjour
    Quand on lit la consigne de reprise
    Alors elle dit Oui sans bonjour

  Scénario: L'outil pause termine la session live
    Soit un serveur live qui demande une pause
    Quand la session live tourne jusqu'à la pause
    Alors la session est marquée en pause
