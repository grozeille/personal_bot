#language: fr
Fonctionnalité: Session Live
  Discussion temps réel : micro streamé, audio modèle joué, tools exécutés,
  interruptions gérées. (Transport simulé : le vrai réseau est testé en live.)

  Scénario: Session live complète avec émotion
    Soit un serveur live qui salue, joue de l'audio et demande happy
    Quand la session live se déroule
    Alors le setup live déclare le modèle, le prompt et set_emotion
    Et le robot salue, joue l'audio et répond au tool
    Et l'émotion happy s'affiche puis la parole s'arrête
    Et la fin de phrase est signalée une fois

  Scénario: Interruption vide le haut-parleur
    Soit un serveur live qui interrompt
    Quand la session live se déroule
    Alors l'interruption vide le haut-parleur

  Scénario: Le robot sourd pendant sa propre parole
    Soit un serveur live qui parle puis termine son tour
    Quand la session live tourne en semi-duplex
    Alors seul du silence est envoyé pendant la parole du robot
    Et l'écho n'arme pas la fin de phrase
    Et le micro repart après une vraie parole

  Scénario: Connexion live impossible
    Soit un serveur live injoignable
    Quand la session live démarre
    Alors rien n'est envoyé

  Scénario: Le robot joue un son sur demande outil
    Soit un serveur live qui joue un son
    Quand la session live se déroule
    Alors le son giggle est joué et le tool est acquitté

  Scénario: Prout demandé joue un son au lieu de la voix
    Soit un serveur live qui fait un prout puis parle
    Quand la session live se déroule
    Alors le prout est joué et la voix du robot est coupée

  Scénario: Bruit bref sans fin de phrase, silence streamé
    Soit un serveur live silencieux
    Quand le micro capte un bruit bref puis le silence
    Alors aucune fin de phrase n'est envoyée pour un bruit
    Et le silence prolongé est streamé

  Scénario: Le modèle peut terminer la discussion
    Soit un serveur live qui termine la discussion
    Quand la session live se déroule
    Alors la discussion se termine après réponse au tool
