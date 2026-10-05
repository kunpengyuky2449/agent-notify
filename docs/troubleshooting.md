# Notification checks and troubleshooting

Check one layer at a time. `queued`, a received inbox message, a Notification Center entry and a visible banner are different observations.

## First verification

1. Tray status should be **Connected**. Use **Local notification test** to test Windows without a cloud send. Check the visible banner, sound and Notification Center; click the banner to open the inbox.
2. Send one cloud test with a unique EventId. Confirm the phone while locked and Windows with a different app focused. Close browsers completely if validating browser independence.
3. Return to your actual game, wait for one deliberate `action-required` test, and confirm the banner there. Record the game/display mode rather than assuming one result applies everywhere.
4. Check reconnection after a network interruption, repeated launch yielding one receiver instance, and startup after the next Windows sign-in.

## Phone received, no Windows banner

- Check the app's inbox and Windows Notification Center first. If both have the message, receiver/cloud transport worked; investigate display settings.
- **Settings → System → Notifications → Agent Notify:** enable **Show notification banners**, **Show notifications in notification center** and sound.
- Go back to **System → Notifications → Set priority notifications → Add apps**, and add **Agent Notify**. The per-app **Normal/High/Top** setting only sorts Notification Center; it is not the Do Not Disturb permission.
- Inspect automatic Do Not Disturb conditions for **playing a game**, **full-screen apps**, duplicated displays and time ranges. With permission, temporarily turn Do Not Disturb off for a controlled test.
- Compare a desktop test and the actual game's borderless/maximized/exclusive-full-screen modes. Native Windows banners are not a game overlay and cannot guarantee visibility in every rendering mode.

See [Microsoft's notification and Do Not Disturb instructions](https://support.microsoft.com/en-us/windows/experience/notifications-and-do-not-disturb-in-windows). Windows 10 uses Focus Assist for the corresponding controls. The original Windows 11 tests succeeded after notification configuration; a received message alone did not establish a visible banner.

## Connection or account problems

Check internet access and Pushover Desktop trial/license, then tray **Reconnect**. A rejected session or another client using the same receiver session stops automatic reconnect. Register a separate device/session on another PC. Login may need the current two-factor code. A device-creation timeout may already have registered a device; inspect the account before retrying.

## Sender cannot decrypt / permission errors

Run setup under the same normal Windows user that will send. A Codex sandbox may use a different identity; approved execution as the configuring user may be necessary. Do not weaken directory ACLs or export decrypted tokens to work around this. The app does not include a background authenticated local relay.

If an antivirus or OS policy blocks a script/executable, report the actual action and message. Do not disguise the binary, disable protection or keep retrying a rejected action. Inspect source/signatures and use the organization's approved process when applicable.

## Duplicate alerts / missing repeats

Close/disable the browser client after native reception is verified. Every registered device has its own delivery; the phone and Windows are expected to both receive. The sender's stable EventId deduplicates successful and uncertain retries. Its ten-attempt hourly cap suppresses further attempts without a delayed queue. Inspect output rather than inventing another event ID to force a retry.

## Ask for help

Open a [GitHub issue](https://github.com/kunpengyuky2449/agent-notify/issues) or contact **neclab369b@gmail.com** if GitHub is unavailable. Include Windows version, app version, exact failing step, sanitized error and whether the test reached phone/inbox/Notification Center/banner. Do not upload session.json, settings.json, sender state, private inboxes or full message screenshots.
