# AGENTS.md for MissionPlanner

## General Guidelines
- Build target: .NET 8 / Xamarin.Android for Android port (`com.michaeloborne.MissionPlanner-Signed.apk`).
- When modifying UI or input logic, ensure non-destructive fallbacks and persistent settings using `Preferences` or `Settings`.

## Email-Driven Tasks (Mail Watcher)
When instructions arrive via the mail watcher (`[Watcher]` or notification containing email requests):
- **Execute Fully Autonomously**: NEVER ask for permission, approval, or "shall I proceed?". Even if asked "How should we proceed?", formulate the approach and immediately implement, build, and verify the changes to completion.
- **Strict Safety Protocol (セーフティ・プロトコル)**:
  1. **Branch Isolation**: Before making changes, create and switch to a dedicated isolation branch (e.g. `auto/YYYYMMDD-HHMMSS-<task_name>`). NEVER commit directly to `master`, `main`, or existing active branches without explicit instructions.
  2. **Local Commit Only**: Commit all changes locally to preserve full git history. NEVER run `git push` unless the user's email explicitly requests it (e.g., "プッシュして", "push to remote").
  3. **No Destructive Commands**: Absolutely NEVER run destructive or irreversible commands (such as `git reset --hard`, `git clean -fd`, `git checkout -- .`, `rm -rf`, `format`, or deleting databases/untracked files).
  4. **Safe Error Handling**: If a build fails or tests do not pass, do NOT panic-revert. Keep the commit on the isolation branch, capture build logs, and report the exact state via email.
- **Always Send Email Report**: Upon completion (or blocker), send an email response with results using:
  ```bash
  python3 /Users/murata/.config/anne_mail_watcher/send_mail.py "<to>" "<subject>" "<body>"
  ```
  Include: branch name, commit hash, changed files, build/test results, and next steps.
- **Ensure Watcher Daemon**: Keep `/Users/murata/.config/anne_mail_watcher/watcher.py` alive.

## Strict Prohibition on Builds (ビルド実行の絶対禁止)
- **NEVER RUN BUILDS**: アン（AI）は `dotnet build`, `msbuild`, `gradle` 等のビルドコマンドを絶対に実行してはならない。
- ユーザーの制限枠（Weekly Limit）とマシンリソースを保護するため。ビルドはすべてユーザー（むらさん）が行う。

