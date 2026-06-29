# App icons

Master sources for the Dependinator application icon (a dependency tree on a
DeepPurple squircle, matching the app's brand color).

- `icon-rounded.svg` — rounded squircle background; used for favicons and the
  192px icon (browser tabs + VS Code extension icon).
- `icon-square.svg` — full-bleed square background; used for the iOS
  `apple-touch-icon` (iOS applies its own corner mask, so the source must not
  have transparent corners).
- `icon-glyph-dark.svg` / `icon-glyph-light.svg` — glyphs (no background) for the
  VS Code extension editor title-bar button: brand-purple (`#7C4DFF`) nodes with
  theme-gray edges (`#424242` dark glyph for light themes, `#C5C5C5` light glyph
  for dark themes).
- `icon-glyph-readme.svg` — same glyph with neutral-gray (`#6E6E6E`) edges,
  rasterized to a PNG for the extension README (the Marketplace blocks SVG
  images), so it reads on both light and dark backgrounds.

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
| `icon-tree-dark.svg` / `icon-tree-light.svg` | vector | `src/DependinatorVsCode/resources` (title-bar button) |
| `icon-toolbar.png` | 54 | `src/DependinatorVsCode/resources` (inline icon in the extension README) |

The VS Code extension icon (`src/DependinatorVsCode/media/icon-192.png`,
referenced by `package.json`) is **generated** from the Wasm `wwwroot` by
`src/DependinatorVsCode/scripts/prepare-wasm.sh`, so it picks up changes
automatically on the next extension build (`./build-ext`).
