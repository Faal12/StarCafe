"""Placeholder top-down player sprites. Run with: python3 Tools/generate_placeholder_art.py
Output goes to Assets/Sprites (player_front.png / player_back.png).
"""
from PIL import Image, ImageDraw
import os

BASE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SPRITES = os.path.join(BASE, "Assets", "Sprites")
os.makedirs(SPRITES, exist_ok=True)


def humanoid(path, skin, outfit, facing_front):
    img = Image.new("RGBA", (32, 48), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([8, 43, 24, 47], fill=(0, 0, 0, 70))
    d.rectangle([11, 40, 14, 46], fill=(30, 25, 20, 255))
    d.rectangle([18, 40, 21, 46], fill=(30, 25, 20, 255))
    d.rounded_rectangle([7, 20, 25, 42], radius=4, fill=outfit)
    d.ellipse([9, 6, 23, 20], fill=skin)
    if facing_front:
        d.ellipse([12, 12, 14, 14], fill=(20, 20, 20, 255))
        d.ellipse([18, 12, 20, 14], fill=(20, 20, 20, 255))
    else:
        d.arc([9, 5, 23, 15], start=200, end=340, fill=(0, 0, 0, 90), width=2)
    img.save(path)


humanoid(os.path.join(SPRITES, "player_front.png"), (230, 190, 150, 255), (60, 90, 160, 255), True)
humanoid(os.path.join(SPRITES, "player_back.png"), (230, 190, 150, 255), (60, 90, 160, 255), False)

print("Done: generated player sprites in", SPRITES)
