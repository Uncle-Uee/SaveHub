# Provider setup guide

Step-by-step instructions to create an account and get SaveHub connected to each
backend: **GitHub**, **Supabase**, and **Google Drive**. Pick one — you only need a
single provider.

All three end with the same two CLI steps:

```powershell
dotnet run --project src/SaveHub.Cli -- config use <provider>   # github | supabase | googledrive
dotnet run --project src/SaveHub.Cli -- config test
```

The desktop app has the same options on its **Settings** tab.

---

## GitHub

Best for a shared, reviewable database (pull requests, history, easy public sharing).

### 1. Create a repository
1. Sign in at <https://github.com> (free).
2. Click **New repository**, give it a name (e.g. `emu-saves`), choose **Public**
   (or Private), and **Create repository**.

### 2. Create a personal access token (PAT)
1. Go to **Settings → Developer settings → Personal access tokens**.
2. **Fine-grained token** (recommended):
   - *Resource owner*: your account.
   - *Repository access*: **Only select repositories** → your repo.
   - *Permissions*: **Contents = Read and write**, **Pull requests = Read and write**.
   - Generate and **copy** the token (`github_pat_...`).
   - *Classic token* alternative: scope `repo` (or `public_repo` for public repos).

### 3. Configure SaveHub
```powershell
dotnet run --project src/SaveHub.Cli -- config github --owner YOUR-NAME --repo emu-saves
# optional: publish immediately when you have write access
# ... --auto-merge

$env:SAVEHUB_GITHUB_TOKEN = "github_pat_xxx"     # or persist for your user (see README)
dotnet run --project src/SaveHub.Cli -- config test
```

You should see your login, the repository, and write access.

---

## Supabase

Best if you want an S3-like bucket with API-key access and your own access rules.

### 1. Create an account + project
1. Sign up at <https://supabase.com> (free tier available).
2. **New project** → pick a name, a strong database password, and a region → create.
   Wait for it to finish provisioning.

### 2. Create a Storage bucket
1. In the project, open **Storage** → **New bucket**.
2. Name it `saves`. Make it **public** if you want anonymous downloads; keep it
   **private** if only you should read it.

### 3. Get your project URL and API key
1. **Project Settings → API**.
2. Copy the **Project URL** (`https://YOUR-PROJECT.supabase.co`).
3. Copy an **API key**:
   - `anon` key — public, subject to your RLS policies.
   - `service_role` key — **powerful, bypasses RLS**; keep it secret and only use it
     for your own private/owner setup.

### 4. (Recommended) Storage access policies
By default a private bucket is locked down. For a personal owner backup, the
`service_role` key works without extra policies. For a shared/community bucket, add
Storage RLS policies: allow the owner to write everywhere, and (optionally) allow
others to write only under a `pending/` prefix. SaveHub already writes non-owner
uploads under `pending/` when `isOwner` is false.

### 5. Configure SaveHub
```powershell
dotnet run --project src/SaveHub.Cli -- config supabase --url https://YOUR-PROJECT.supabase.co --bucket saves --owner
$env:SAVEHUB_SUPABASE_KEY = "your-key"
dotnet run --project src/SaveHub.Cli -- config test
```

---

## Google Drive

Best for a **personal backup** in your own Drive. SaveHub uses the least-privilege
**`drive.file`** scope, so it can only see and manage a folder **it creates** — it
cannot read the rest of your Drive. **Each user creates their own OAuth client**
(free); nothing secret is shipped with the app.

### 1. Create a Google Cloud project (free)
1. Go to <https://console.cloud.google.com> and sign in.
2. Top bar → **Select a project → New project** → name it (e.g. `SaveHub`) → create.

### 2. Enable the Drive API
1. **APIs & Services → Library** → search **Google Drive API** → **Enable**.

### 3. Configure the OAuth consent screen
1. **APIs & Services → OAuth consent screen**.
2. User type: **External** → **Create**.
3. Fill in the required app name and your email; you can skip optional fields.
4. **Scopes**: you don't need to add any here (SaveHub requests `drive.file` at
   sign-in).
5. **Test users**: add your own Google account.
6. Leave the app in **Testing** mode. That's enough for personal use — no
   verification and no fees. (Publishing to "Production" for outside users would
   trigger Google's review; `drive.file` avoids the expensive restricted-scope
   assessment, but Testing mode is simplest for personal use.)

### 4. Create an OAuth client (Desktop app)
1. **APIs & Services → Credentials → Create credentials → OAuth client ID**.
2. Application type: **Desktop app** → create.
3. Copy the **Client ID** and **Client secret**.

> Note: for a Desktop client, Google does not treat the client secret as truly
> confidential — but keep it to yourself; another party could use it to impersonate
> your app's consent screen or consume your project's quota. Since each user makes
> their own client, this stays your problem only for your own client.

### 5. Configure SaveHub
```powershell
dotnet run --project src/SaveHub.Cli -- config google --client-id "xxxxx.apps.googleusercontent.com" --owner
$env:SAVEHUB_GDRIVE_CLIENT_SECRET = "your-client-secret"

# opens your browser to sign in and grant access to a SaveHub folder it creates
dotnet run --project src/SaveHub.Cli -- config google-login
dotnet run --project src/SaveHub.Cli -- config test
```

- SaveHub creates (and reuses) a folder named **`SaveHub`** at your Drive root.
  Change it with `--folder-name "My Saves"`.
- The first sign-in shows an **"unverified app"** notice (because your app is in
  Testing) — that's expected for your own client; continue.
- The session lasts **~2.5 hours**; sign in again afterwards. In the desktop app the
  token is memory-only; in the CLI it's cached for convenience.

---

## Which should I choose?

| You want... | Use |
| --- | --- |
| A shared, public, reviewable database with history | **GitHub** |
| Your own bucket with API-key access / custom rules | **Supabase** |
| A private personal backup in your own Google Drive | **Google Drive** |

You can switch at any time with `config use <provider>` — all three produce the same
folder layout.
