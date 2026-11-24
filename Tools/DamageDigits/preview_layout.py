# 用与 Unity 组件相同的排版公式合成样张，用于核对字库与排版
import json, os, sys
from PIL import Image
D = sys.argv[1]; OUT = sys.argv[2]
m = json.load(open(os.path.join(D, "DamageDigits.json"), encoding="utf-8"))
m["glyphs"] = {g["ch"]: g for g in m["glyphs"]}
def compose(text, style, size):
    atlas = Image.open(os.path.join(D, f"DamageDigits_{style}.png"))
    k = size / m['renderSize']; u = size / 10; H = m['cellHeight']
    total = sum(m['glyphs'][c]['advance'] for c in text) - m['tracking']
    W = int(total * u + 160 * k + 40); Hc = int(H * k + 40); img = Image.new('RGBA', (W, Hc), (0, 0, 0, 0))
    cx, cy = W / 2, Hc / 2
    for layer in (0, 1):
        pen = 0.0
        for c in text:
            g = m['glyphs'][c]
            cell = atlas.crop((g['x'], layer * H, g['x'] + g['w'], layer * H + H)).resize((max(1, int(g['w'] * k)), max(1, int(H * k))), Image.LANCZOS)
            # 组件公式：原点相对数字中心 x = u*(pen - total/2) - shear*u*5, y(向下) = -5u
            ox = cx + u * (pen - total / 2) - m['shear'] * u * 5; oy = cy - 5 * u
            img.alpha_composite(cell, (int(round(ox - g['originX'] * k)), int(round(oy - g['originY'] * k))))
            pen += g['advance']
    return img
rows = [("1,280", "normal", 46), ("+396", "light", 36), ("3,960", "heavy", 58), ("720", "kill", 64), ("860", "hurt", 40), ("+1,500", "heal", 40)]
attrs = ["physical", "fire", "water", "wood", "earth", "light_attr", "dark", "poison"]
sheet = Image.new('RGBA', (2600, 330), (52, 48, 70, 255))
x = 20
for t, s, z in rows:
    im = compose(t, s, z); sheet.alpha_composite(im, (x, 70 - im.height // 2 + 20)); x += im.width + 10
x = 10
for a in attrs:
    im = compose("2,408", a, 62); sheet.alpha_composite(im, (x, 230 - im.height // 2)); x += im.width - 20
sheet.convert('RGB').save(OUT)
