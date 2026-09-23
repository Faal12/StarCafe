"""Placeholder full-body portrait for Lerche (ship AI / narrator robot), until real art exists.
Run with: python3 Tools/generate_lerche_portrait.py
"""
from PIL import Image, ImageDraw
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Portraits", "lerche_fullbody.png")

W, H = 500, 1000
img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

COPPER = (176, 112, 62, 255)
COPPER_LIGHT = (212, 148, 88, 255)
COPPER_DARK = (110, 68, 38, 255)
METAL_DARK = (52, 42, 36, 255)
OUTLINE = (34, 26, 22, 255)
AMBER_GLOW = (255, 176, 64, 255)

# Antenna
d.line([(250, 70), (250, 20)], fill=METAL_DARK, width=8)
d.ellipse([234, 0, 266, 32], fill=AMBER_GLOW, outline=OUTLINE, width=4)

# Head
d.rounded_rectangle([120, 70, 380, 310], radius=70, fill=COPPER, outline=OUTLINE, width=6)
d.rounded_rectangle([140, 84, 360, 130], radius=24, fill=COPPER_LIGHT)
d.rounded_rectangle([96, 160, 122, 230], radius=8, fill=METAL_DARK)
d.rounded_rectangle([378, 160, 404, 230], radius=8, fill=METAL_DARK)

# One big eye
d.ellipse([190, 130, 310, 250], fill=METAL_DARK, outline=OUTLINE, width=6)
d.ellipse([206, 146, 294, 234], fill=AMBER_GLOW)
d.ellipse([234, 174, 266, 206], fill=OUTLINE)
d.ellipse([222, 158, 240, 176], fill=(255, 240, 200, 255))

# Raised eyebrow: sarcasm at rest
d.line([(190, 120), (312, 92)], fill=OUTLINE, width=12)

# Mouth grille
for x in range(200, 305, 18):
    d.line([(x, 275), (x, 296)], fill=OUTLINE, width=5)

# Neck
d.rectangle([215, 308, 285, 350], fill=METAL_DARK)

# Arms
d.rounded_rectangle([40, 350, 110, 640], radius=30, fill=COPPER_DARK, outline=OUTLINE, width=5)
d.rounded_rectangle([390, 350, 460, 640], radius=30, fill=COPPER_DARK, outline=OUTLINE, width=5)
d.ellipse([34, 620, 116, 700], fill=METAL_DARK, outline=OUTLINE, width=5)
d.ellipse([384, 620, 466, 700], fill=METAL_DARK, outline=OUTLINE, width=5)

# Torso
d.rounded_rectangle([100, 340, 400, 680], radius=40, fill=COPPER, outline=OUTLINE, width=6)
d.rounded_rectangle([120, 360, 380, 400], radius=16, fill=COPPER_LIGHT)
d.line([(250, 400), (250, 660)], fill=COPPER_DARK, width=4)

# Chest lamp: "das Licht"
d.ellipse([214, 470, 286, 542], fill=METAL_DARK, outline=OUTLINE, width=5)
d.ellipse([228, 484, 272, 528], fill=AMBER_GLOW)

# Panel rivets
for x, y in [(130, 620), (370, 620), (130, 440), (370, 440)]:
    d.ellipse([x - 7, y - 7, x + 7, y + 7], fill=COPPER_DARK, outline=OUTLINE, width=2)

# Legs
d.rounded_rectangle([140, 680, 235, 940], radius=18, fill=COPPER_DARK, outline=OUTLINE, width=5)
d.rounded_rectangle([265, 680, 360, 940], radius=18, fill=COPPER_DARK, outline=OUTLINE, width=5)

# Feet
d.rounded_rectangle([120, 920, 245, 985], radius=16, fill=METAL_DARK, outline=OUTLINE, width=5)
d.rounded_rectangle([255, 920, 380, 985], radius=16, fill=METAL_DARK, outline=OUTLINE, width=5)

img.save(OUT)
print("saved", OUT, img.size)
