"""Placeholder full-body portrait for the Guard NPC, until real character art exists.
Run with: python3 Tools/generate_npc_portrait.py
"""
from PIL import Image, ImageDraw
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Portraits", "npc_guard_fullbody.png")

W, H = 500, 1000
img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

# Helmet dome
d.ellipse([120, 40, 380, 300], fill=(150, 140, 125, 255), outline=(70, 62, 52, 255), width=6)

# Visor
d.rounded_rectangle([150, 140, 350, 200], radius=18, fill=(30, 20, 18, 255))
d.rounded_rectangle([160, 148, 340, 180], radius=12, fill=(200, 90, 40, 230))
d.rectangle([165, 158, 335, 170], fill=(255, 150, 80, 255))

# Helmet side vents
d.rounded_rectangle([120, 170, 150, 230], radius=8, fill=(90, 82, 70, 255))
d.rounded_rectangle([350, 170, 380, 230], radius=8, fill=(90, 82, 70, 255))

# Neck — bridges the helmet base straight down, no gap
d.rectangle([205, 260, 295, 360], fill=(120, 100, 80, 255))

# Arms hanging at the sides
d.rounded_rectangle([30, 340, 95, 620], radius=20, fill=(70, 62, 52, 255))
d.rounded_rectangle([405, 340, 470, 620], radius=20, fill=(70, 62, 52, 255))
d.rounded_rectangle([35, 590, 90, 640], radius=14, fill=(120, 100, 80, 255))
d.rounded_rectangle([410, 590, 465, 640], radius=14, fill=(120, 100, 80, 255))

# Torso / jacket
d.rounded_rectangle([100, 330, 400, 660], radius=30, fill=(58, 52, 46, 255))
d.rounded_rectangle([100, 330, 400, 380], radius=20, fill=(78, 70, 60, 255))
d.rounded_rectangle([225, 470, 275, 510], radius=6, fill=(200, 90, 40, 255))

# Belt
d.rectangle([100, 640, 400, 670], fill=(40, 34, 28, 255))
d.rectangle([225, 645, 275, 665], fill=(200, 90, 40, 255))

# Legs
d.rounded_rectangle([130, 660, 240, 940], radius=10, fill=(46, 42, 38, 255))
d.rounded_rectangle([260, 660, 370, 940], radius=10, fill=(46, 42, 38, 255))

# Boots
d.rounded_rectangle([120, 920, 245, 985], radius=10, fill=(30, 26, 22, 255))
d.rounded_rectangle([255, 920, 380, 985], radius=10, fill=(30, 26, 22, 255))

img.save(OUT)
print("saved", OUT, img.size)
