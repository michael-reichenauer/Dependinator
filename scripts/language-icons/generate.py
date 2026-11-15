#!/usr/bin/env python3
# Generates the language tiles in src/Dependinator.UI/Diagrams/Icons/Library/Languages/: a rounded
# tile in the language's color with its letters, the icon of a source's root node (a solution, a
# TypeScript or JavaScript project, a Bicep deployment, a Go module). The letters are drawn as
# paths from Inter Bold (SIL Open Font License), so they look the same everywhere and app CSS on
# <text> cannot restyle them.
#
# Usage: pip install fonttools; python3 scripts/language-icons/generate.py [path/to/Inter-Bold.otf]
# To add a language, add a row to LANGUAGES and run it again.
import os
import sys

from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from fontTools.ttLib import TTFont

# Icon id: letters, tile gradient top and bottom, letter color. C# keeps the violet of the
# library's other icons; the others follow the common language colors, toned to sit beside them.
LANGUAGES = [
    ("CSharp", "C#", "#6F5BEA", "#5034C4", "#F1ECFF"),
    ("TypeScript", "TS", "#3D8BDB", "#2563B0", "#EAF4FF"),
    ("JavaScript", "JS", "#F2CF3A", "#D9AE12", "#3A3000"),
    ("Go", "Go", "#22BCE0", "#0A93B8", "#F0FBFF"),
    ("Bicep", "Bicep", "#1FAF9B", "#0E8475", "#EFFFFC"),
]

CAP_HEIGHT = 7.2  # letter height in the 24-unit icon, for two letters
MAX_WIDTH = 15.0  # widest the letters may be (longer codes get smaller)
CENTER_Y = 12.0

root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
out = os.path.join(root, "src/Dependinator.UI/Diagrams/Icons/Library/Languages")
font_path = sys.argv[1] if len(sys.argv) > 1 else "/usr/share/fonts/opentype/inter/Inter-Bold.otf"
font = TTFont(font_path)
glyphs = font.getGlyphSet()
cmap = font.getBestCmap()
cap_units = font["OS/2"].sCapHeight


def letters_path(text):
    # Lays the letters out in font units (advance widths, no kerning), then scales and centers
    # their outline box on the tile.
    pens = []
    x = 0
    for ch in text:
        name = cmap[ord(ch)]
        pens.append((name, x))
        x += glyphs[name].width
    bounds = BoundsPen(glyphs)
    for name, dx in pens:
        glyphs[name].draw(TransformPen(bounds, (1, 0, 0, 1, dx, 0)))
    x_min, y_min, x_max, y_max = bounds.bounds
    scale = min(CAP_HEIGHT / cap_units, MAX_WIDTH / (x_max - x_min))
    # Font units are y-up: flip, scale, and center the box.
    tx = 12 - scale * (x_min + x_max) / 2
    ty = CENTER_Y + scale * (y_min + y_max) / 2
    svg = SVGPathPen(glyphs, ntos=lambda v: f"{v:.2f}".rstrip("0").rstrip("."))
    for name, dx in pens:
        glyphs[name].draw(TransformPen(svg, (scale, 0, 0, -scale, tx + scale * dx, ty)))
    return svg.getCommands()


os.makedirs(out, exist_ok=True)
for icon_id, text, top, bottom, ink in LANGUAGES:
    grad = f"grad{icon_id}"
    svg = (
        f'<svg id="{icon_id}" xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24">'
        f'<defs><linearGradient id="{grad}" x1="0" y1="0" x2="0" y2="1">'
        f'<stop offset="0" stop-color="{top}" /><stop offset="1" stop-color="{bottom}" /></linearGradient></defs>'
        f'<rect x="2.5" y="2.5" width="19" height="19" rx="3.8" fill="url(#{grad})" />'
        f'<path d="{letters_path(text)}" fill="{ink}" /></svg>\n'
    )
    with open(os.path.join(out, f"{icon_id}.svg"), "w") as file:
        file.write(svg)
    print(f"{icon_id}.svg")
