# Git Guide: Saving, Pushing and Fetching Your Projects

A step-by-step guide for saving Unity projects with git and keeping them on GitHub.
All commands are typed in a terminal opened **in the project folder**
(for example `C:\Users\Karesz\My project (1)`).

---

## 0. The words you need

| Word | What it means |
|---|---|
| **Repository (repo)** | A project folder that git tracks. |
| **Commit** | A save point: a snapshot of every file at one moment. |
| **Push** | Upload your commits from this PC to GitHub. |
| **Fetch** | Download news from GitHub, but don't change your files yet. |
| **Pull** | Fetch **and** apply the news to your files (fetch + merge). |
| **Clone** | Download a whole project from GitHub for the first time. |
| **Remote / origin** | The GitHub copy of your project. `origin` is its nickname. |
| **Branch** | A line of history. Yours is called `main`. |

The life of a change:

```
 your files  --git add-->  staged  --git commit-->  local save point  --git push-->  GitHub
 your files  <--git pull--------------------------------------------------------  GitHub
```

---

## 1. One-time setup (once per computer)

### 1.1 Tell git who you are
This name and email go on every commit, in all projects (`--global`):

```bash
git config --global user.name "Shapedime"
```
```bash
git config --global user.email "your-shapedime-email@example.com"
```

To keep your real email private, use GitHub's no-reply address from
github.com → **Settings → Emails** (looks like `12345678+Shapedime@users.noreply.github.com`).

Check it:
```bash
git config --global user.name
```

### 1.2 Sign in to GitHub
You don't sign in with a command. The first time you push, a window pops up.
Choose **"Sign in with your browser"** and click **Authorize**. Git remembers it.

Which GitHub accounts git remembers:
```bash
git credential-manager github list
```

Forget an account (for example the wrong one):
```bash
git credential-manager github logout Kareszesz
```

---

## 2. Putting a NEW project on GitHub

Do this once for each new project.

**Step 1.** On github.com click **New repository**. Give it a name, choose
**Private**, and do **not** tick "Add a README" (keep it empty).

**Step 2.** In the project folder, start git:
```bash
git init -b main
```

**Step 3.** For Unity projects, add a `.gitignore` file before the first commit.
It stops git from saving the huge folders Unity rebuilds by itself
(`Library/`, `Temp/`, `Logs/`, `UserSettings/`). Copy the `.gitignore` from this
project into the new one.

**Step 4.** Make the first save point:
```bash
git add -A
```
```bash
git commit -m "First save"
```

**Step 5.** Link the folder to the GitHub repo (copy the address from GitHub's page):
```bash
git remote add origin https://github.com/Shapedime/YOUR-REPO-NAME.git
```

**Step 6.** Upload it:
```bash
git push -u origin main
```
`-u` remembers the link, so later a plain `git push` is enough.

---

## 3. Daily routine: save and push

Do this whenever you finish something that works.

**Step 1.** In Unity press **Ctrl+S**. Git only sees what is saved to disk.

**Step 2.** See what changed:
```bash
git status
```

**Step 3.** Mark everything for saving:
```bash
git add -A
```

**Step 4.** Make the save point, with a short note on what you did:
```bash
git commit -m "Added assisted spike"
```

**Step 5.** Upload to GitHub:
```bash
git push
```

Short version: **Ctrl+S → `git add -A` → `git commit -m "..."` → `git push`**

---

## 4. Getting a project onto another PC (clone)

Do this when a project is on GitHub but not on this computer yet.

**Step 1.** Open a terminal in the folder where you want the project, for example `C:\Users\Karesz`.

**Step 2.** Download it:
```bash
git clone https://github.com/Shapedime/Strand-R-pi-game-Unity-w-Claude.git
```
This creates a new folder with the whole project and all its history.

**Step 3.** Open that folder in **Unity Hub → Add → Add project from disk**.
The first open takes a while because Unity rebuilds `Library/`.

---

## 5. Fetching and pulling (getting the newest version)

Do this when you worked on another PC (or someone else pushed) and want those changes here.

**Step 1.** Close Unity, or at least save everything (**Ctrl+S**).

**Step 2.** See if GitHub has anything new, without touching your files:
```bash
git fetch
```
```bash
git status
```
- "Your branch is behind 'origin/main' by 2 commits" means there is something new to pull.
- "Your branch is up to date" means there is nothing to do.

**Step 3.** See what the new commits are:
```bash
git log --oneline main..origin/main
```

**Step 4.** Apply them to your files:
```bash
git pull
```

**Golden rule:** **pull before you start working, push when you stop.**
Then both PCs always stay in step.

---

## 6. Looking back and undoing

List your save points (newest first):
```bash
git log --oneline
```

See what you changed since the last save point:
```bash
git diff
```

Throw away **all** unsaved changes and go back to the last save point.
⚠️ This permanently deletes work you haven't committed.
```bash
git restore .
```

Bring back just one file from the last save point:
```bash
git restore Assets/Scripts/Player/BallContact.cs
```

Look at an old save point (replace `1302f5c` with an id from `git log --oneline`):
```bash
git switch --detach 1302f5c
```
Go back to the present:
```bash
git switch main
```

---

## 7. Common errors and fixes

### `error: remote origin already exists`
The project is already linked. Check where it points:
```bash
git remote -v
```
If the address is wrong, change it:
```bash
git remote set-url origin https://github.com/Shapedime/RIGHT-REPO.git
```

### `Permission ... denied to Kareszesz` / error 403
Git signed in with the wrong account. Forget it and push again; the browser
sign-in pops up:
```bash
git credential-manager github logout Kareszesz
```
```bash
git push
```

### `rejected ... (fetch first)` or `Updates were rejected`
GitHub has commits you don't have yet. Pull first, then push:
```bash
git pull
```
```bash
git push
```

### `CONFLICT (content): Merge conflict in ...`
The same file was changed on both PCs. Easiest safe way out, cancelling the pull:
```bash
git merge --abort
```
Then ask for help (or ask Claude) before trying again. Unity scenes and prefabs are
hard to merge by hand, so avoid editing the same scene on two PCs at once.

### `warning: ... LF will be replaced by CRLF`
Harmless. It's about Windows line endings. Ignore it.

### `nothing to commit, working tree clean`
Everything is already saved. Nothing to do.

### File too large (over 100 MB)
GitHub refuses single files over 100 MB. Keep huge files (videos, raw recordings)
out of the project, or look up **Git LFS**.

---

## 8. Cheat sheet

| I want to... | Command |
|---|---|
| See what changed | `git status` |
| Save a point | `git add -A` then `git commit -m "note"` |
| Upload | `git push` |
| Check GitHub for news | `git fetch` then `git status` |
| Download and apply news | `git pull` |
| Download a project for the first time | `git clone <address>` |
| List save points | `git log --oneline` |
| Undo unsaved changes ⚠️ | `git restore .` |
| See where GitHub link points | `git remote -v` |
| See which accounts git knows | `git credential-manager github list` |
