"""Builds derived textures for the main hall: real seamless materials +
copied decor pieces + procedural furniture recolored to a warm accent palette.
Run with: python3 Tools/build_hall_assets.py
"""
from PIL import Image, ImageDraw
import os
import shutil

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PACKS = os.path.join(ROOT, "Assets", "packs")
SHIP = os.path.join(ROOT, "Assets", "Ship")
DECOR = os.path.join(SHIP, "Decor")
os.makedirs(SHIP, exist_ok=True)
os.makedirs(DECOR, exist_ok=True)

SCIFI_ARCH = os.path.join(PACKS, "Sci-FiSpaceship&Station2DMegaPropsPack_Sample_v1.0", "Tiles", "Architecture")


def tile_texture(src_path, out_path, target_w, target_h, rotate90=False, fit_thickness=None, tile_size=None):
    tile = Image.open(src_path).convert("RGBA")
    if tile_size is not None:
        # Downscale a big source texture (often 4K) to a sane on-screen repeat size
        # BEFORE tiling it, otherwise "tiling" just crops one giant corner of it.
        tile = tile.resize((tile_size, tile_size))
    if fit_thickness is not None:
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


AMBER = (196, 148, 78, 255)
AMBER_TRIM = (230, 190, 110, 255)
DARK_WOOD = (74, 52, 36, 255)


def round_table(path, size=72):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([4, 8, size - 4, size - 4], fill=(20, 16, 12, 130))
    d.ellipse([2, 2, size - 6, size - 8], fill=DARK_WOOD, outline=AMBER_TRIM, width=3)
    d.ellipse([size // 2 - 6, size // 2 - 9, size // 2 + 6, size // 2 + 3], fill=(90, 60, 30, 180))
    img.save(path)


def sci_chair(path, size=28):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([2, size - 8, size - 2, size - 2], fill=(0, 0, 0, 60))
    d.rounded_rectangle([4, 4, size - 4, size - 6], radius=5, fill=(60, 42, 30, 255), outline=AMBER_TRIM, width=2)
    img.save(path)


def sci_couch(path, w=96, h=40):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([4, h - 10, w - 4, h - 2], fill=(0, 0, 0, 60))
    d.rounded_rectangle([2, 6, w - 2, h - 6], radius=8, fill=(66, 46, 32, 255), outline=AMBER_TRIM, width=3)
    d.rounded_rectangle([2, 2, w - 2, 14], radius=6, fill=(80, 56, 38, 255))
    img.save(path)


def sci_counter(path, w=800, h=80):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w, h], fill=(52, 36, 24, 255))
    d.rectangle([0, 0, w, 12], fill=AMBER)
    d.rectangle([0, 12, w, 16], fill=AMBER_TRIM)
    d.rectangle([0, 0, w - 1, h - 1], outline=(28, 18, 10, 255), width=3)
    for x in range(20, w - 20, 90):
        d.rectangle([x, h - 22, x + 50, h - 10], fill=(70, 48, 30, 255), outline=AMBER_TRIM, width=1)
    img.save(path)


# --- Floor: Scifi_Panels_05 (locker/access-panel look) as main floor ---
# Source is a 4096x4096 sheet containing 4 distinct labeled panels (B-32..B-35) —
# downscale first so tiling repeats a reasonably sized cell, not one giant corner.
tile_texture(os.path.join(PACKS, "Scifi_Panels_05", "Scifi_Panels_05_basecolor.jpg"),
             os.path.join(SHIP, "floor_hall.png"), 1680, 960, tile_size=400)

# --- Floor accent near the bar: glowing-seam panel ---
tile_texture(os.path.join(PACKS, "Floor Tile 1", "PNG", "1k", "floor_tile_1_color.png"),
             os.path.join(SHIP, "floor_accent.png"), 900, 220)

# --- Walls: calm painted-wall base on all four sides ---
WALL_THICKNESS = 96
HALL_W, HALL_H = 1680, 960
TOTAL_W, TOTAL_H = HALL_W + WALL_THICKNESS * 2, HALL_H + WALL_THICKNESS * 2
PAINTED_WALL = os.path.join(PACKS, "Scifi_PaintedWall_01", "Scifi_PaintedWall_01_basecolor.jpg")

tile_texture(PAINTED_WALL, os.path.join(SHIP, "wall_top.png"), TOTAL_W, WALL_THICKNESS, fit_thickness=WALL_THICKNESS)
tile_texture(PAINTED_WALL, os.path.join(SHIP, "wall_bottom.png"), TOTAL_W, WALL_THICKNESS, fit_thickness=WALL_THICKNESS)
tile_texture(PAINTED_WALL, os.path.join(SHIP, "wall_left.png"), WALL_THICKNESS, TOTAL_H, fit_thickness=WALL_THICKNESS, rotate90=True)
tile_texture(PAINTED_WALL, os.path.join(SHIP, "wall_right.png"), WALL_THICKNESS, TOTAL_H, fit_thickness=WALL_THICKNESS, rotate90=True)

# --- Bar screen: one big non-tiled holo-panel mounted on the wall behind the counter ---
screen_src = os.path.join(PACKS, "Sci-fi Panel 1", "PNG", "1k", "scifi_panel_1_color_1k.png")
screen_img = Image.open(screen_src).convert("RGBA").resize((700, 350))
screen_img.save(os.path.join(SHIP, "bar_screen.png"))
print("bar_screen saved")

# --- Door & elevator (unchanged, already good) ---
door_src = os.path.join(SCIFI_ARCH, "scifi_architecture_door_airlock_round_01.png")
Image.open(door_src).convert("RGBA").resize((96, 96)).save(os.path.join(SHIP, "door_airlock.png"))
print("door_airlock saved")

elevator_src = os.path.join(PACKS, "Hotel-Labratory-Sprites", "Sprite Sheets", "Elevator", "elevator-Sheet.png")
elevator_img = Image.open(elevator_src).convert("RGBA")
frame_w = elevator_img.width // 6
crop_frame(elevator_src, os.path.join(SHIP, "elevator_door.png"),
           (elevator_img.width - frame_w, 0, elevator_img.width, elevator_img.height))

# --- Service robot (unchanged) ---
robot_src = os.path.join(PACKS, "TopView_Robot_Asset_Pack", "TopView_Robot_Asset_Pack", "Player.png")
robot_img = Image.open(robot_src).convert("RGBA")
robot_single = robot_img.crop((0, 0, robot_img.width // 2, robot_img.height))
robot_single.save(os.path.join(SHIP, "_robot_raw.png"))
recolor_hue(os.path.join(SHIP, "_robot_raw.png"), os.path.join(SHIP, "service_robot.png"), hue_shift=90)
os.remove(os.path.join(SHIP, "_robot_raw.png"))

# --- Procedural furniture, warm amber/wood palette (contrasts with the cool floor/walls) ---
round_table(os.path.join(SHIP, "table_round.png"))
sci_chair(os.path.join(SHIP, "chair.png"))
sci_couch(os.path.join(SHIP, "couch.png"))
sci_counter(os.path.join(SHIP, "bar_counter_scifi.png"))

# --- Decor pieces copied straight from the Demo furniture pack ---
DECOR_ITEMS = {
    87: "couch_curved_a.png",
    88: "couch_curved_b.png",
    523: "holo_table.png",
    145: "owner_chair.png",
    744: "armor_statue.png",
    1034: "server_rack.png",
    639: "monitor_wall.png",
    155: "weapon_rack.png",
    514: "bottle_shelf.png",
    475: "curiosity_shelf.png",
    746: "room_divider.png",
    591: "grandfather_clock.png",
    1000: "lounge_sofa.png",
    12: "bench.png",
    482: "vase.png",
    1038: "tech_pod.png",
    993: "spider_drone.png",
}
for num, name in DECOR_ITEMS.items():
    src = os.path.join(PACKS, "Demo", f"Furniture ({num}).png")
    dst = os.path.join(DECOR, name)
    shutil.copyfile(src, dst)
    print("copied decor", name)

print("Done.")
