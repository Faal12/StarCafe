"""Cuts the hand-drawn chibi player art into game-ready map frames.

Input  (Assets/Portraits, kept on disk, not shipped):
    Character 2.png        front idle
    Character 3.png        back idle
    Animation/Slide 1-4.png  four walk frames (figure faces left)
Output (Assets/Sprites/Player): player_idle_front/back.png, player_walk_1..4.png

All frames share one canvas and one scale (art is 2x the game size, the Sprite2D shows
them at exactly scale 0.5, so mipmapping stays clean). Feet touch the bottom edge, the body centre sits on the middle.
Run: python Tools/build_player_frames.py
"""
import os
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
SRC = os.path.join(ROOT, "Assets", "Portraits")
DST = os.path.join(ROOT, "Assets", "Sprites", "Player")

CANVAS = (108, 156)         # 2x game size 54x78
FIGURE_HEIGHT = 144         # tallest pose after scaling (72 px in game)
REFERENCE_WIDTH = 3750      # canvas width of most source files; Slide 1 was drawn smaller

FRAMES = {
    "player_idle_front.png": "Character 2.png",
    "player_idle_back.png": "Character 3.png",
    "player_walk_1.png": "Animation/Slide 1.png",
    "player_walk_2.png": "Animation/Slide 2.png",
    "player_walk_3.png": "Animation/Slide 3.png",
    "player_walk_4.png": "Animation/Slide 4.png",
}


def load(name):
    im = Image.open(os.path.join(SRC, name)).convert("RGBA")
    unit = REFERENCE_WIDTH / im.width          # bring every sheet to the same drawing scale
    box = im.getbbox()
    cropped = im.crop(box)
    if unit != 1:
        cropped = cropped.resize((round(cropped.width * unit), round(cropped.height * unit)), Image.LANCZOS)
    return cropped


def body_centre_x(img):
    """Alpha-weighted horizontal centre (steady while the legs swing)."""
    alpha = img.getchannel("A")
    width, height = img.size
    cols = [sum(alpha.getpixel((x, y)) for y in range(0, height, 4)) for x in range(width)]
    total = sum(cols)
    return sum(x * c for x, c in enumerate(cols)) / total if total else width / 2


def main():
    os.makedirs(DST, exist_ok=True)
    sources = {out: load(name) for out, name in FRAMES.items()}
    scale = FIGURE_HEIGHT / max(img.height for img in sources.values())

    for out, img in sources.items():
        small = img.resize((max(1, round(img.width * scale)), max(1, round(img.height * scale))), Image.LANCZOS)
        cx = body_centre_x(small)
        canvas = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
        x = round(CANVAS[0] / 2 - cx)
        y = CANVAS[1] - small.height
        canvas.alpha_composite(small, (x, y))
        canvas.save(os.path.join(DST, out))
        print(out, small.size, "at", (x, y))


if __name__ == "__main__":
    main()
