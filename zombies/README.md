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
- Rounds with rising zombie health and count. Walkers early, runners and sprinters later.
- Points: 10 per hit, 60 per kill, 100 per headshot kill, 130 per knife kill.
- Wall buys (M14, Olympia, MP40, MP5K, AK74u), the Mystery Box (950, with the teddy bear move), and
  Pack-a-Punch on the stage (5000, after power).
- Perks: Quick Revive (works without power in solo, up to 3 self-revives), Juggernog, Speed Cola,
  Double Tap.
- Power-ups: Max Ammo, Insta-Kill, Double Points, Kaboom (nuke), Carpenter.

## Controls

| Desktop | Touch |
| --- | --- |
| WASD to move, Shift to sprint | Left thumb: floating joystick (push far up to sprint) |
| Mouse to look, left click fire, right click aim | Right thumb: drag to look; drag on Fire to aim while shooting |
| F / E to buy and open; hold to rebuild barriers | Use button appears when something is in reach |
| R reload, V / Q knife, 1 / 2 / wheel to switch | Aim, R, Knife, Swap buttons |
| Esc pause | Pause button, top right |

## Not in yet

Kino's teleporter linking (Pack-a-Punch is simply on the stage once the power is on), dog rounds,
crawlers, the fire traps and co-op.
