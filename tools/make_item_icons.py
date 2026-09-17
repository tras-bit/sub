#!/usr/bin/env python3
"""
make_item_icons.py — иконки предметов для инвентаря (PIL, 96×96, стиль «терминал + тактика»).

Зачем: инвентарь был текстовым (одни названия). Теперь у каждого предмета есть иконка,
которую грузит UI/ItemIcons.cs из Resources/icons/<id с _ вместо .>.png.

Как рисуем: у предмета определяется «архетип» по ключевым словам в id (rifle, ammo, can,
medkit, boots, key, furnace…), дальше архетип рисуется примитивами. Рамка и подсветка —
по редкости (Common серый → Anomalous фиолетовый), как цвет тира в Rust.

Запуск: python3 tools/make_item_icons.py [--check]
"""
import math
import os
import re
import sys
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, ".."))
ITEMS_CS = os.path.join(ROOT, "UnityProject", "Assets", "Subsistence", "Scripts", "Core", "Items.cs")
OUT = os.path.join(ROOT, "UnityProject", "Assets", "Subsistence", "Resources", "icons")
S = 96                      # размер иконки
PAD = 4

RARITY = {
    "Common":    ((74, 82, 74),    (150, 160, 150)),
    "Uncommon":  ((38, 74, 52),    (110, 220, 140)),
    "Rare":      ((34, 58, 92),    (110, 175, 255)),
    "VeryRare":  ((74, 46, 96),    (198, 140, 255)),
    "Military":  ((88, 62, 24),    (255, 200, 96)),
    "Anomalous": ((86, 30, 64),    (255, 120, 200)),
}
BG = (18, 22, 20)
FG = (206, 214, 206)
DIM = (128, 138, 128)
WOOD = (122, 86, 48)
METAL = (150, 156, 160)
DARK = (58, 62, 64)
RUBBER = (44, 46, 48)
CLOTH = (96, 96, 88)
GREEN = (120, 200, 120)
YELLOW = (232, 200, 90)
RED = (206, 96, 80)
GLASS = (150, 200, 210)
STEEL = (176, 182, 188)


# ------------------------------------------------------------------ примитивы
def rr(d, box, r, fill=None, outline=None, w=2):
    d.rounded_rectangle(box, radius=r, fill=fill, outline=outline, width=w)


def bar(d, box, col):
    d.rectangle(box, fill=col)


def circ(d, cx, cy, r, fill=None, outline=None, w=2):
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=fill, outline=outline, width=w)


def poly(d, pts, fill=None, outline=None):
    d.polygon(pts, fill=fill, outline=outline)


# ------------------------------------------------------------------ архетипы
def draw_rifle(d, c=FG):
    bar(d, [10, 46, 74, 52], c)                         # ствольная коробка
    bar(d, [74, 47, 88, 50], c)                         # ствол
    bar(d, [22, 52, 30, 66], DARK)                      # магазин
    bar(d, [14, 52, 22, 62], WOOD)                      # рукоять
    poly(d, [(10, 46), (4, 44), (4, 34), (12, 34)], fill=WOOD)   # приклад
    bar(d, [40, 42, 56, 46], DARK)                      # планка


def draw_pistol(d, c=FG):
    bar(d, [22, 40, 68, 47], c)
    poly(d, [(26, 47), (40, 47), (36, 66), (24, 66)], fill=DARK)
    bar(d, [34, 50, 40, 58], METAL)


def draw_shotgun(d, c=FG):
    bar(d, [12, 44, 82, 50], c)
    bar(d, [34, 50, 58, 56], WOOD)                       # цевьё
    poly(d, [(12, 44), (2, 40), (6, 30), (16, 32)], fill=WOOD)
    bar(d, [26, 50, 32, 60], DARK)


def draw_smg(d, c=FG):
    bar(d, [16, 44, 66, 51], c)
    bar(d, [30, 51, 40, 70], DARK)
    bar(d, [6, 46, 16, 49], DARK)
    poly(d, [(66, 44), (76, 42), (76, 34), (66, 36)], fill=DARK)


def draw_lmg(d, c=FG):
    draw_rifle(d, c)
    rr(d, [52, 34, 68, 50], 3, fill=DARK)              # короб с лентой
    circ(d, 60, 42, 6, fill=None, outline=DIM, w=2)


def draw_launcher(d, c=FG):
    bar(d, [8, 40, 84, 54], c)
    circ(d, 16, 47, 9, fill=None, outline=RED, w=3)      # боеголовка
    poly(d, [(84, 40), (92, 44), (92, 50), (84, 54)], fill=DARK)


def draw_grenade(d, c=GREEN):
    circ(d, 48, 55, 17, fill=None, outline=c, w=3)
    for i in range(4):
        x = 36 + i * 8
        d.line([x, 43, x, 67], fill=c, width=1)
    bar(d, [44, 30, 52, 38], METAL)
    circ(d, 56, 32, 4, fill=None, outline=YELLOW, w=2)


def draw_c4(d, c=YELLOW):
    for i, x in enumerate((16, 40, 64)):
        rr(d, [x, 42, x + 20, 70], 3, fill=(58, 54, 40), outline=c, w=2)
    bar(d, [16, 52, 84, 60], DARK)
    bar(d, [46, 30, 58, 42], METAL)
    circ(d, 52, 34, 5, fill=RED)


def draw_ammo(d, c=YELLOW):
    for i in range(4):
        x = 20 + i * 14
        poly(d, [(x, 70), (x, 40), (x + 5, 30), (x + 10, 40), (x + 10, 70)], fill=c)
    bar(d, [14, 70, 82, 76], DARK)


def draw_magazine(d, c=FG):
    rr(d, [34, 26, 62, 74], 4, fill=DARK, outline=c, w=2)
    bar(d, [34, 38, 62, 42], DIM)


def draw_can(d, c=FG):
    rr(d, [26, 28, 70, 74], 5, fill=(96, 104, 100), outline=c, w=2)
    bar(d, [26, 46, 70, 56], (168, 92, 72))
    circ(d, 48, 32, 17, fill=None, outline=DIM, w=1)


def draw_bottle(d, c=GLASS):
    rr(d, [40, 26, 56, 38], 2, fill=c)                   # горлышко
    rr(d, [30, 38, 66, 76], 6, fill=(60, 84, 78), outline=c, w=2)
    bar(d, [30, 52, 66, 62], c)


def draw_food(d, c=(214, 160, 96)):
    circ(d, 48, 50, 22, fill=(70, 60, 46), outline=c, w=2)
    circ(d, 48, 50, 11, fill=None, outline=c, w=1)
    bar(d, [46, 24, 50, 30], DARK)


def draw_medkit(d, c=RED):
    rr(d, [18, 32, 78, 72], 6, fill=(56, 58, 58), outline=c, w=2)
    bar(d, [42, 42, 54, 62], c)
    bar(d, [32, 46, 64, 58], c)


def draw_syringe(d, c=GREEN):
    bar(d, [26, 46, 66, 52], (200, 206, 206))
    bar(d, [20, 50, 26, 54], STEEL)
    bar(d, [66, 48, 80, 50], STEEL)
    bar(d, [40, 52, 58, 60], c)


def draw_helmet(d, c=FG):
    d.pieslice([22, 26, 74, 78], 180, 360, fill=(70, 74, 76), outline=c, width=2)
    bar(d, [22, 50, 74, 56], c)
    bar(d, [30, 34, 66, 38], DIM)


def draw_facemask(d, c=STEEL):
    d.pieslice([24, 26, 72, 74], 180, 360, fill=(84, 88, 92), outline=c, width=2)
    circ(d, 38, 50, 5, fill=DARK)
    circ(d, 58, 50, 5, fill=DARK)
    for i in range(5):
        y = 58 + i * 3
        d.line([44, y, 52, y], fill=DIM, width=1)


def draw_vest(d, c=FG):
    poly(d, [(30, 26), (42, 26), (48, 34), (54, 26), (66, 26), (70, 72), (26, 72)], fill=(66, 70, 72), outline=c)
    bar(d, [30, 48, 66, 54], DIM)


def draw_jacket(d, c=WOOD):
    poly(d, [(32, 26), (44, 26), (48, 32), (52, 26), (64, 26), (70, 70), (26, 70)], fill=(84, 74, 58), outline=c)
    d.line([48, 32, 48, 70], fill=DIM, width=2)


def draw_pants(d, c=WOOD):
    poly(d, [(30, 26), (66, 26), (66, 70), (52, 70), (48, 44), (44, 70), (30, 70)], fill=(84, 74, 58), outline=c)


def draw_boots(d, c=RUBBER):
    for x in (22, 52):
        rr(d, [x, 34, x + 22, 68], 3, fill=c, outline=FG, w=2)
        bar(d, [x - 2, 68, x + 24, 74], DARK)
    bar(d, [22, 40, 74, 44], (196, 176, 96))


def draw_gloves(d, c=RUBBER):
    for x in (20, 52):
        rr(d, [x, 40, x + 24, 68], 4, fill=c, outline=FG, w=2)
        for i in range(3):
            bar(d, [x + 3 + i * 7, 30, x + 8 + i * 7, 42], c)
    bar(d, [20, 66, 76, 72], (196, 176, 96))


def draw_mask_gas(d, c=FG):
    circ(d, 48, 50, 20, fill=(70, 76, 74), outline=c, w=2)
    circ(d, 34, 50, 8, fill=(52, 56, 56), outline=c, w=2)
    circ(d, 62, 50, 8, fill=(52, 56, 56), outline=c, w=2)
    bar(d, [44, 66, 52, 74], DARK)


def draw_key(d, c=YELLOW):
    circ(d, 30, 40, 11, fill=None, outline=c, w=4)
    bar(d, [40, 46, 76, 52], c)
    bar(d, [64, 52, 70, 62], c)
    bar(d, [74, 52, 80, 60], c)


def draw_lock(d, c=STEEL):
    rr(d, [26, 44, 70, 76], 5, fill=(64, 68, 70), outline=c, w=2)
    d.arc([34, 26, 62, 54], 180, 360, fill=c, width=5)
    circ(d, 48, 58, 5, fill=DARK)


def draw_door(d, c=WOOD):
    rr(d, [28, 20, 68, 78], 3, fill=(86, 66, 44), outline=c, w=2)
    bar(d, [36, 30, 60, 44], None) if False else bar(d, [36, 30, 60, 44], (70, 54, 36))
    circ(d, 62, 56, 3, fill=YELLOW)


def draw_furnace(d, c=(150, 92, 60)):
    rr(d, [22, 30, 74, 76], 4, fill=(72, 62, 58), outline=c, w=2)
    circ(d, 48, 52, 10, fill=(230, 140, 60))
    bar(d, [34, 22, 62, 30], (86, 84, 82))


def draw_workbench(d, c=WOOD):
    bar(d, [14, 44, 82, 52], c)
    bar(d, [20, 52, 26, 78], (84, 62, 38))
    bar(d, [70, 52, 76, 78], (84, 62, 38))
    bar(d, [26, 56, 70, 60], (96, 78, 52))
    bar(d, [40, 32, 56, 44], METAL)


def draw_tool(d, c=STEEL):
    bar(d, [40, 24, 48, 66], WOOD)
    poly(d, [(36, 66), (52, 66), (56, 78), (32, 78)], fill=c)


def draw_axe(d, c=STEEL):
    bar(d, [44, 26, 52, 74], WOOD)
    poly(d, [(52, 28), (74, 34), (74, 50), (52, 46)], fill=c)


def draw_pickaxe(d, c=STEEL):
    bar(d, [44, 28, 52, 76], WOOD)
    d.arc([24, 20, 72, 52], 200, 340, fill=c, width=6)


def draw_hammer(d, c=STEEL):
    bar(d, [44, 34, 52, 76], WOOD)
    rr(d, [28, 22, 68, 40], 3, fill=c)


def draw_bow(d, c=WOOD):
    d.arc([24, 20, 72, 78], 120, 240, fill=c, width=5)
    d.line([36, 24, 36, 74], fill=DIM, width=2)
    bar(d, [36, 46, 74, 50], (200, 204, 200))


def draw_arrow(d, c=WOOD):
    d.line([24, 70, 68, 30], fill=c, width=4)
    poly(d, [(68, 30), (72, 40), (58, 34)], fill=STEEL)
    poly(d, [(26, 62), (18, 74), (32, 72)], fill=(196, 120, 96))


def draw_spear(d, c=WOOD):
    d.line([22, 76, 66, 32], fill=c, width=5)
    poly(d, [(66, 32), (80, 20), (70, 42)], fill=STEEL)


def draw_torch(d, c=(230, 150, 60)):
    bar(d, [44, 44, 52, 78], WOOD)
    poly(d, [(36, 44), (60, 44), (48, 22)], fill=c)


def draw_lantern(d, c=YELLOW):
    rr(d, [32, 34, 64, 70], 4, fill=(88, 78, 50), outline=c, w=2)
    circ(d, 48, 52, 9, fill=(250, 220, 140))
    d.arc([40, 22, 56, 40], 180, 360, fill=STEEL, width=3)


def draw_flashlight(d, c=STEEL):
    rr(d, [30, 40, 62, 66], 4, fill=(70, 74, 76), outline=c, w=2)
    poly(d, [(62, 40), (82, 34), (82, 72), (62, 66)], fill=(238, 226, 170))


def draw_camera(d, c=FG):
    rr(d, [20, 36, 76, 70], 5, fill=(58, 60, 62), outline=c, w=2)
    circ(d, 48, 53, 12, fill=(40, 44, 46), outline=c, w=2)
    bar(d, [28, 30, 44, 38], DARK)


def draw_keycard(d, c=GREEN):
    rr(d, [24, 32, 72, 68], 4, fill=(50, 54, 54), outline=c, w=2)
    bar(d, [30, 38, 50, 46], c)
    for i in range(3):
        bar(d, [30, 52 + i * 5, 66, 54 + i * 5], DIM)


def draw_mushroom(d, c=(150, 220, 140)):
    d.pieslice([26, 30, 70, 62], 180, 360, fill=c)
    bar(d, [44, 46, 52, 72], (216, 214, 200))


def draw_resource(d, c=(150, 140, 120)):
    for i in range(3):
        poly(d, [(24 + i * 16, 70), (32 + i * 16, 42), (40 + i * 16, 70)], fill=c)


def draw_ore(d, c=(160, 150, 150)):
    draw_resource(d, (110, 106, 100))
    for i in range(3):
        circ(d, 32 + i * 16, 56, 3, fill=c)


def draw_wood(d, c=WOOD):
    for i in range(3):
        x = 20 + i * 20
        rr(d, [x, 34, x + 16, 74], 4, fill=c, outline=(88, 62, 34), w=2)
        circ(d, x + 8, 40, 5, fill=(150, 112, 66))


def draw_cloth(d, c=CLOTH):
    rr(d, [22, 34, 74, 72], 5, fill=c, outline=DIM, w=2)
    d.line([30, 44, 66, 62], fill=(140, 140, 130), width=2)
    d.line([66, 44, 30, 62], fill=(140, 140, 130), width=2)


def draw_scrap(d, c=(160, 160, 150)):
    poly(d, [(20, 66), (34, 40), (52, 52), (44, 72)], fill=(96, 98, 96), outline=c)
    poly(d, [(52, 68), (62, 44), (80, 56), (74, 74)], fill=(78, 80, 80), outline=c)


def draw_powder(d, c=(190, 180, 160)):
    rc = rr(d, [26, 38, 70, 74], 5, fill=(70, 66, 60), outline=c, w=2)
    for i in range(3):
        circ(d, 38 + i * 12, 56, 5, fill=(120, 114, 104))


def draw_fuel(d, c=(200, 150, 70)):
    rr(d, [26, 32, 70, 76], 4, fill=(84, 66, 40), outline=c, w=2)
    bar(d, [40, 24, 56, 34], DARK)
    bar(d, [26, 50, 70, 56], c)


def draw_explosive_rocket(d, c=RED):
    rr(d, [26, 42, 68, 56], 4, fill=(80, 74, 70), outline=c, w=2)
    poly(d, [(26, 42), (14, 49), (26, 56)], fill=c)
    poly(d, [(68, 42), (82, 49), (68, 56)], fill=DARK)


def draw_scope(d, c=FG):
    rr(d, [16, 44, 80, 56], 6, fill=(58, 62, 64), outline=c, w=2)
    circ(d, 30, 50, 9, fill=(40, 46, 48), outline=c, w=2)
    bar(d, [40, 40, 56, 44], DARK)


def draw_silencer(d, c=DARK):
    rr(d, [24, 44, 72, 58], 7, fill=(52, 54, 56), outline=c, w=2)
    for i in range(4):
        d.line([32 + i * 10, 46, 32 + i * 10, 56], fill=DIM, width=1)


def draw_laser(d, c=RED):
    rr(d, [30, 44, 62, 60], 4, fill=(56, 58, 60), outline=c, w=2)
    circ(d, 66, 52, 4, fill=RED)


def draw_melee(d, c=STEEL):
    poly(d, [(30, 68), (60, 26), (70, 34), (40, 74)], fill=c)
    bar(d, [26, 62, 40, 76], WOOD)


def draw_bag(d, c=(120, 96, 60)):
    rr(d, [24, 40, 72, 76], 6, fill=c, outline=(84, 66, 40), w=2)
    d.arc([36, 24, 60, 48], 180, 360, fill=DIM, width=4)


def draw_box(d, c=WOOD):
    rr(d, [22, 36, 74, 74], 4, fill=(104, 78, 46), outline=c, w=2)
    d.line([22, 36, 74, 74], fill=(140, 110, 70), width=2)
    d.line([74, 36, 22, 74], fill=(140, 110, 70), width=2)


def draw_turret(d, c=STEEL):
    circ(d, 48, 56, 14, fill=(70, 74, 76), outline=c, w=2)
    bar(d, [60, 50, 86, 56], c)
    bar(d, [34, 40, 62, 48], DARK)


def draw_generator(d, c=(160, 160, 150)):
    bar(d, [44, 20, 52, 44], STEEL)
    for i in range(3):
        d.line([52, 22 + i * 6, 70 - i * 6, 34 + i * 6], fill=c, width=3)
    bar(d, [34, 44, 62, 46], c)
    rr(d, [26, 46, 70, 76], 4, fill=(70, 72, 70), outline=DIM, w=2)


def draw_trap(d, c=STEEL):
    for i in range(7):
        x = 24 + i * 8
        poly(d, [(x, 70), (x + 4, 30), (x + 8, 70)], fill=c)
    bar(d, [20, 66, 76, 74], DARK)


def draw_wire(d, c=(206, 150, 90)):
    circ(d, 48, 50, 22, fill=None, outline=c, w=3)
    circ(d, 48, 50, 12, fill=None, outline=c, w=3)
    d.line([26, 50, 70, 50], fill=c, width=3)


def draw_pipe(d, c=(140, 146, 150)):
    for y in (38, 58):
        rr(d, [20, y, 76, y + 12], 4, fill=c, outline=DARK, w=2)
        circ(d, 26, y + 6, 3, fill=DARK)


def draw_gear(d, c=STEEL):
    circ(d, 48, 52, 16, fill=None, outline=c, w=5)
    circ(d, 48, 52, 7, fill=None, outline=c, w=3)
    for i in range(6):
        a = i * math.pi / 3
        x, y = 48 + math.cos(a) * 22, 52 + math.sin(a) * 22
        circ(d, x, y, 4, fill=c)


def draw_flare(d, c=RED):
    rr(d, [42, 34, 54, 74], 3, fill=(90, 86, 80), outline=FG, w=2)
    circ(d, 48, 28, 7, fill=c)


def draw_glow(d, c=(150, 230, 130)):
    circ(d, 48, 46, 20, fill=(60, 92, 60), outline=c, w=3)
    for i in range(4):
        a = i * math.pi / 2 + 0.4
        d.line([48 + math.cos(a) * 22, 46 + math.sin(a) * 22,
                48 + math.cos(a) * 30, 46 + math.sin(a) * 30], fill=c, width=2)
    bar(d, [44, 64, 52, 78], (216, 214, 200))


def draw_flamethrower(d, c=FG):
    bar(d, [18, 44, 62, 52], c)
    bar(d, [40, 30, 62, 44], DARK)                      # баллоны
    circ(d, 74, 48, 7, fill=(230, 150, 60))
    poly(d, [(14, 44), (4, 40), (6, 52), (14, 52)], fill=WOOD)


def draw_binoculars(d, c=STEEL):
    for x in (22, 54):
        rr(d, [x, 36, x + 20, 70], 5, fill=(58, 62, 64), outline=c, w=2)
        circ(d, x + 10, 68, 6, fill=GLASS)
    bar(d, [42, 44, 54, 52], DARK)


def draw_plan(d, c=(220, 210, 170)):
    rr(d, [24, 22, 74, 76], 3, fill=(198, 190, 160), outline=c, w=2)
    d.line([30, 34, 68, 34], fill=(80, 110, 150), width=2)
    d.line([30, 46, 68, 46], fill=(80, 110, 150), width=2)
    d.line([30, 58, 56, 58], fill=(150, 90, 80), width=2)


def draw_token(d, c=YELLOW):
    circ(d, 48, 50, 22, fill=None, outline=c, w=3)
    circ(d, 48, 50, 14, fill=(60, 54, 34), outline=c, w=2)
    bar(d, [44, 44, 52, 56], c)


def draw_chocolate(d, c=(120, 78, 46)):
    rr(d, [22, 32, 74, 72], 4, fill=c, outline=(160, 120, 80), w=2)
    for i in range(3):
        d.line([22, 46 + i * 10, 74, 46 + i * 10], fill=(200, 160, 110), width=2)
    bar(d, [22, 32, 74, 40], (198, 190, 170))


def draw_sewing(d, c=STEEL):
    circ(d, 40, 54, 14, fill=None, outline=c, w=3)
    d.line([46, 44, 74, 22], fill=c, width=3)
    circ(d, 74, 22, 5, fill=(206, 96, 80))
    for i in range(3):
        d.line([26 + i * 8, 66, 30 + i * 8, 74], fill=DIM, width=2)


def draw_rock(d, c=(140, 140, 136)):
    poly(d, [(22, 70), (30, 40), (52, 32), (74, 50), (66, 72)], fill=(96, 96, 92), outline=c)
    d.line([40, 48, 54, 44], fill=(140, 140, 136), width=2)


def draw_attire(d, c=(120, 96, 70)):
    poly(d, [(30, 30), (44, 30), (48, 40), (52, 30), (66, 30), (68, 70), (28, 70)], fill=c, outline=(90, 70, 50))
    for i in range(3):
        d.line([32 + i * 12, 44, 32 + i * 12, 66], fill=(70, 54, 38), width=3)


def draw_generic(d, c=FG):
    rr(d, [26, 34, 70, 74], 5, fill=(52, 56, 56), outline=c, w=2)
    circ(d, 48, 54, 10, fill=None, outline=DIM, w=2)


ARCH = {
    "flamethrower": draw_flamethrower, "binoculars": draw_binoculars, "planner": draw_plan,
    "building": draw_plan, "token": draw_token, "chocolate": draw_chocolate, "sewing": draw_sewing,
    "rock": draw_rock, "attire": draw_attire, "helterneck": draw_attire, "hide": draw_attire,
    "rifle": draw_rifle, "pistol": draw_pistol, "revolver": draw_pistol, "shotgun": draw_shotgun,
    "smg": draw_smg, "lmg": draw_lmg, "hmlmg": draw_lmg, "minigun": draw_lmg, "rocket": draw_launcher,
    "grenade": draw_grenade, "beancan": draw_grenade, "molotov": draw_bottle, "c4": draw_c4,
    "explosive": draw_c4, "satchel": draw_c4, "mine": draw_trap, "ammo": draw_ammo, "magazine": draw_magazine,
    "can": draw_can, "water": draw_bottle, "bottle": draw_bottle, "juice": draw_bottle, "flask": draw_bottle,
    "food": draw_food, "apple": draw_food, "berry": draw_food, "meat": draw_food, "fish": draw_food,
    "medkit": draw_medkit, "bandage": draw_cloth, "syringe": draw_syringe, "antidote": draw_syringe,
    "helmet": draw_helmet, "facemask": draw_facemask, "vest": draw_vest, "plate": draw_vest,
    "jacket": draw_jacket, "shirt": draw_jacket, "coat": draw_jacket, "suit": draw_jacket, "kilt": draw_pants,
    "pants": draw_pants, "boots": draw_boots, "shoes": draw_boots, "flippers": draw_boots,
    "gloves": draw_gloves, "respirator": draw_mask_gas, "hazmat": draw_mask_gas, "mask": draw_mask_gas,
    "nightvision": draw_scope, "goggles": draw_scope, "key": draw_key, "lock": draw_lock, "door": draw_door,
    "furnace": draw_furnace, "workbench": draw_workbench, "repair": draw_workbench, "research": draw_workbench,
    "hatchet": draw_axe, "axe": draw_axe, "pickaxe": draw_pickaxe, "hammer": draw_hammer, "mallet": draw_hammer,
    "wrench": draw_tool, "sledge": draw_hammer, "shovel": draw_pickaxe, "hoe": draw_pickaxe,
    "weapon.mod": draw_scope, "holosight": draw_scope, "scope": draw_scope, "silencer": draw_silencer,
    "muzzle": draw_silencer, "laser": draw_laser, "extendedmags": draw_magazine, "bow": draw_bow,
    "arrow": draw_arrow, "spear": draw_spear, "knife": draw_melee, "machete": draw_melee, "cleaver": draw_melee,
    "baton": draw_melee, "sword": draw_melee, "torch": draw_torch, "lantern": draw_lantern,
    "flashlight": draw_flashlight, "lamp": draw_lantern, "camera": draw_camera, "keycard": draw_keycard,
    "mushroom": draw_mushroom, "wood": draw_wood, "log": draw_wood, "cloth": draw_cloth, "leather": draw_cloth,
    "rope": draw_wire, "tape": draw_cloth, "scrap": draw_scrap, "refined": draw_resource, "metal": draw_resource,
    "fragments": draw_scrap, "sulfur": draw_ore, "ore": draw_ore, "stone": draw_resource, "stones": draw_resource,
    "powder": draw_powder, "fuel": draw_fuel, "charcoal": draw_powder, "shell": draw_ammo,
    "crate": draw_box, "box": draw_box, "bag": draw_bag, "sleeping": draw_bag, "bed": draw_bag,
    "turret": draw_turret, "sam": draw_turret, "generator": draw_generator, "turbine": draw_generator,
    "trap": draw_trap, "spikes": draw_trap, "barricade": draw_pipe, "gate": draw_pipe, "sign": draw_camera,
    "flare": draw_flare, "glow": draw_glow, "candle": draw_flare, "fuse": draw_gear, "geiger": draw_gear,
    "reactor": draw_flare, "rod": draw_flare, "anomaly": draw_glow, "shard": draw_glow,
    "membrane": draw_glow, "flesh": draw_food, "bone": draw_melee, "hive": draw_glow,
    "scooter": draw_gear, "cart": draw_box, "minecart": draw_box, "elevator": draw_gear,
    "purifier": draw_gear, "vending": draw_can, "cupboard": draw_box, "chlorine": draw_bottle,
    "oxygen": draw_bottle, "duct": draw_cloth, "fuse.hi": draw_gear,
}

# порядок важен: длинные ключи раньше коротких
ORDER = sorted(ARCH.keys(), key=len, reverse=True)


def archetype_for(item_id):
    low = item_id.lower()
    for key in ORDER:
        if key in low:
            return ARCH[key]
    return draw_generic


# ------------------------------------------------------------------ парсинг предметов
def parse_items():
    src = open(ITEMS_CS, encoding="utf-8").read()
    out = []
    for m in re.finditer(r'\bR\("([^"]+)",\s*"([^"]*)",\s*"([^"]*)",\s*ItemCategory\.(\w+),'
                         r'\s*(\d+),\s*(\d+),\s*([\d.]+)f([^;]*)\);', src):
        iid, ru, en, cat = m.group(1), m.group(2), m.group(3), m.group(4)
        rest = m.group(8)
        rar = re.search(r'Rarity\.(\w+)', rest)
        out.append(dict(id=iid, ru=ru, en=en, cat=cat, rarity=rar.group(1) if rar else "Common"))
    return out


def font(size):
    for p in ("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
              "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"):
        if os.path.exists(p):
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()


def icon_for(item):
    img = Image.new("RGBA", (S, S), BG + (255,))
    d = ImageDraw.Draw(img)
    bg, accent = RARITY.get(item["rarity"], RARITY["Common"])

    # подложка по редкости + рамка
    d.rounded_rectangle([0, 0, S - 1, S - 1], radius=10, fill=tuple(int(x * 0.55) for x in bg) + (255,))
    d.rounded_rectangle([1, 1, S - 2, S - 2], radius=10, outline=accent + (255,), width=2)

    # сам предмет (внутреннее поле)
    inner = Image.new("RGBA", (S - 2 * PAD, S - 2 * PAD), (0, 0, 0, 0))
    di = ImageDraw.Draw(inner)
    archetype_for(item["id"])(di)
    img.alpha_composite(inner, (PAD, PAD))

    # номер/подпись не рисуем: id и так виден в тултипе — иконка должна читаться силуэтом
    if os.environ.get("ICON_DEBUG"):
        d.text((6, S - 18), item["id"][:16], font=font(9), fill=(255, 210, 60, 255))
    return img


def main():
    check = "--check" in sys.argv
    items = parse_items()
    os.makedirs(OUT, exist_ok=True)
    made = 0
    for it in items:
        name = it["id"].replace(".", "_") + ".png"
        path = os.path.join(OUT, name)
        if check and os.path.exists(path):
            continue
        icon_for(it).save(path)
        made += 1
    print(f"[icons] предметов: {len(items)} | нарисовано: {made} | папка: {OUT}")
    # сколько иконок всего в папке
    have = len([f for f in os.listdir(OUT) if f.endswith(".png")])
    print(f"[icons] в папке иконок: {have}")


if __name__ == "__main__":
    main()
