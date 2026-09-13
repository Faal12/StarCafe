"""One-off generator for placeholder sprite/tile art. Run with: python3 Tools/generate_placeholder_art.py
Re-run any time to regenerate; output goes to Assets/Sprites and Assets/Env.
"""
from PIL import Image, ImageDraw
import os

BASE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SPRITES = os.path.join(BASE, "Assets", "Sprites")
ENV = os.path.join(BASE, "Assets", "Env")
os.makedirs(SPRITES, exist_ok=True)
os.makedirs(ENV, exist_ok=True)


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


def metal_wall(path, w, h, horizontal):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w, h], fill=(90, 96, 104, 255))
    step = 32
    if horizontal:
        for x in range(0, w, step):
            d.line([(x, 0), (x, h)], fill=(60, 66, 74, 255), width=2)
            d.ellipse([x + 4, h // 2 - 2, x + 8, h // 2 + 2], fill=(50, 55, 62, 255))
    else:
        for y in range(0, h, step):
            d.line([(0, y), (w, y)], fill=(60, 66, 74, 255), width=2)
            d.ellipse([w // 2 - 2, y + 4, w // 2 + 2, y + 8], fill=(50, 55, 62, 255))
    d.rectangle([0, 0, w - 1, h - 1], outline=(40, 44, 50, 255), width=2)
    img.save(path)


def floor(path, w, h):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    plank_h = 36
    base_colors = [(122, 87, 58), (112, 79, 52), (130, 94, 63)]
    for i, y in enumerate(range(0, h, plank_h)):
        color = base_colors[i % len(base_colors)]
        d.rectangle([0, y, w, y + plank_h], fill=(*color, 255))
        d.line([(0, y), (w, y)], fill=(70, 48, 30, 255), width=1)
    for x in range(0, w, 96):
        d.line([(x, 0), (x, h)], fill=(70, 48, 30, 80), width=1)
    img.save(path)


def counter(path, w, h):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w, h], fill=(96, 63, 38, 255))
    d.rectangle([0, 0, w, 10], fill=(140, 100, 65, 255))
    d.rectangle([0, 0, w - 1, h - 1], outline=(60, 38, 22, 255), width=3)
    img.save(path)


humanoid(os.path.join(SPRITES, "player_front.png"), (230, 190, 150, 255), (60, 90, 160, 255), True)
humanoid(os.path.join(SPRITES, "player_back.png"), (230, 190, 150, 255), (60, 90, 160, 255), False)
humanoid(os.path.join(SPRITES, "npc_customer.png"), (140, 180, 120, 255), (200, 120, 40, 255), True)

metal_wall(os.path.join(ENV, "wall_top.png"), 1152, 32, True)
metal_wall(os.path.join(ENV, "wall_bottom.png"), 1152, 32, True)
metal_wall(os.path.join(ENV, "wall_left.png"), 32, 648, False)
metal_wall(os.path.join(ENV, "wall_right.png"), 32, 648, False)

floor(os.path.join(ENV, "floor_tavern.png"), 1088, 584)
counter(os.path.join(ENV, "bar_counter.png"), 400, 64)

print("Done: generated placeholder art in", SPRITES, "and", ENV)
