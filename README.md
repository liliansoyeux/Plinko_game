# Plinko Palace

Roguelike de Plinko en Godot 4.7 / C#. Tout est dessiné et synthétisé en code : aucun asset externe (ni image, ni son).

## Jouer

- **Souris** : viser (le lanceur suit le curseur). **Clic** pour lâcher une bille, **clic maintenu** pour une rafale.
- **Clavier** : `←/→` (ou `Q/D`) pour viser, `Espace` pour lâcher, `Échap` pour la pause, `F` pour la vitesse x2, `M` pour couper le son.
- Cartes d'amélioration : clic, ou touches `1` / `2` / `3`.

## Règles

- Chaque **palier** donne un objectif de score et un nombre de billes. Si l'objectif n'est pas atteint quand toutes les billes sont tombées, la partie est perdue.
- Un **boss** arrive tous les 5 paliers, avec deux handicaps : bâtons en plus, bâtons tournants, cases rétrécies…
- Les bonnes cases remplissent la jauge d'**XP**. Chaque niveau fait apparaître un **coffre** (commun, rare ou épique) sur le plateau : touche-le avec une bille pour choisir une amélioration parmi trois.
- Les mauvaises cases (x0.x) et les billes ratées remplissent la jauge de **malus**. Quand elle est pleine, un **coffre maudit** apparaît et impose un malus, dont tu choisis le moindre.
- Le bonus **« Bâton à placer »** met le jeu en pause : tu choisis où poser le bâton (clic), et tu le fais tourner avec la molette ou le clic droit. Il garde sa place pour le reste de la partie. Le **bâton sauvage** (malus), lui, tombe au hasard.
- **Portail dédoubleur** (rareté **légendaire**, environ 2 % des coffres) : tu le places, et chaque bille qui le traverse se dédouble, une fois par portail.
- Les **cocktails** (8 recettes) sont des bonus passifs permanents, posés sur la machine. On les obtient avec l'amélioration épique « Cocktail surprise » ou avec les tongs.
- Les **chaussures** sont le personnage, chacune avec son avantage de départ : Mocassins, Baskets fétiches, Talons dorés, Santiags, Tongs de plage.
- Les **valeurs des cases** sont tirées au hasard à chaque partie (petits gains, gains moyens, jackpots, mauvaises cases de x0.1 à x0.8), avec toujours **autant de cases positives que négatives**. La disposition est ensuite validée par un calcul d'espérance (loi binomiale) : jamais injouable, jamais triviale.

## Progression entre les parties

- Chaque fin de partie, et même un abandon, rapporte des **jetons** : 3 par palier réussi, 6 par boss battu, plus un bonus selon le niveau atteint.
- L'**arbre de compétences** (bouton sur l'écran titre, ou touche `C`) a 3 branches de 4 nœuds. Chaque nœud demande au moins un niveau dans le nœud au-dessus :
  - **Fortune** : gains, billes dorées, jackpots → *Portail d'ouverture* (chaque partie commence avec un portail).
  - **Chance** : XP, coffres, relances de cartes (`R`) → *Quatrième carte* dans chaque coffre.
  - **Sécurité** : billes, bouclier, jauge de malus → *Seconde chance* (+3 billes au lieu du game over si l'objectif est atteint à 60 % ou plus).
- Le bouton « Réinitialiser » rembourse tous les jetons dépensés.

## Organisation du code

| Dossier | Contenu |
|---|---|
| `scripts/Main.cs` | Racine : environnement (bloom HDR), fondus, bascule écran titre ↔ partie |
| `scripts/TitleScreen.cs` | Écran titre et choix des chaussures |
| `scripts/GameScreen.cs` | Une partie : assemble la scène, relie les événements, gère les entrées et la pause |
| `scripts/Autoload/` | `RunManager` (règles), `Sfx` (sons et musique synthétisés), `SaveData` (records) |
| `scripts/Board/` | Plateau (génération adaptative, lanceur), coffres, bâtons, disposition des cases |
| `scripts/Data/` | Statistiques, améliorations, malus, personnages, paliers et boss |
| `scripts/Modifiers/` | Les cocktails |
| `scripts/UI/` | HUD, cartes d'amélioration, bannières, pause, fin de partie, style commun |
| `scripts/Visuals/` | Décor, borne, jambes et chaussures, effets (particules, texte flottant, tremblement) |
| `scripts/Debug/AutoPilot.cs` | Bot de test (voir ci-dessous) |

## Tests automatiques (AutoPilot)

Le jeu peut se jouer tout seul pour les tests d'intégration et l'équilibrage. Les options disponibles sont documentées en tête de `scripts/Debug/AutoPilot.cs`. Le bot n'écrit jamais dans les records.

```bash
# 30 parties simulées sans fenêtre, en accéléré, avec un résumé par partie
Godot_v4.7.2-stable_mono_win64_console.exe --headless --fixed-fps 60 --path . -- --autopilot --runs=30

# Une partie en fenêtre, pilotée par de vrais clics souris, avec captures d'écran
Godot_v4.7.2-stable_mono_win64_console.exe --audio-driver Dummy --path . -- --autopilot --mouse --shots=captures --shot-every=3
```
