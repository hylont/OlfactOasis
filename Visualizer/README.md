# OlfactOasis Visualizer

Affichage en direct, sur un second écran, des résultats de calibration olfactive
d'OlfactOasis.

Chaque carte de la grille montre **l'agréabilité d'une odeur selon son intensité**
pour un participant :

- la couleur de la courbe est celle de l'odeur (chocolat brun, menthe verte…) et son
  nom est écrit en grand ;
- ● point plein : odeur perçue — ○ point creux : odeur non perçue ;
- ★ étoile + trait pointillé : intensité retenue comme optimale par le système
  (rappelée en haut à droite de la carte) ;
- toutes les cartes partagent la même échelle verticale, elles sont donc comparables.

Les résultats les plus récents sont en premier. Un nouveau résultat s'entoure de la
couleur de son odeur et porte un badge « NOUVEAU » pendant quelques secondes.

## Installation

Python 3.10 ou plus récent, puis :

```bash
pip install -r requirements.txt
```

La synchronisation avec le casque utilise **adb**, déjà installé avec le module
Android de Unity ou avec Meta Quest Developer Hub : l'application le trouve seule.

## Utilisation

```bash
python main.py
```

| Option | Effet |
|---|---|
| `--fullscreen` | démarre en plein écran |
| `--screen 1` | ouvre la fenêtre sur le second écran (0 = écran principal) |
| `--examples` | affiche aussi les fichiers du dossier `Examples` |
| `--folder CHEMIN` | affiche aussi un autre dossier de résultats (répétable) |
| `--demo [N]` | rejoue les exemples comme une session en direct, une odeur toutes les N secondes (4 par défaut) — idéal pour répéter une présentation |
| `--no-headset` | ne cherche pas le casque |
| `--adb CHEMIN` | chemin de `adb.exe`, s'il n'est pas trouvé automatiquement |

Pour une démonstration : `python main.py --screen 1 --fullscreen`

### Raccourcis clavier

| Touche | Action |
|---|---|
| `F11` ou `F` (ou double-clic) | plein écran / fenêtré |
| `Échap` | quitter le plein écran |
| `+` / `-` | plus / moins de cartes par page (6 par défaut) |
| `←` / `→` | résultats plus récents / plus anciens |
| `Début` | revenir aux derniers résultats |

## Récupération automatique depuis le casque

Il suffit de brancher le casque en USB, **mode développeur activé**. Toutes les
2 secondes, l'application :

1. cherche un casque branché (`adb devices`) ;
2. liste les fichiers de résultats du casque
   (`/sdcard/Android/data/com.Lohan.OlfactOasisExperience/files/ScentCalibrationResults/`) ;
3. copie dans `Visualizer/Data/` ceux qui sont nouveaux ou modifiés.

Unity réécrit le fichier d'un participant après chaque cabine : chaque nouvelle odeur
apparaît donc dès que le casque est branché. Les fichiers copiés restent dans `Data/`
(non versionné), les résultats restent donc affichés une fois le casque débranché.

L'état du casque est affiché en haut à droite. Si « autorisez le débogage USB » s'affiche,
mettez le casque et acceptez la demande d'autorisation.

## Organisation du code

```
main.py                    point d'entrée, options de la ligne de commande
olfactoasis_viz/
    config.py              tous les réglages : chemins, couleurs des odeurs, thème, grille
    results.py             lecture des JSON et calcul des courbes ; surveillance des dossiers
    headset.py             recherche d'adb et copie des fichiers depuis le casque (thread)
    result_card.py         une carte : en-tête + graphique (pyqtgraph)
    main_window.py         fenêtre : grille, pagination, plein écran, état du casque
    demo.py                mode démo
```

Le flux est volontairement simple : `HeadsetSync` copie les fichiers dans `Data/`,
`ResultsLibrary` remarque les fichiers nouveaux ou modifiés et prévient `MainWindow`, qui
reconstruit les cartes de la page affichée.

### Calcul de l'agréabilité

Pour chaque intensité testée (`Parameters.Strength`) :

- odeur non perçue, ou réponse neutre → 0 ;
- agréable / désagréable → + / − la longueur du tracé « à quel point » dessiné par le
  participant (`ResponseMagnitude`), ou ± 1 s'il n'a rien tracé.

L'intensité optimale est celle calculée par Unity (`OptimalParameters.Strength`) ;
0 signifie qu'aucune intensité n'a été jugée agréable.

### Ajouter une odeur

Ajouter une ligne au dictionnaire `SCENTS` de `config.py` : nom dans le JSON (`EScentName`
en majuscules, ex. `DARK_CHOCOLATE`), nom affiché et couleur.
