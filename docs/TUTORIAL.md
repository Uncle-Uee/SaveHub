# Tutorial: Upload a PS1/PS2 memory card to your GitHub repo

This guide takes you from a raw memory-card file to a published save in your own
GitHub repository. SaveHub does the zipping and reads the game's Title ID from the
card for you.

We focus on **PS1** and **PS2** memory cards. (For **PS3 and newer**, see
[the note at the end](#ps3-and-newer).)

---

## What you need

1. **.NET SDK 8 or later** (this repo uses .NET 10) — <https://dotnet.microsoft.com/download>.
2. A **public GitHub repository** to hold the saves (e.g. `your-name/emu-saves`).
3. A **GitHub personal access token (PAT)** with *Contents* and *Pull requests*
   read/write on that repo (or the classic `public_repo` scope).
4. A **memory-card file**:
   - PS1: a `.mcr` / `.mcd` / `.gme` / `.srm` card (128 KB) or one exported by your
     emulator.
   - PS2: a `.ps2` card image (from PCSX2's `memcards` folder).

> One memory card = one `.zip`. If your card holds saves for several games, see
> [Cards with multiple games](#cards-with-multiple-games).

---

## Step 1 — Build SaveHub

```powershell
git clone <this-repo>
cd SaveHub
dotnet build
```

## Step 2 — Point SaveHub at your repo

```powershell
dotnet run --project src/SaveHub.Cli -- config github --owner your-name --repo emu-saves
```

Provide your token via an environment variable (recommended — keeps it out of the
config file):

```powershell
# this session only
$env:SAVEHUB_GITHUB_TOKEN = "ghp_xxx"

# or persist for your user (reopen the terminal afterwards)
[Environment]::SetEnvironmentVariable("SAVEHUB_GITHUB_TOKEN", "ghp_xxx", "User")
```

Verify the connection:

```powershell
dotnet run --project src/SaveHub.Cli -- config test
```

You should see your GitHub login, the repository, and whether you have write access.

## Step 3 — (Optional) enable auto-merge

By default every upload opens a **pull request**. If it's your repo and you want
saves published immediately:

```powershell
dotnet run --project src/SaveHub.Cli -- config github --owner your-name --repo emu-saves --auto-merge
```

Auto-merge only applies when you have write access; it's at your own risk.

## Step 4 — Upload the memory card

You do **not** need to zip anything or look up the Title ID — SaveHub reads the
serial straight from the card and builds the zip.

**PS2 example:**

```powershell
dotnet run --project src/SaveHub.Cli -- upload `
  --platform PS2 --type mc `
  --file "C:\path\to\Mcd001.ps2" `
  --description "First level completed"
```

**PS1 example:**

```powershell
dotnet run --project src/SaveHub.Cli -- upload `
  --platform PS1 --type mc `
  --file "C:\path\to\card.mcr" `
  --description "100% completion"
```

SaveHub prints the detected id, e.g. `Detected game id from memory card: SCUS-97199`,
then either merges (auto-merge on) or gives you a pull-request link.

Tip: add a friendly name that appears in the platform's games list:
`--title "Ratchet & Clank"`.

### What just happened

SaveHub created this structure in your repo (creating the `PS2/` folder and the
games index the first time):

```
PS2/
  README.md                 # games index: | SCUS-97199 | Ratchet & Clank |
  SCUS-97199/
    README.md               # saves index: | 01.zip | Memory Card | First level completed |
    01.zip                  # your memory card + an embedded README.txt
    icon.jpg                # cover art (auto-downloaded for the serial)
```

- The **zip** contains your card plus a `README.txt` describing the save.
- The description also appears in the game's `README.md` so anyone can read it
  without downloading.
- A **cover image** was fetched automatically for PS1/PS2. To use your own instead:
  add `--icon "C:\art\cover.png"`. To skip the download: `--no-cover-art`.

## Step 5 — Download a save back

```powershell
dotnet run --project src/SaveHub.Cli -- list saves --platform PS2 --game SCUS-97199
dotnet run --project src/SaveHub.Cli -- download --platform PS2 --game SCUS-97199 --archive 01.zip --output ".\ratchet.zip"
```

For the full download walkthrough (unzipping, putting the card back into your
emulator, browser downloads), see the [download tutorial](TUTORIAL-DOWNLOAD.md).

## Step 6 — Update a save later (don't create a duplicate)

To replace `01.zip` instead of adding `02.zip`:

```powershell
dotnet run --project src/SaveHub.Cli -- upload `
  --platform PS2 --game SCUS-97199 --type mc --index 1 `
  --file "C:\path\to\Mcd001.ps2" `
  --description "All planets 100%"
```

(The desktop app's **Edit** tab does the same with a click.)

---

## How the Title ID is read from the card

You don't need to do this manually, but here's what SaveHub does and how to check
it yourself.

PlayStation cards store each save's **product code** (the game serial) as plain
text inside the card:

- **PS1**: each directory entry's filename looks like `BASLUS-00190ODDWORLD` —
  region prefix (`BA`), then the serial `SLUS-00190`, then a game tag.
- **PS2**: each save folder is named like `BASCUS-97199Rat&Clank` — the serial
  `SCUS-97199` appears verbatim.

SaveHub scans the card bytes for these serial patterns (`SLUS`, `SCUS`, `SLES`,
`SCES`, `SLPS`, ... followed by 5 digits) and uses the most common one.

**Check it yourself (PowerShell):**

```powershell
$enc = [Text.Encoding]::GetEncoding(28591)  # ISO-8859-1
$text = $enc.GetString([IO.File]::ReadAllBytes("C:\path\to\Mcd001.ps2"))
[regex]::Matches($text, '(SLUS|SCUS|SLES|SCES|SLPS|SLPM|SCPS)[-_ ]?\d{5}') |
  ForEach-Object { $_.Value } | Group-Object | Sort-Object Count -Descending
```

The top result (e.g. `SCUS-97199`) is your Title ID. If you ever need to override
detection, pass it explicitly with `--titleid SCUS-97199`.

### Cards with multiple games

A real memory card can hold saves for many games. SaveHub picks the **most
frequent** serial. If you want the card filed under a specific game, pass
`--titleid <serial>` (or `--name "<game name>"`) yourself.

---

## PS3 and newer

PS3/PS4/PS5 don't use memory cards — a save is a **folder** whose Title ID lives in
a binary `PARAM.SFO` file inside it. To upload one:

1. Select the **Folder** save type (`--type folder`).
2. Include the save's `PARAM.SFO` among the files — SaveHub reads `TITLE_ID`
   (e.g. `BLUS30490`, `CUSA12345`) from it automatically.

```powershell
dotnet run --project src/SaveHub.Cli -- upload `
  --platform PS4 --type folder `
  --file "C:\save\PARAM.SFO" --file "C:\save\savedata0" `
  --description "Chapter 5 start"
```

Requirements summary:

| Console | Save shape | Title ID source | SaveHub type |
| --- | --- | --- | --- |
| PS1 / PS2 | Memory card image | Read from the card | `mc` |
| PS3 / PS4 / PS5 | Save **folder** | `PARAM.SFO` `TITLE_ID` | `folder` |
| PSP / Vita | Folder w/ `PARAM.SFO` | `PARAM.SFO` `TITLE_ID`/`DISC_ID` | `folder` (or `mc`) |
| Nintendo (GBA, DS, ...) | Raw `.sav`/state | none — pass `--name` (else `Unknown`) | `mc`/`state` |

Cover art is auto-fetched for PS1/PS2/PSP; for other platforms add your own with
`--icon`.

---

## Prefer a GUI?

Everything above is also available in the **desktop app** (Windows):

```powershell
dotnet run --project src/SaveHub.WinForms
```

Use the **Settings** tab to connect (GitHub, Supabase, or Google Drive), then the
**Upload** tab: pick the device + save type, browse the card, type a description,
and click **Upload**. The Title ID is detected for you.
