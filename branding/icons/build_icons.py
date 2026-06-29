#!/usr/bin/env python3
"""Render the Dependinator app icon SVGs into all required PNG/ICO assets."""
import io
import os
import cairosvg
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
ROUNDED = os.path.join(HERE, "icon-rounded.svg")
SQUARE = os.path.join(HERE, "icon-square.svg")

WASM = os.path.join(REPO, "src/Dependinator.Wasm/wwwroot")
WEB = os.path.join(REPO, "src/Dependinator.Web/wwwroot")


def render(svg, size):
    png = cairosvg.svg2png(url=svg, output_width=size, output_height=size)
    return Image.open(io.BytesIO(png)).convert("RGBA")


def save_png(svg, size, path):
    render(svg, size).save(path, format="PNG")
    print(f"  PNG  {size:>4}  -> {os.path.relpath(path, REPO)}")


def save_ico(svg, path, sizes=(16, 32, 48)):
    # Render large, let Pillow downscale into each ICO entry.
    base = render(svg, 256)
    base.save(path, format="ICO", sizes=[(s, s) for s in sizes])
    print(f"  ICO  {sizes} -> {os.path.relpath(path, REPO)}")


def main():
    # Wasm host (also feeds the VS Code extension via prepare-wasm.sh)
    save_png(ROUNDED, 32, os.path.join(WASM, "favicon.png"))
    save_png(ROUNDED, 192, os.path.join(WASM, "icon-192.png"))
    save_ico(ROUNDED, os.path.join(WASM, "favicon.ico"))
    save_png(SQUARE, 180, os.path.join(WASM, "apple-touch-icon.png"))

    # Blazor Server host
    save_png(ROUNDED, 32, os.path.join(WEB, "favicon.png"))
    save_png(SQUARE, 180, os.path.join(WEB, "apple-touch-icon.png"))

    print("Done.")


if __name__ == "__main__":
    main()
