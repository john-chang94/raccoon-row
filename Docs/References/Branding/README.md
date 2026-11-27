# Temporary app icon

`RaccoonRow-TemporaryHeadshot.png` uses the neutral Scout from the corrected Krita idle master (`CorrectedScout/Masters/scout-idle-corrected.kra` in the legacy project). The drawing was not changed: only framing, a circular headshot crop, and flat background and padding were added. The square is 1024 × 1024 with no alpha channel.

Used in the current iOS Xcode export. Unity Player Settings and the previously signed Android bundle are not updated by the Xcode icon change.

The Android store build uses the same approved headshot with distinct adaptive foreground and background textures so system masks retain the padding. Launcher source textures are in `Assets/DumpsterDynasty/Art/Branding`, and the store-prep build helper assigns adaptive, round and fallback slots. The separate `Raccoon-Row-Play-Store-Icon-512.png` is for the Play listing, not the Android bundle.
