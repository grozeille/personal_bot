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

  Scénario: Le modèle peut demander une calibration
    Soit un serveur live qui demande une calibration
    Quand la session live se déroule
    Alors la calibration est demandée puis la session se termine

  Scénario: Le modèle tourne la tête en direct
    Soit un serveur live qui tourne la tête
    Quand la session live se déroule
    Alors la tête tourne et la session continue

  Scénario: Appel d'outil dupliqué ignoré
    Soit un serveur live qui répète un appel d'outil
    Quand la session live se déroule
    Alors l'appel dupliqué n'agit qu'une fois

  Scénario: Souffle coupé, voix faible transmise
    Soit un serveur live silencieux
    Quand le micro capte du souffle puis une voix faible
    Alors le souffle part en zéros et la voix faible intacte

  Scénario: Claquement fort ignoré
    Soit un serveur live silencieux
    Quand le micro capte des claquements forts
    Alors les bangs forts ne déclenchent pas de tour

  Scénario: Tap pendant la réponse revient à l'écoute
    Soit un serveur live qui parle puis termine son tour
    Quand la réponse est coupée par un tap
    Alors la fin de réponse est ignorée et l'écoute reprend

  Scénario: Long monologue découpé en tours
    Soit un serveur live silencieux
    Quand l'utilisateur parle sans s'arrêter
    Alors une fin de phrase est forcée malgré la parole continue

  Scénario: Compteur de tour remis à zéro après la réponse
    Soit un serveur live silencieux
    Quand deux tours utilisateur encadrent une réponse
    Alors le compteur de tour repart de zéro après la réponse

  Scénario: Micro fermé tant que le haut-parleur joue
    Soit un serveur live silencieux
    Quand le haut-parleur garde du retard puis se vide
    Alors le micro reste sourd puis l'écoute reprend

  Scénario: Réponse bloquée rouverte par le watchdog
    Soit un serveur live qui parle sans finir son tour
    Quand le micro tourne pendant le blocage
    Alors le micro est rouvert et ça se voit dans le journal

  Scénario: Envois en échec répétés demandent le repli
    Soit un serveur live silencieux
    Quand les envois échouent
    Alors la session s'arrête en demandant le repli unaire
