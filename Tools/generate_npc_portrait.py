"""Placeholder bust portrait for the Guard NPC, until real character art exists.
Run with: python3 Tools/generate_npc_portrait.py
"""
from PIL import Image, ImageDraw
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Portraits", "npc_guard_portrait.png")

def build(w, h, full_body):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    scale = w / 500.0

    def sx(v):
        return v * scale

    def sy(v):
        return v * scale

    # Helmet dome
    d.ellipse([sx(120), sy(40), sx(380), sy(300)], fill=(150, 140, 125, 255), outline=(70, 62, 52, 255), width=max(2, int(6 * scale)))

    # Visor
    d.rounded_rectangle([sx(150), sy(140), sx(350), sy(200)], radius=sx(18), fill=(30, 20, 18, 255))
    d.rounded_rectangle([sx(160), sy(148), sx(340), sy(180)], radius=sx(12), fill=(200, 90, 40, 230))
    d.rectangle([sx(165), sy(158), sx(335), sy(170)], fill=(255, 150, 80, 255))

    # Helmet side vents
    d.rounded_rectangle([sx(120), sy(170), sx(150), sy(230)], radius=sx(8), fill=(90, 82, 70, 255))
    d.rounded_rectangle([sx(350), sy(170), sx(380), sy(230)], radius=sx(8), fill=(90, 82, 70, 255))

    # Neck — bridges the helmet base straight down, no gap
    d.rectangle([sx(205), sy(260), sx(295), sy(360)], fill=(120, 100, 80, 255))

    if not full_body:
        d.rounded_rectangle([sx(50), sy(330), sx(450), sy(h / scale)], radius=sx(30), fill=(58, 52, 46, 255))
        d.rounded_rectangle([sx(50), sy(330), sx(450), sy(380)], radius=sx(20), fill=(78, 70, 60, 255))
        d.rounded_rectangle([sx(225), sy(420), sx(275), sy(460)], radius=sx(6), fill=(200, 90, 40, 255))
        img.save(OUT)
        print("saved", OUT, img.size)
        return

    # --- Full body below the shoulders ---
    # Arms hanging at the sides
    d.rounded_rectangle([sx(30), sy(340), sx(95), sy(620)], radius=sx(20), fill=(70, 62, 52, 255))
    d.rounded_rectangle([sx(405), sy(340), sx(470), sy(620)], radius=sx(20), fill=(70, 62, 52, 255))
    d.rounded_rectangle([sx(35), sy(590), sx(90), sy(640)], radius=sx(14), fill=(120, 100, 80, 255))
    d.rounded_rectangle([sx(410), sy(590), sx(465), sy(640)], radius=sx(14), fill=(120, 100, 80, 255))

    # Torso / jacket
    d.rounded_rectangle([sx(100), sy(330), sx(400), sy(660)], radius=sx(30), fill=(58, 52, 46, 255))
    d.rounded_rectangle([sx(100), sy(330), sx(400), sy(380)], radius=sx(20), fill=(78, 70, 60, 255))
    d.rounded_rectangle([sx(225), sy(470), sx(275), sy(510)], radius=sx(6), fill=(200, 90, 40, 255))

    # Belt
    d.rectangle([sx(100), sy(640), sx(400), sy(670)], fill=(40, 34, 28, 255))
    d.rectangle([sx(225), sy(645), sx(275), sy(665)], fill=(200, 90, 40, 255))

    # Legs
    d.rounded_rectangle([sx(130), sy(660), sx(240), sy(940)], radius=sx(10), fill=(46, 42, 38, 255))
    d.rounded_rectangle([sx(260), sy(660), sx(370), sy(940)], radius=sx(10), fill=(46, 42, 38, 255))

    # Boots
    d.rounded_rectangle([sx(120), sy(920), sx(245), sy(985)], radius=sx(10), fill=(30, 26, 22, 255))
    d.rounded_rectangle([sx(255), sy(920), sx(380), sy(985)], radius=sx(10), fill=(30, 26, 22, 255))

    img.save(OUT)
    print("saved", OUT, img.size)


build(500, 620, full_body=False)
OUT = os.path.join(ROOT, "Assets", "Portraits", "npc_guard_fullbody.png")
build(500, 1000, full_body=True)
