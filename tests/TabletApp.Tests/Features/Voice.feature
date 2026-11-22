#language: fr
Fonctionnalité: Test voix
  Le robot dit bonjour (TTS Gemini), écoute, et affiche le visage cœur
  si l'enfant répond « bonjour ». (Audio simulé : le vrai matériel est
  testé en live avec --voice-test.)

  Scénario: Bonjour entendu affiche le cœur
    Soit un test voix dont l'écoute entend "Bonjour !"
    Quand le robot dit bonjour et écoute
    Alors le robot a dit "Bonjour, je m'appelle Patate !"
    Et le visage cœur a été affiché puis le visage neutre restauré

  Scénario: Salut entendu n'affiche pas le cœur
    Soit un test voix dont l'écoute entend "salut"
    Quand le robot dit bonjour et écoute
    Alors le visage cœur n'a pas été affiché

  Scénario: Discussion avec émotions puis adieux sur silences
    Soit une discussion avec :
      | entendu            | réplique | émotion | son    |
      | Bonjour            | Salut !  | happy   |        |
      | Raconte une blague | Toto !   | laugh   | giggle |
      |                    |          |         |        |
      |                    |          |         |        |
    Quand la discussion se déroule
    Alors le robot salue, répond et dit au revoir
    Et les visages suivent le script
    Et les sons suivent le script

  Scénario: Émotion inconnue ignorée en discussion
    Soit une discussion avec :
      | entendu | réplique | émotion  |
      | coucou  | Salut !  | farfelue |
      |         |          |          |
      |         |          |          |
    Quand la discussion se déroule
    Alors le robot salue, répond et dit au revoir
    Et les visages suivent le script

  Scénario: Tap pendant la réflexion revient à l'écoute
    Quand un tour est annulé pendant la réflexion
    Alors la réplique annulée n'est pas dite et la discussion continue

  Scénario: Tap routé selon l'état visible
    Quand on route des taps :
      | halo      | parle | action |
      | Thinking  | non   | écoute |
      | Listening | oui   | écoute |
      | None      | oui   | écoute |
      | Listening | non   | fin    |
      | None      | non   | fin    |
    Alors les actions sont correctes
