# Plinko Palace — édition incrémentale

Jeu incrémental (idle) de Plinko en Godot 4.7 / C#, inspiré de Cookie Clicker, Antimatter Dimensions et des « idle Plinko ». Tout est dessiné et synthétisé en code : aucun asset externe.

> La version roguelike d'origine se trouve sur la branche `main`.

## Jouer

- **Souris** : viser (le lanceur suit le curseur). **Clic** ou **Espace** pour lâcher une bille, **clic maintenu** pour une rafale.
- `←/→` (ou `Q/D`) pour viser, `Échap` pour la pause, `M` pour couper le son.
- La partie se **sauvegarde toute seule**, toutes les 15 secondes et à la fermeture.

## Boucle de jeu

- **Pièces.** Chaque bille qui tombe dans une case rapporte *valeur de la bille × multiplicateur de la case × bonus*. Les cases sont symétriques : faibles au centre, énormes sur les bords. Viser compte.
- **Billes** (onglet *Billes*) : 6 types — Bille, Argent, Or, Diamant, Rubis, Cosmique. Chaque type vaut ~12× le précédent. Le prix augmente de 15 % à chaque achat, et on peut acheter par x1 / x10 / x100 / MAX. Chaque bille possédée retombe en boucle après une **recharge**. Au-delà de 24 billes d'un même type, chaque bille à l'écran en représente plusieurs, pour préserver les performances.
- **Améliorations** (onglet *Améliorations*) :
  - distributeur automatique ;
  - recharge rapide ;
  - polissage (x1,25 par niveau) ;
  - +1 rangée (bords plus rentables) ;
  - clous dorés (les clous rapportent) ;
  - coup critique (x10) ;
  - cases renforcées (x1,3 par niveau) ;
  - portail dédoubleur, que l'on place soi-même et qui dédouble les billes.
- **Coffre en or** : il apparaît de temps en temps sur le plateau. Une bille qui le touche déclenche une **frénésie** (gains x7 pendant 30 s) ou un **gros lot** de pièces.
- **Gains hors-ligne** : une partie de tes revenus continue pendant ton absence (25 % sur 4 h au départ, améliorable).

## Prestige : les chaussures

- L'onglet *Chaussures* permet de **recommencer à zéro** (pièces, billes, améliorations) contre des **jetons**. Le nombre de jetons suit la racine cubique des gains de la partie, multipliée par le bonus des chaussures.
- Les **chaussures sont des niveaux de difficulté** débloqués petit à petit. Chaque paire se débloque en gagnant assez dans une seule partie avec la paire précédente :

| Chaussures | Contraintes | Jetons | Déblocage |
|---|---|---|---|
| Mocassins | aucune | x1 | départ |
| Baskets fétiches | prix x2 | x3 | 10M en une partie |
| Talons dorés | prix x2, cases x0,6 | x8 | 1B |
| Santiags | prix x3, cases x0,6, recharge x2 | x20 | 100B |
| Tongs de plage | prix x4, cases x0,5, recharge x2,5 | x60 | 10T |

- Chaque jeton gagné donne **+1 % de revenus pour toujours**, et chaque paire débloquée **+50 %**.

## Arbre de compétences

L'arbre se paie en jetons et ses bonus sont permanents. Il a 3 branches de 4 nœuds :

- **Fortune** : revenus, critiques, clous, puis *Bords dorés* (cases extrêmes x3).
- **Automatisation** : distributeur offert, **Majordome** (achat automatique des billes), **Intendant** (achat automatique des améliorations), gains hors-ligne.
- **Économie** : prix réduits, capital de départ, coffres en or, puis *Portail permanent*.

## Organisation du code

| Fichier / dossier | Contenu |
|---|---|
| `scripts/Autoload/IdleManager.cs` | Toute l'économie : coûts, gains, prestige, compétences, frénésie, achat auto, hors-ligne, sauvegarde |
| `scripts/Data/IdleDefs.cs` | Types de billes, améliorations, arbre de compétences |
| `scripts/Data/CharacterDef.cs` | Chaussures = difficultés |
| `scripts/Board/IdleBoard.cs` | Plateau : réserve de billes, recharge, distributeur, portails, coffre en or |
| `scripts/Idle/` | Écrans (titre, jeu), boutique, HUD de la borne, arbre de compétences |
| `scripts/Visuals/`, `scripts/UI/Style.cs` | Décor, borne, chaussures, effets, style commun |
| `scripts/Debug/AutoPilot.cs` | Bot de test et d'équilibrage |

## Tests automatiques (AutoPilot)

Le bot joue tout seul avec une stratégie gloutonne : il achète, place les portails, fait ses prestiges et dépense ses jetons, en journalisant l'économie. Il n'écrit jamais dans la sauvegarde.

```bash
# 60 minutes de jeu simulées sans fenêtre
Godot_v4.7.2-stable_mono_win64_console.exe --headless --fixed-fps 60 --path . -- --autopilot --minutes=60

# En fenêtre, avec captures d'écran et défilement des onglets de la boutique
Godot_v4.7.2-stable_mono_win64_console.exe --audio-driver Dummy --path . -- --autopilot --shots=captures --tabs
```
