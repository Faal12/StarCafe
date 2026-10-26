"""Derives the UI art from the hand-drawn sources (kept on disk, not shipped).

    python Tools/build_ui_assets.py

Assets/Portraits/Logo.png (Krümel with his cup) -> Assets/UI/kruemel.png, cropped and scaled for menus.
"""
import os
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
SRC = os.path.join(ROOT, "Assets", "Portraits", "Logo.png")
DST = os.path.join(ROOT, "Assets", "UI")
HEIGHT = 500  # drawn at exactly half this size in the menu


def main():
    os.makedirs(DST, exist_ok=True)
    image = Image.open(SRC).convert("RGBA")
    image = image.crop(image.getbbox())
    image = image.resize((round(image.width * HEIGHT / image.height), HEIGHT), Image.LANCZOS)
    path = os.path.join(DST, "kruemel.png")
    image.save(path, optimize=True)
    print("kruemel.png", image.size, round(os.path.getsize(path) / 1000), "kB")


if __name__ == "__main__":
    main()
