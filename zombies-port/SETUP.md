# Zombies mobile prototype: setup and testing

A Unity rebuild of Black Ops 1 Zombies for phones. This folder holds the scripts. These steps turn
them into a playable test level and put it on a phone. Allow about an hour, mostly for installing Unity.

You need a Windows PC or a Mac. An iPhone build needs a Mac.

## 1. Install Unity (one time)

1. Install **Unity Hub**: <https://unity.com/download>
2. In Unity Hub, go to **Installs**, then **Install Editor**, and choose the latest **Unity 6 LTS**.
3. On the modules page, tick:
   - **Android Build Support**, including the OpenJDK and Android SDK & NDK sub-items
   - **iOS Build Support**, only on a Mac, only for iPhones
4. Sign in with a free Unity account. The **Personal** license is enough.

## 2. Create the project

1. In Unity Hub, click **Projects**, then **New project**, and choose the **Universal 3D** (URP) template. Name it anything.
2. Get this folder: on GitHub, open the repository, switch to the branch
   **`ccr-1ae00a4c-wtnkn0`**, and click **Code**, then **Download ZIP**. Unzip it.
3. Copy the whole **`zombies-port`** folder into the project's **`Assets`** folder.
   Keep `zombies-port/Unity/Editor` named exactly `Editor`.
4. In Unity:
   - **Window → Package Manager → Unity Registry**: install **AI Navigation**.
   - If asked to **Import TMP Essentials**, click Import.
5. Check that the **Console** (Window → General → Console) has no red errors.
   **If it does, copy them and send them back. The code hasn't been opened in Unity before, so a few fixes are expected.**

## 3. Build the test level (one click)

Menu **Zombies → Build Test Level**, then **Build**.

This creates and saves `Assets/Scenes/ZombiesTestLevel.unity`:

| Where | What |
|---|---|
| The player's spawn | the centre of a 60 × 60 m floor |
| Behind the player | a boarded window, with zombies spawning outside it |
| Left | three wall guns (M14, Olympia, MP40) |
| Right | Quick Revive, Juggernog, the power switch, and the Mystery Box |
| Ahead | a 1000-point door into a second room, which has a second Mystery Box spot |
| Overall | round system, health and last stand, perks, power-ups, and hellhound rounds (round 5, 6 or 7, then every 4–5 rounds) |

It also bakes the NavMesh (zombie pathfinding) and locks the app to landscape.

## 4. Play it on the computer

Press **Play**. The player starts with 500 points, and round 1 begins after 2 seconds.

| Action | Key |
|---|---|
| Move / sprint | WASD / Shift |
| Look around | **Tab** turns mouse-look on (Esc frees the mouse) |
| Shoot / aim | Left / right mouse (while mouse-look is on) |
| Reload / knife / swap gun | R / V / Q |
| Interact (buy, power, door) | F |
| Rebuild window / revive | **hold** F |

With mouse-look off, the mouse can click the on-screen touch buttons like a finger.
For a phone-shaped preview, switch the Game view's dropdown from **Game** to **Simulator**.

Useful test shortcuts (Play mode; right-click a component's header in the Inspector):

| On this object | Use this |
|---|---|
| **Round System** → RoundDirector | **Debug: Kill All Zombies** (skip a round) |
| **Round System** → PowerUpDirector | **Debug: Drop Nuke / Max Ammo / ...** |
| **Round System** → DogRoundDirector | **Debug: Next Round Is a Dog Round**, then kill all zombies to get there |

## 5. Put it on the phone

### Android

1. On the phone, turn on **Developer options** (Settings → About phone → tap **Build number** 7 times),
   then turn on **USB debugging**.
2. Connect the phone by USB and accept the "Allow USB debugging" prompt.
3. In Unity, open **File → Build Profiles**, select **Android**, and click **Switch Platform** (this takes a few minutes the first time).
4. Click **Build And Run**. It installs and launches on the phone.

To send the app instead: click **Build** to get an `.apk` file and send it. The phone needs
"Install unknown apps" allowed for whatever app opens the file.

### iPhone (Mac only)

1. Install **Xcode** from the Mac App Store.
2. In Unity, open **File → Build Profiles**, select **iOS**, click **Switch Platform**, then **Build**, and pick a folder.
3. Open the generated `.xcodeproj` in Xcode and choose your Apple ID under **Signing & Capabilities**. A free Apple ID works; the app then expires after 7 days.
4. Connect the iPhone and press **Run**. On the iPhone, trust the developer under
   Settings → General → VPN & Device Management.

### Quick touch test without building

Install **Unity Remote 5** (free on Android and iOS). Connect the phone by USB, pick it under
**Edit → Project Settings → Editor → Unity Remote → Device**, and press Play. The game runs on
the computer and the phone shows it and sends touches back.

## Touch controls

| Control | Where |
|---|---|
| Move | left thumb, anywhere in the lower-left (the stick appears where you touch) |
| Look / aim | drag anywhere on the right side |
| FIRE | biggest button, bottom-right; dragging it also aims |
| AIM, R (reload), KNIFE, SWAP | around FIRE |
| Interact | centre-right; appears only near something usable; hold it to rebuild or revive |

## What to send back

- Any red **Console** errors (copy the text).
- Anything that feels wrong: controls too sensitive, buttons in awkward spots, zombies stuck, prices, timing.
- A screenshot or short screen recording from the phone helps a lot.
