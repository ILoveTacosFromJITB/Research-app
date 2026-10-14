# Zombies: Kino der Toten (fan-made)

A round-based zombie survival game in the style of Black Ops 1 Zombies. It runs in the browser and is
built from scratch with Three.js and Web Audio. There are no downloaded models, textures or sounds;
everything is generated in code. It's a fan tribute and is not affiliated with Activision or Treyarch.

Open `index.html` in a browser (phone or desktop). It needs an internet connection the first time to load
Three.js from cdnjs.

## What's in it

- Loading screen, then a main menu (Solo, Options, Controls, About) over a fly-through of the map.
- **Kino der Toten**, laid out after the original: the lobby where you spawn, the dressing room and the
  alley on either side, and the theater and stage in the middle. Doors cost 750 or 1000 points. The
  theater's main doors open when you turn on the power backstage.
- Barricaded windows that zombies tear down. Hold Use inside to rebuild them for points.
- Rounds follow the original rules: zombie health (+100 a round, then +10% from round 10), the solo
  zombie count, a spawn delay that shortens 5% each round, and walkers turning into runners and
  sprinters as the rounds climb.
- Points: 10 per hit; a kill pays 50 plus 10 for the body or 50 for the head; knife kills pay 130.
  Rebuilding barriers pays up to 50 × the round number (max 500) per round.
- Hellhound rounds every 4 to 5 rounds, starting on round 5, 6 or 7. The last hound drops a Max Ammo.
- Wall buys (M14, Olympia, MP40, MP5K, AK74u) and the Mystery Box (950). The teddy bear can't show up
  in the first 4 uses; after that the odds rise the more the box is used.
- Kino's teleporter: with the power on, activate the mainframe on the stage and link the pad in the
  lobby. It sends you to the projector room, where Pack-a-Punch is (5000), for 30 seconds. Then it
  has to be linked again.
- Perks: Quick Revive (works without power in solo, up to 3 self-revives), Juggernog, Speed Cola,
  Double Tap.
- Power-ups: Max Ammo, Insta-Kill, Double Points, Kaboom (nuke), Carpenter. They drop on a 3% chance
  per kill, plus a guaranteed drop each time your earned points pass a rising mark (max 4 a round).

## Controls

| Desktop | Touch |
| --- | --- |
| WASD to move, Shift to sprint | Left thumb: floating joystick (push far up to sprint) |
| Mouse to look, left click fire, right click aim | Right thumb: drag to look; drag on Fire to aim while shooting |
| F / E to buy and open; hold to rebuild barriers | Use button appears when something is in reach |
| R reload, V / Q knife, 1 / 2 / wheel to switch | Aim, R, Knife, Swap buttons |
| Esc pause | Pause button, top right |

## Not in yet

Nova crawlers, the fire traps, the balcony and dining areas, and co-op.

The round rules above were worked out by reading Black Ops 1's game scripts for how the mechanics
behave. All code here is written from scratch; no game code or assets are included.
