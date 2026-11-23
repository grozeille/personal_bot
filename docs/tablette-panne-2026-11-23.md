# Panne tablette 2026-11-22/23 — mises à jour Intel + tactile/boutons morts

## Timeline
- 22 nov au soir : Windows Update propose KB2267602 (Defender, pending install) + KB890830 (MSRT v5.147, installing) + ~11 pilotes Intel (GPIO, Serial IO I2C, DPTF, UART, caméras, SST).
- Vérifié en SSH : KB toujours `IsInstalled=0`, Defender déjà à 1.463.426.0 (= version proposée, affichage cosmétique). Pilotes Intel installés avec succès (historique WU).
- `RebootRequired=True` → reboot manuel user 23h59. Reboot OK, `explorer`+`winlogon` vivants.
- Échec logon 00h02 (4625, probablement faute de frappe tactile), puis **logon réussi 00h03**. Compte mathi sain (actif, admin, mdp n'expire jamais).
- Symptômes user : écran horloge verical (normal : Venue 8 Pro = portrait natif 1200x1920), tactile mort, boutons latéraux morts, puis écran noir.
- Mon reboot SSH (`Restart-Computer -Force`) : effectif, boot 00h05:04. Mêmes symptômes après.
- ~00h3x : SSH ne répond plus (TCP 22/445 ouvert mais banner SSH timeout = userspace coincé, noyau vivant).

## Pistes
- `Get-PnpDevice` distant : statut `Unknown` pour ~37 périphériques, y compris des fonctionnels (HD Graphics affichait l'horloge !) → statut distant non fiable, ne pas conclure.
- À vérifier au retour : `ConfigManagerErrorCode` numérique (WMI) du tactile (SYNA7500), GPIO Buttons, I2C HID ; erreurs DriverFrameworks-UserMode au boot ; rollback pilotes Serial IO I2C / GPIO via `pnputil /delete-driver <oem> /uninstall` + `/scan-devices` (pas de point de restauration système — `Get-ComputerRestorePoint` vide).
- Si coincé au réveil : appui long power 10-15 s (coupure hardware, indépendante de Windows), rallumer, me dire quoi.

## Moniteur
- `tablet-watch.log` (scratch) : tentative SSH toutes les 3 min (~5 h). Si SSH revient, reprendre diagnostic + rollback si besoin.
- Le tactile ne peut pas être validé à distance : test final par user le matin.
