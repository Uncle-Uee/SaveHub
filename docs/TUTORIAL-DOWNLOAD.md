# Tutorial: Download a memory card from your repo

This guide covers getting a save **out** of your SaveHub database and back into your
emulator. It works the same for any backend you've configured (GitHub, Supabase, or
Google Drive).

If you haven't uploaded anything yet, see
[Upload a PS1/PS2 memory card](TUTORIAL.md) first.

---

## What a downloaded save contains

Each archive is a `.zip` named `NN.zip` (memory card), `NN-sstate.zip` (save state),
or `NN-folder.zip` (PS3+ save folder). Inside you'll find:

- the raw save file(s) (e.g. `Mcd001.ps2`), and
- a `README.txt` describing what the save is for.

You **unzip** it and copy the raw file into your emulator's memory-card folder.

---

## Option A — Command line

### 1. Find the save you want

List platforms, then games, then the saves for a game:

```powershell
dotnet run --project src/SaveHub.Cli -- list platforms
dotnet run --project src/SaveHub.Cli -- list games --platform PS2
dotnet run --project src/SaveHub.Cli -- list saves  --platform PS2 --game SCUS-97199
```

`list saves` shows each archive with its type and description, for example:

```
Archive   Type         Description
01.zip    MemoryCard   First level completed
02.zip    MemoryCard   All planets 100%
```

### 2. Download it

```powershell
dotnet run --project src/SaveHub.Cli -- download `
  --platform PS2 --game SCUS-97199 --archive 01.zip `
  --output "C:\downloads\ratchet-01.zip"
```

`--output` is optional; without it the file is saved as the archive name in the
current folder.

### 3. Unzip and use it

```powershell
Expand-Archive "C:\downloads\ratchet-01.zip" -DestinationPath "C:\downloads\ratchet-01"
```

Read `README.txt` to confirm what the save is, then copy the card file
(e.g. `Mcd001.ps2`) into your emulator's memory-card folder:

- **PCSX2 (PS2):** `Documents\PCSX2\memcards\` (then select it in *Config →
  Memory Cards*).
- **DuckStation (PS1):** `memcards\` in your DuckStation user folder.

> Tip: back up your existing card first — copying overwrites it.

---

## Option B — Desktop app

```powershell
dotnet run --project src/SaveHub.WinForms
```

1. Open the **Download** tab.
2. Pick a **System** (e.g. `PS2`). SaveHub lists every game and save for it, with
   descriptions.
3. Select the row you want and click **Download Selected**.
4. Choose where to save the `.zip`.
5. Unzip it and copy the card file into your emulator, as above.

---

## Downloading from a browser (no tools)

Because the database is just files, you can also grab a save straight from the web:

- **GitHub:** open the file in your repo (e.g.
  `PS2/SCUS-97199/01.zip`) and click **Download**.
- **Supabase / Google Drive:** download the object/file from the bucket or shared
  folder.

The per-game `README.md` in each folder lists every save and its description, and
the platform `README.md` lists all games — so you can browse before downloading.

---

## Troubleshooting

- **"Not found"** — check the exact archive name with `list saves`; names are
  `01.zip`, `01-sstate.zip`, etc.
- **Private backend** — make sure your token/key is set (the same one used for
  uploads) so SaveHub can read the file.
- **Emulator doesn't see the card** — confirm you copied the *unzipped* card file
  (not the `.zip`) into the correct memory-card folder and selected it in the
  emulator's settings.
