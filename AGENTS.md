# Android device work

When connecting to a device with ADB, immediately read and durably record its
original `screen_off_timeout`, then temporarily set it to 1800000 ms (30 minutes)
before other device work. Use the explicit target serial. Never overwrite an
unrestored original value when reconnecting after a dropped connection.

Restore the recorded original value and verify it whenever the work finishes or
pauses. If the original setting was absent (`null`), delete the temporary setting
instead of inventing a value. If disconnection prevents restoration, preserve the
pending restoration record and clearly tell the user; restore on reconnection.

For extended device work, record `screen_brightness` and `screen_brightness_mode`,
then reduce brightness (manual mode, value 20). Restore and verify both settings
alongside the screen timeout when finishing or pausing.

Before updating or launching the app during a historical-data investigation,
back up its existing private sensor records, preferences, and external garden
save. Launching the daily collector prunes old files. Use an in-place APK update
and do not clear app data or logcat unless explicitly requested.

# Repository privacy and remote synchronization

The active project branch is `misssion` (three s characters), as requested by the user.
Continue implementation work there. Keep `main` at the integration merge unless
the user explicitly requests another merge or branch change.

Before each commit and push, inspect the changed files and run
`python3 scripts/check-repo-privacy.py` for the publishable working tree, then
`python3 scripts/check-repo-privacy.py --staged` for the exact staged contents
before committing. Gitleaks must be installed or supplied via `GITLEAKS_BIN`;
see `docs/REPOSITORY_PRIVACY.md`. A missing or failing scanner is not a pass.

Do not commit credentials, passwords, API/access tokens, private signing keys,
personal contact details, device serials, actual wireless ADB addresses, private
sensor/health records, app preferences, garden saves, or raw device captures.
Use environment variables, clearly synthetic fixtures, and placeholders in
examples. Keep device backups, screenshots, logs, and restoration journals in
ignored `artifacts/` or a private location outside the repository. Never remove
pending device restoration records during privacy cleanup.

Review prose, screenshots, binary assets, and test fixtures as well as code;
automated scanners cannot establish whether health or free-text data is personal.
Retain public third-party license/copyright attribution. Report findings by
file, location, and category without repeating the sensitive value. Replace or
remove sensitive values in the current tree; do not rewrite Git history unless
the user explicitly requests it. If a live credential is found, remove it and
tell the user it also needs revocation/rotation; deletion alone does not revoke it.

Commit or push only when the user explicitly asks to publish the changes to Git.
Otherwise keep completed work local and unstaged. This overrides the former
automatic commit/push workflow at the user's request.
When publication is requested, review the intended files and outgoing commits,
run the privacy checks above, use a normal non-force push to the configured
remote/upstream, and verify that remote HEAD matches local HEAD. Do not include
unrelated work. If a push fails, preserve local commits and report the blocker.
Do not force-push, rewrite history, or merge into another branch merely to make
synchronization succeed.
