# Save Database

This repository is a community save database managed with **SaveHub**. It stores
emulator **memory cards** and **save states** for many platforms so they can be
shared and re-downloaded.

## Folder structure

```
<PLATFORM>/
  README.md           # games index for this platform (title id + game name)
  <GAME-ID>/
    README.md         # saves index: each archive with its type and description
    01.zip            # a memory card (one card per zip)
    02.zip            # the next memory card
    01-sstate.zip     # a save state (may contain multiple files)
    icon.jpg          # cover art
```

- `<PLATFORM>` is the console/handheld folder, e.g. `PS1`, `PS2`, `PS3`, `GBA`,
  `DS`, `3DS`, `SNES`, ...
- `<GAME-ID>` is the game serial / title id, e.g. `SLUS-21274`.
- Memory-card archives are named `NN.zip`; save-state archives are named
  `NN-sstate.zip`, where `NN` is an incrementing, zero-padded number.
- Each archive contains a `README.txt` describing what the save is for (e.g.
  "100% completion"). The same description is also listed in the game folder's
  `README.md` so you can read it without downloading the archive.
- Each platform folder has a `README.md` listing all its games.

## Save states are emulator-specific

Save states usually only work in the emulator (and often the same version) that
created them — for example mGBA and VBA-M states are not interchangeable. The
emulator is recorded in each save's `README.txt`/`.txt` file; check it before use.

## Contributing

Contributions come in as pull requests (created automatically by SaveHub).
- Repository **owners and contributors** may enable auto-merge (at their own risk).
- Everyone else contributes via a fork + pull request, which the owner reviews and
  merges.

---
Managed with [SaveHub](https://github.com/uncle-uee/SaveHub).
