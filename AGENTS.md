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
