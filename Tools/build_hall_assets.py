"""Builds derived textures for the main hall from packs/ source art + procedural furniture.
Run with: python3 Tools/build_hall_assets.py
"""
from PIL import Image, ImageDraw, ImageOps
import os
import colorsys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PACKS = os.path.join(ROOT, "Assets", "packs")
SHIP = os.path.join(ROOT, "Assets", "Ship")
os.makedirs(SHIP, exist_ok=True)

SCIFI_ARCH = os.path.join(PACKS, "Sci-FiSpaceship&Station2DMegaPropsPack_Sample_v1.0", "Tiles", "Architecture")


def tile_texture(src_path, out_path, target_w, target_h, rotate90=False, fit_thickness=None):
    tile = Image.open(src_path).convert("RGBA")
    if fit_thickness is not None:
        # Scale the tile so its short side exactly matches the wall thickness,
        # preserving aspect ratio, before repeating it along the wall's length.
        scale = fit_thickness / tile.height
        tile = tile.resize((max(1, round(tile.width * scale)), fit_thickness))
    if rotate90:
        tile = tile.rotate(90, expand=True)
    tw, th = tile.size
    out = Image.new("RGBA", (target_w, target_h))
    for y in range(0, target_h, th):
        for x in range(0, target_w, tw):
            out.paste(tile, (x, y), tile)
    out = out.crop((0, 0, target_w, target_h))
    out.save(out_path)
    print("tiled", out_path, out.size)


def recolor_hue(src_path, out_path, hue_shift):
    img = Image.open(src_path).convert("RGBA")
    r, g, b, a = img.split()
    rgb = Image.merge("RGB", (r, g, b))
    hsv = rgb.convert("HSV")
    h, s, v = hsv.split()
    h = h.point(lambda p: int((p + hue_shift) % 256))
    hsv2 = Image.merge("HSV", (h, s, v))
    rgb2 = hsv2.convert("RGB")
    out = Image.merge("RGBA", (*rgb2.split(), a))
    out.save(out_path)
    print("recolored", out_path, out.size)


def crop_frame(src_path, out_path, box):
    img = Image.open(src_path).convert("RGBA")
    frame = img.crop(box)
    frame = frame.resize((frame.width * 6, frame.height * 6), Image.NEAREST)
    frame.save(out_path)
    print("cropped frame", out_path, frame.size)


def round_table(path, size=72):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([4, 8, size - 4, size - 4], fill=(30, 34, 42, 130))
    d.ellipse([2, 2, size - 6, size - 8], fill=(52, 60, 72, 255), outline=(90, 200, 220, 255), width=3)
    d.ellipse([size // 2 - 6, size // 2 - 9, size // 2 + 6, size // 2 + 3], fill=(90, 200, 220, 180))
    img.save(path)


def sci_chair(path, size=28):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([2, size - 8, size - 2, size - 2], fill=(0, 0, 0, 60))
    d.rounded_rectangle([4, 4, size - 4, size - 6], radius=5, fill=(45, 50, 60, 255), outline=(80, 180, 200, 255), width=2)
    img.save(path)


def sci_couch(path, w=96, h=40):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([4, h - 10, w - 4, h - 2], fill=(0, 0, 0, 60))
    d.rounded_rectangle([2, 6, w - 2, h - 6], radius=8, fill=(48, 56, 68, 255), outline=(90, 200, 220, 255), width=3)
    d.rounded_rectangle([2, 2, w - 2, 14], radius=6, fill=(58, 66, 80, 255))
    img.save(path)


def sci_counter(path, w=800, h=80):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w, h], fill=(46, 52, 62, 255))
    d.rectangle([0, 0, w, 12], fill=(70, 80, 94, 255))
    d.rectangle([0, 12, w, 16], fill=(90, 200, 220, 255))
    d.rectangle([0, 0, w - 1, h - 1], outline=(25, 28, 34, 255), width=3)
    for x in range(20, w - 20, 90):
        d.rectangle([x, h - 22, x + 50, h - 10], fill=(60, 68, 80, 255), outline=(90, 200, 220, 180), width=1)
    img.save(path)


# --- Walls & floor, tiled from real Sci-Fi Architecture tiles ---
WALL_THICKNESS = 96
HALL_W, HALL_H = 1680, 960
TOTAL_W, TOTAL_H = HALL_W + WALL_THICKNESS * 2, HALL_H + WALL_THICKNESS * 2

tile_texture(os.path.join(SCIFI_ARCH, "scifi_architecture_floor_glow_strip_01.png"),
             os.path.join(SHIP, "floor_hall.png"), HALL_W, HALL_H)
tile_texture(os.path.join(SCIFI_ARCH, "scifi_architecture_wall_window_space_01.png"),
             os.path.join(SHIP, "wall_top.png"), TOTAL_W, WALL_THICKNESS, fit_thickness=WALL_THICKNESS)
tile_texture(os.path.join(SCIFI_ARCH, "scifi_architecture_wall_window_space_01.png"),
             os.path.join(SHIP, "wall_bottom.png"), TOTAL_W, WALL_THICKNESS, fit_thickness=WALL_THICKNESS)
tile_texture(os.path.join(SCIFI_ARCH, "scifi_architecture_wall_window_space_01.png"),
             os.path.join(SHIP, "wall_left.png"), WALL_THICKNESS, TOTAL_H, fit_thickness=WALL_THICKNESS, rotate90=True)
tile_texture(os.path.join(SCIFI_ARCH, "scifi_architecture_wall_window_space_01.png"),
             os.path.join(SHIP, "wall_right.png"), WALL_THICKNESS, TOTAL_H, fit_thickness=WALL_THICKNESS, rotate90=True)

# --- Door: real airlock tile, used as-is for entrance ---
door_src = os.path.join(SCIFI_ARCH, "scifi_architecture_door_airlock_round_01.png")
door_img = Image.open(door_src).convert("RGBA").resize((96, 96))
door_img.save(os.path.join(SHIP, "door_airlock.png"))
print("door_airlock saved")

# --- Elevator: crop last frame from the hotel/lab elevator sheet, upscaled (pixel-art look) ---
elevator_src = os.path.join(PACKS, "Hotel-Labratory-Sprites", "Sprite Sheets", "Elevator", "elevator-Sheet.png")
elevator_img = Image.open(elevator_src).convert("RGBA")
frame_w = elevator_img.width // 6
crop_frame(elevator_src, os.path.join(SHIP, "elevator_door.png"),
           (elevator_img.width - frame_w, 0, elevator_img.width, elevator_img.height))

# --- Service robot: recolor the TopView_Robot player sprite toward cyan/blue ---
robot_src = os.path.join(PACKS, "TopView_Robot_Asset_Pack", "TopView_Robot_Asset_Pack", "Player.png")
robot_img = Image.open(robot_src).convert("RGBA")
robot_single = robot_img.crop((0, 0, robot_img.width // 2, robot_img.height))
robot_single.save(os.path.join(SHIP, "_robot_raw.png"))
recolor_hue(os.path.join(SHIP, "_robot_raw.png"), os.path.join(SHIP, "service_robot.png"), hue_shift=90)
os.remove(os.path.join(SHIP, "_robot_raw.png"))

# --- Procedural furniture, matching the sci-fi palette ---
round_table(os.path.join(SHIP, "table_round.png"))
sci_chair(os.path.join(SHIP, "chair.png"))
sci_couch(os.path.join(SHIP, "couch.png"))
sci_counter(os.path.join(SHIP, "bar_counter_scifi.png"))

print("Done.")
