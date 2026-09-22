"""Placeholder bust portrait for the Guard NPC, until real character art exists.
Run with: python3 Tools/generate_npc_portrait.py
"""
from PIL import Image, ImageDraw
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Portraits", "npc_guard_portrait.png")

W, H = 500, 620
img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

# Shoulders / torso (dark utility jacket) — starts high enough to meet the neck/helmet
d.rounded_rectangle([50, 330, 450, 620], radius=30, fill=(58, 52, 46, 255))
d.rounded_rectangle([50, 330, 450, 380], radius=20, fill=(78, 70, 60, 255))

# Neck — bridges the helmet base straight down into the shoulders, no gap
d.rectangle([205, 260, 295, 360], fill=(120, 100, 80, 255))

# Helmet dome
d.ellipse([120, 40, 380, 300], fill=(150, 140, 125, 255), outline=(70, 62, 52, 255), width=6)

# Visor
d.rounded_rectangle([150, 140, 350, 200], radius=18, fill=(30, 20, 18, 255))
d.rounded_rectangle([160, 148, 340, 180], radius=12, fill=(200, 90, 40, 230))
d.rectangle([165, 158, 335, 170], fill=(255, 150, 80, 255))

# Helmet side vents
d.rounded_rectangle([120, 170, 150, 230], radius=8, fill=(90, 82, 70, 255))
d.rounded_rectangle([350, 170, 380, 230], radius=8, fill=(90, 82, 70, 255))

# Chest emblem
d.rounded_rectangle([225, 420, 275, 460], radius=6, fill=(200, 90, 40, 255))

img.save(OUT)
print("saved", OUT, img.size)
