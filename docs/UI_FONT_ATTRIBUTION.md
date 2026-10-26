# UI font attribution

The app bundles unmodified static font files. Retain the adjacent license files
when distributing the app or its source.

| Family | Use | Bundled faces | Official source |
| --- | --- | --- | --- |
| Wanted Sans | Reading text, settings, navigation and buttons | Regular (400), Medium (500), SemiBold (600) | [Wanted Sans](https://github.com/wanteddev/wanted-sans) |
| LINE Seed KR | Garden brand, plant title, growth and reward headings | Bold (700) | [LINE Seed](https://seed.line.me/index_kr.html) |

Wanted Sans: Copyright 2024 The Wanted Sans Project Authors. Downloaded from
the official repository's `packages/wanted-sans/fonts/ttf/` directory on
2026-10-26; revision `02c9b822349c188ada95f9e2d90c2ed18f853235`.
Its original SIL Open Font License 1.1 is preserved in
`Assets/Resources/Fonts/WantedSans-OFL.txt` and the Android font asset directory.

LINE Seed KR: Copyright © LY Corporation. Downloaded from the official
`LINE_Seed_Sans_KR.zip` distribution on 2026-10-26; archive directory
`LINE_SeedKR_2023.09.06`, TTF `LINESeedKR-Bd.ttf`. The font's own name table
declares SIL Open Font License 1.1 and the copyright above. That attribution
and the full standard license text are retained in
`Assets/Resources/Fonts/LINESeedKR-OFL.txt`.

| File | SHA-256 |
| --- | --- |
| WantedSans-Regular.ttf | `849b51938f5779e7526fcf09b3c737e9d401261d111f626280af16423bf19f40` |
| WantedSans-Medium.ttf | `e5f05e044e105b699f7eea82f9875ebcad46bfe73118a6ef60d48889631816aa` |
| WantedSans-SemiBold.ttf | `1378a4914ebc4f658af9d68284048ddbc904a9704ae78fcdd84e7862bde89d70` |
| LINESeedKR-Bd.ttf | `df6d817f60814528922a0e2911b76a1252db0697c2de78a7283ea7a3c8e70e21` |

Unity and native Android use identical Wanted Sans binaries. TextMesh Pro uses
dynamic atlases with explicit Medium and SemiBold face mappings. Legacy uGUI
text selects the actual face with normal styling instead of synthesizing bold.
The existing size scale is unchanged. LINE Seed is limited to short garden
display text; long instructions and settings remain Wanted Sans.

The button-edge vines and leaves are original geometry in
`GardenButtonBotanicals.cs`. They use no downloaded image asset or extra texture.
Only wide primary buttons receive them; the label area, layout and hit target
remain unchanged.
