# App icons

Master sources for the Dependinator application icon (a dependency tree on a
DeepPurple squircle, matching the app's brand color).

- `icon-rounded.svg` — rounded squircle background; used for favicons and the
  192px icon (browser tabs + VS Code extension icon).
- `icon-square.svg` — full-bleed square background; used for the iOS
  `apple-touch-icon` (iOS applies its own corner mask, so the source must not
  have transparent corners).

## Regenerate the raster assets

```bash
python3 -m pip install cairosvg pillow   # one-time
python3 branding/icons/build_icons.py
```

This writes:

| Output | Size | Destination |
| --- | --- | --- |
| `favicon.png` | 32 | `src/Dependinator.Wasm/wwwroot`, `src/Dependinator.Web/wwwroot` |
| `favicon.ico` | 16/32/48 | `src/Dependinator.Wasm/wwwroot` |
| `icon-192.png` | 192 | `src/Dependinator.Wasm/wwwroot` |
| `apple-touch-icon.png` | 180 | `src/Dependinator.Wasm/wwwroot`, `src/Dependinator.Web/wwwroot` |

The VS Code extension icon (`src/DependinatorVsCode/media/icon-192.png`,
referenced by `package.json`) is **generated** from the Wasm `wwwroot` by
`src/DependinatorVsCode/scripts/prepare-wasm.sh`, so it picks up changes
automatically on the next extension build (`./build-ext`).
