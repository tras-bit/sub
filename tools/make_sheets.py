#!/usr/bin/env python3
"""
make_sheets.py — сводные листы превью (PIL). Собирает отдельные рендеры в один PNG
с подписями, чтобы можно было одним скрином показать всё оружие / вещи уровня.

Использование:
  python3 tools/make_sheets.py weapons              # docs/previews/_sheet_weapons.png
  python3 tools/make_sheets.py level L0             # docs/previews/_sheet_L0.png
  python3 tools/make_sheets.py level L37
  python3 tools/make_sheets.py level L3
"""
import os
import sys
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
PREV = os.path.abspath(os.path.join(HERE, "..", "docs", "previews"))

WEAPONS = [
    ("W_rifle_ak", "АКМ — 7.62, штампованный ресивер, дерево"),
    ("W_rifle_m4", "M4A1 — 5.56, планка, телескоп-приклад"),
    ("W_smg_mp5", "MP5 — 9мм, трубчатый ресивер, 3-луг"),
    ("W_shotgun_pump", "Помпа — 12к, вент-планка, дерево"),
    ("W_lmg_m249", "M249 — пулемёт, кожух, сошки, короб"),
    ("W_rifle_bolt", "Болтовка — оптика, затвор, ложе"),
    ("W_rocket_launcher", "РПГ — боеголовка, сопло, дерево"),
    ("EX_grenade_f1", "Ф-1 — насечка, запал, чека"),
    ("EX_explosive_timed", "C4 — брикеты, детонатор, таймер"),
]

MONSTERS = [
    ("MN_whisperer", "L0 · Шептун — руки до колен, идёт на свет"),
    ("MN_smiler", "L0 · Улыбающийся — визитная карточка коридоров"),
    ("MN_drowned", "L37 · Утопленник — раздутый, вода из пасти"),
    ("MN_hound", "L37 · Гончая — стаями по затопленным залам"),
    ("MN_partygoer", "L37 · Патигуер — слышит всё"),
    ("MN_spark", "L3 · Искровик — обгоревший, бьёт током"),
    ("MN_skinstealer", "L3 · Свежеватель — притворяется игроком"),
    ("MN_clump", "L1 · Сгусток — медленный, липкий"),
]

LEVELS = {
    "L0": ("ЖЁЛТЫЕ КОРИДОРЫ", [
        ("LV_corridor_panel", "панель с обоями"),
        ("LV_ceiling_light", "лампа дневного света"),
        ("LV_office_chair", "офисный стул"),
        ("LV_filing_cabinet", "картотека"),
        ("LV_water_cooler", "кулер"),
        ("LV_cardboard_stack", "коробки"),
        ("LV_mop_bucket", "швабра с ведром"),
        ("LV_exit_sign", "табличка EXIT"),
        ("LV_desk_fan", "вентилятор"),
    ]),
    "L37": ("БАССЕЙНЫ", [
        ("LV_pool_ladder", "лестница в бассейн"),
        ("LV_lifebuoy", "спасательный круг"),
        ("LV_deck_chair", "лежак"),
        ("LV_pool_pump", "насос с фильтром"),
        ("LV_pipe_valve", "труба с вентилем"),
        ("LV_shower_head", "душевая лейка"),
        ("LV_wet_floor_sign", "знак «мокрый пол»"),
        ("LV_pool_tile_panel", "панель кафеля"),
        ("LV_inflatable_ring", "надувной круг"),
    ]),
    "L3": ("ЭЛЕКТРОСТАНЦИЯ", [
        ("LV_turbine_housing", "корпус турбины"),
        ("LV_control_panel", "щит управления"),
        ("LV_transformer", "трансформатор"),
        ("LV_breaker_cabinet", "шкаф рубильников"),
        ("LV_pipe_flange", "труба с фланцем"),
        ("LV_valve_wheel", "колесо-вентиль"),
        ("LV_coolant_tank", "бак охлаждения"),
        ("LV_cable_spool", "кабельный барабан"),
        ("LV_warning_sign", "знак на стойке"),
    ]),
}

BG = (18, 20, 22)
FG = (150, 235, 170)
DIM = (110, 130, 118)


def _font(size):
    for path in ("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
                 "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"):
        if os.path.exists(path):
            try:
                return ImageFont.truetype(path, size)
            except Exception:
                pass
    return ImageFont.load_default()


def sheet(items, out, cols, title, cell=(640, 340), label_h=46, dpi_note=None):
    """Собирает лист из картинок _w3_<name>.png / _lv_<name>.png."""
    tiles = []
    for name, caption in items:
        for prefix in ("_w4_", "_w3_", "_lv_"):
            p = os.path.join(PREV, prefix + name + ".png")
            if os.path.exists(p):
                tiles.append((p, name, caption))
                break
    if not tiles:
        raise SystemExit(f"[sheet] нет ни одного рендера для {out}")
    rows = (len(tiles) + cols - 1) // cols
    head = 74
    W = cols * cell[0] + (cols + 1) * 12
    H = head + rows * (cell[1] + label_h + 12) + 12
    img = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(img)
    d.text((16, 14), title, font=_font(30), fill=FG)
    if dpi_note:
        d.text((16, 48), dpi_note, font=_font(16), fill=DIM)
    for i, (path, name, caption) in enumerate(tiles):
        r, c = divmod(i, cols)
        x = 12 + c * (cell[0] + 12)
        y = head + r * (cell[1] + label_h + 12)
        tile = Image.open(path).convert("RGB")
        tile.thumbnail(cell, Image.LANCZOS)
        ox = x + (cell[0] - tile.width) // 2
        oy = y + (cell[1] - tile.height) // 2
        img.paste(tile, (ox, oy))
        d.rectangle([x, y, x + cell[0], y + cell[1]], outline=(40, 46, 44))
        d.text((x + 4, y + cell[1] + 6), name, font=_font(19), fill=FG)
        d.text((x + 4, y + cell[1] + 26), caption, font=_font(15), fill=DIM)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    img.save(out)
    print(f"[sheet] {out}  ({img.width}×{img.height}, плиток {len(tiles)})")


def main():
    what = (sys.argv[1] if len(sys.argv) > 1 else "weapons").lower()
    globals()["BG"] = BG
    if what == "weapons":
        sheet(WEAPONS, os.path.join(PREV, "_sheet_weapons.png"), 3,
              "SUBSISTENCE — ОРУЖИЕ v4 (Blender, процедурно)",
              dpi_note="булевы прорези/окна/каналы стволов · оболочки со стенками · заклёпки, оси, планки Пикатинни")
    elif what == "monsters":
        items = []
        for name, cap in MONSTERS:
            for pref in ("_m4_", "", "_lv_"):
                pth = os.path.join(PREV, pref + name + ".png")
                if os.path.exists(pth):
                    items.append((name, cap))
                    break
        # подменяем путь поиска: sheet() умеет только _w3_/_lv_, поэтому собираем вручную
        tiles = []
        for name, cap in items:
            for pref in ("_m4_", "", "_lv_"):
                pth = os.path.join(PREV, pref + name + ".png")
                if os.path.exists(pth):
                    tiles.append((pth, name, cap))
                    break
        cols, cell, label_h, head = 4, (460, 640), 46, 74
        rows = (len(tiles) + cols - 1) // cols
        W = cols * cell[0] + (cols + 1) * 12
        H = head + rows * (cell[1] + label_h + 12) + 12
        img = Image.new("RGB", (W, H), BG)
        d = ImageDraw.Draw(img)
        d.text((16, 14), "SUBSISTENCE — МОНСТРЫ: у каждого уровня свои", font=_font(30), fill=FG)
        d.text((16, 48), "L0: Улыбающийся + Шептун · L37: Гончая + Утопленник + Патигуер · L3: Свежеватель + Искровик",
               font=_font(16), fill=DIM)
        for i, (pth, name, cap) in enumerate(tiles):
            r, c = divmod(i, cols)
            x = 12 + c * (cell[0] + 12)
            y = head + r * (cell[1] + label_h + 12)
            tile = Image.open(pth).convert("RGB")
            tile.thumbnail(cell, Image.LANCZOS)
            img.paste(tile, (x + (cell[0] - tile.width) // 2, y + (cell[1] - tile.height) // 2))
            d.rectangle([x, y, x + cell[0], y + cell[1]], outline=(40, 46, 44))
            d.text((x + 4, y + cell[1] + 6), name, font=_font(19), fill=FG)
            d.text((x + 4, y + cell[1] + 26), cap, font=_font(15), fill=DIM)
        out = os.path.join(PREV, "_sheet_monsters.png")
        img.save(out)
        print(f"[sheet] {out}  ({img.width}×{img.height}, плиток {len(tiles)})")
        return
    else:
        key = (sys.argv[2] if len(sys.argv) > 2 else "L0").upper()
        title, items = LEVELS[key]
        sheet(items, os.path.join(PREV, f"_sheet_{key}.png"), 3,
              f"SUBSISTENCE — УРОВЕНЬ {key}: {title} (свои вещи)",
              dpi_note="только этот уровень: другой палитры и моделей здесь нет")


if __name__ == "__main__":
    main()
