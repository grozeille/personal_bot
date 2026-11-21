# PiServoServer (stub, #28)

Serveur cou : parle le contrat TCP `../Simulator3D/PROTOCOL.md`, affiche les
ordres reçus, répond comme le simu. Pas encore de PCA9685 : validation du
tuyau tablette -> Pi uniquement.

## Vérifier le protocole (Windows)

```powershell
go build -o pistub.exe .
.\pistub.exe -port 9080
```

## Compiler pour le Pi 1 (ARMv6)

```powershell
$env:GOOS='linux'; $env:GOARCH='arm'; $env:GOARM='6'; go build -o piservo .
```

## Déployer et lancer

```sh
scp piservo mathias@raspberrypi.local:~
ssh mathias@raspberrypi.local ./piservo
```

Puis côté tablette : `HEAD_SIM_HOST` vers l'IP du Pi + `--head-test`.
