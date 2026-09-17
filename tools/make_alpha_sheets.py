#!/usr/bin/env python3
"""
make_alpha_sheets.py — листы превью для alpha 1.0.0 (ответы v2).
Собирает рендеры Blender в PNG с подписями (кириллица — DejaVuSans).

  python3 tools/make_alpha_sheets.py all
  python3 tools/make_alpha_sheets.py buildpieces | items | wb | anim
"""
import os
import sys
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
PREV = os.path.abspath(os.path.join(HERE, "..", "docs", "previews"))
BG = (14, 16, 20)
FG = (150, 235, 170)
DIM = (145, 168, 152)
ACC = (255, 205, 60)


def _font(size):
    for path in ("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
                 "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"):
        if os.path.exists(path):
            try:
                return ImageFont.truetype(path, size)
            except Exception:
                pass
    return ImageFont.load_default()


def _wrap(d, text, font, max_w, max_lines=2):
    """Переносит подпись по словам в max_lines строк, обрезает хвост «…»."""
    words, lines, cur = text.split(), [], ""
    for w in words:
        probe = (cur + " " + w).strip()
        if d.textlength(probe, font=font) <= max_w or not cur:
            cur = probe
        else:
            lines.append(cur); cur = w
            if len(lines) == max_lines: break
    if cur and len(lines) < max_lines:
        lines.append(cur)
    if len(lines) == max_lines and d.textlength(lines[-1], font=font) > max_w:
        while lines[-1] and d.textlength(lines[-1] + "…", font=font) > max_w:
            lines[-1] = lines[-1][:-1]
        lines[-1] += "…"
    return lines


def make_sheet(tiles, out, cols, title, subtitle="", cell=(400, 282), label_h=44, head=76):
    """tiles: список (имя_файла_в_previews, подпись)."""
    real = [(os.path.join(PREV, fn), cap) for fn, cap in tiles if os.path.exists(os.path.join(PREV, fn))]
    if not real:
        print(f"[sheet] пропуск {os.path.basename(out)} — нет рендеров")
        return
    rows = (len(real) + cols - 1) // cols
    W = cols * cell[0] + (cols + 1) * 12
    H = head + rows * (cell[1] + label_h + 12) + 12
    img = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(img)
    ft_title = _font(23)
    if d.textlength(title, font=ft_title) > W - 32:      # заголовок не должен вылезать
        ft_title = _font(18)
    d.text((16, 10), title, font=ft_title, fill=ACC)
    if subtitle:
        d.text((16, 42), subtitle, font=_font(14), fill=DIM)
    for i, (path, cap) in enumerate(real):
        r, c = divmod(i, cols)
        x = 12 + c * (cell[0] + 12)
        y = head + r * (cell[1] + label_h + 12)
        tile = Image.open(path).convert("RGB")
        tile.thumbnail(cell, Image.LANCZOS)
        img.paste(tile, (x + (cell[0] - tile.width) // 2, y + (cell[1] - tile.height) // 2))
        d.rectangle([x, y, x + cell[0], y + cell[1]], outline=(42, 48, 46))
        ft = _font(15)
        for li, line in enumerate(_wrap(d, cap, ft, cell[0] - 8)):
            d.text((x + 4, y + cell[1] + 4 + li * 19), line, font=ft, fill=FG)
    img.save(out)
    print(f"[sheet] {os.path.basename(out)}  ({img.width}×{img.height}, плиток {len(real)})")


# ------------------------------------------------------------------ листы

WB_MODELS = [("DD_workbench1", "T1 · верстак 1 уровня"), ("DD_workbench2", "T2 · верстак 2 уровня"),
             ("DD_workbench3", "T3 · верстак 3 уровня")]
WB_LEVELS = [("L0", "L0 — жёлтые коридоры"), ("L37", "L37 — бассейны"), ("L3", "L3 — электростанция")]

BUILDPIECES = [
    ("_bp_BD_foundation_tri.png", "Фундамент-треугольник, 3 м · скос 45°"),
    ("_bp_BD_floor_tri.png", "Перекрытие-треугольник, 3 м · скос 45°"),
    ("_bp_BD_roof.png", "Крыша: скат, конёк, стропила (была куб)"),
    ("_bp_BD_ramp.png", "Пандус: настил 3 м, подъём 1.5 м, бортики"),
    ("_bp_BD_ramp_corner.png", "Рампа-угол (9в): подъём в угол клетки + площадка"),
    ("_bp_BD_high_wall.png", "Высокая стена 3×6 м, два пояса (была 3 м)"),
    ("_bp_BD_pillar.png", "Столб 0.36×3 м: база, тело, оголовок"),
    ("_bp_BD_railing.png", "Перила (9в): перекладины и балясины"),
    ("_bp_BD_shutters.png", "Ставни (9в): рама, 9 ламелей, петли"),
]

ITEMS = [
    ("_it_IT_glow_mushroom.png", "L0 · светогриб — светит в темноте"),
    ("_it_IT_duct_tape.png", "L0 · скотч — рулон + отклеенный хвост"),
    ("_it_IT_lamp_portable.png", "L0 · лампа-переноска — решётка, крюк, кабель"),
    ("_it_IT_respirator.png", "L0 · респиратор — два фильтра, клапан, ремни"),
    ("_it_IT_diving_mask.png", "L37 · маска ныряльщика — стекло, ремень, зажим"),
    ("_it_IT_chlorine.png", "L37 · хлорка — бутылка, этикетка, ручка"),
    ("_it_IT_flippers.png", "L37 · ласты — перо, рёбра, ремешки"),
    ("_it_IT_oxygen_tank.png", "L37 · кислородный баллон — вентиль, манометр"),
    ("_it_IT_rubber_gloves.png", "L3 · диэлектрические перчатки — пара"),
    ("_it_IT_fuse_hi.png", "L3 · силовой предохранитель — латунные колпачки"),
    ("_it_IT_boots_rubber.png", "L3 · резиновые сапоги — пара, отвороты"),
    ("_it_IT_wrench_insulated.png", "L3 · изолированный ключ — 0.42 м"),
]

ANIM = [("MN_whisperer", "Улыбающийся? нет — L0 Шептун: руки до колен"),
        ("MN_drowned", "L37 Утопленник: раздутый, вода из пасти"),
        ("MN_spark", "L3 Искровик: обгоревший, бьёт током")]
PHASES = ["покой (дыхание)", "шаг: крен влево", "шаг: крен вправо", "смерть (заваливание 82°)"]


def sheets(which="all"):
    if which in ("all", "wb"):
        tiles = []
        for base, mname in WB_MODELS:
            for lvl, lname in WB_LEVELS:
                tiles.append((f"_wb_{base}_{lvl}.png", f"{mname} — {lname}"))
        make_sheet(tiles, os.path.join(PREV, "_sheet_workbench_levels.png"), 3,
                   "SUBSISTENCE 1.0.0-alpha — верстаки: 3 модели × 3 уровня = 9 вариантов (ответ 5в)",
                   "палитра подставляется по зоне, где ставишь верстак · скорость крафта ×1 / ×2 / ×3 (3в)")

    if which in ("all", "buildpieces"):
        make_sheet(BUILDPIECES, os.path.join(PREV, "_sheet_buildpieces.png"), 3,
                   "СТРОЙКА — 12 элементов стало 15 (ответы 9в и 10а): Blender-модели всем шести «кубовым»",
                   "клавиши: 1-9, 0, «-», «=», «[» рампа-угол, «]» перила, «\\» ставни · перила и ставни дешевле (15 дерева)",
                   cell=(400, 286))

    if which in ("all", "items"):
        make_sheet(ITEMS, os.path.join(PREV, "_sheet_levelitems.png"), 4,
                   "СВОИ ПРЕДМЕТЫ УРОВНЕЙ — по 4 на уровень (ответ 11а): у каждого своя модель",
                   "вещи уровней не пересекаются: L0 быт коридоров · L37 вода · L3 электричество")

    if which in ("all", "anim"):
        tiles = []
        for name, cap in ANIM:
            for i, ph in enumerate(PHASES):
                fn = f"_anim_{name}_{i}.png"
                if os.path.exists(os.path.join(PREV, fn)):
                    tiles.append((fn, f"{name} · {ph}"))
        make_sheet(tiles, os.path.join(PREV, "_sheet_monster_anim.png"), 4,
                   "АНИМАЦИЯ МОНСТРОВ (ответ 15а — как сейчас): 4 фазы процедурно, ноль анимаций-файлов",
                   "bob 7 см · крен 7° · наклон 8° (погоня ×1.4) · смерть 82° за 0.6 с",
                   cell=(300, 400))


if __name__ == "__main__":
    sheets((sys.argv[1] if len(sys.argv) > 1 else "all").lower())
