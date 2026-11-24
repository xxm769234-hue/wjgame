# -*- coding: utf-8 -*-
"""
「裂刃体」伤害数字字库生成器（自主设计，逐笔坐标绘制，不依赖任何字体文件）
用法：python render_damage_digits.py <输出目录>
依赖：pycairo、Pillow
输出：
  DamageDigits_<style>.png   每种配色一张：上排 back 层（发光+厚度+描边），下排 face 层（正面填色+纹理+高光）
  DamageFx.png               白色粒子与徽章底图（运行时按属性色着色）
  DamageDigits.json          字形度量（所有配色共用）
"""
import cairo, json, math, os, random, sys
from PIL import Image, ImageFilter

OUT = sys.argv[1] if len(sys.argv) > 1 else "out"
os.makedirs(OUT, exist_ok=True)

# ---------- 字形：单位制，字高 10，y 向下；每个字符 = 若干折线笔画 ----------
GL = {
 '0': dict(w=7,   s=[[(1.2,2.4),(2.4,1.2),(5.8,1.2),(5.8,7.6),(4.6,8.8),(1.2,8.8),(1.2,2.4)]]),
 '1': dict(w=5.1, s=[[(1.0,2.9),(3.4,1.2),(3.4,8.8)]]),
 '2': dict(w=7,   s=[[(1.2,2.6),(2.6,1.2),(5.8,1.2),(5.8,4.2),(1.2,8.0),(1.2,8.8),(6.0,8.8)]]),
 '3': dict(w=7,   s=[[(1.2,1.2),(4.6,1.2),(5.8,2.4),(5.8,7.6),(4.6,8.8),(1.2,8.8)],[(2.6,5.0),(5.8,5.0)]]),
 '4': dict(w=7.2, s=[[(4.7,8.8),(4.7,1.2),(1.2,6.2),(6.4,6.2)]]),
 '5': dict(w=7,   s=[[(5.9,1.2),(1.4,1.2),(1.4,4.7),(4.6,4.7),(5.8,5.9),(5.8,7.6),(4.6,8.8),(1.2,8.8)]]),
 '6': dict(w=7,   s=[[(5.6,1.2),(2.4,1.2),(1.2,2.4),(1.2,7.6),(2.4,8.8),(4.6,8.8),(5.8,7.6),(5.8,5.9),(4.6,4.7),(1.2,4.7)]]),
 '7': dict(w=6.8, s=[[(1.0,1.2),(5.8,1.2),(5.8,2.8),(2.9,8.8)]]),
 '8': dict(w=7,   s=[[(2.4,1.2),(4.6,1.2),(5.8,2.4),(5.8,7.6),(4.6,8.8),(2.4,8.8),(1.2,7.6),(1.2,2.4),(2.4,1.2)],[(1.2,5.0),(5.8,5.0)]]),
 '9': dict(w=7,   s=[[(1.4,8.8),(4.6,8.8),(5.8,7.6),(5.8,2.4),(4.6,1.2),(2.4,1.2),(1.2,2.4),(1.2,4.1),(2.4,5.3),(5.8,5.3)]]),
 ',': dict(w=2.6, thin=True, s=[[(1.5,7.9),(0.9,10.1)]]),
 '+': dict(w=6.4, thin=True, s=[[(0.8,5.0),(5.6,5.0)],[(3.2,2.6),(3.2,7.4)]]),
}
CHARS = "0123456789,+"
SW, THIN_SW, SHEAR, TRACK = 2.35, 1.7, -0.2, 0.55   # 笔画宽、细笔画宽、右倾、字距（单位）
RENDER = 80                                          # 渲染字号（像素，字高 10 单位）
U = RENDER / 10.0
DEPTH = 5                                            # 侧面厚度层数
OW = 1.05                                            # 描边（单位）
PAD = 30                                             # 单元格四周留白（像素，容纳发光/厚度）
CELL_H = int(RENDER * 1.05) + PAD * 2

def hexrgb(h, a=1.0):
    h = h.lstrip('#'); return (int(h[0:2],16)/255, int(h[2:4],16)/255, int(h[4:6],16)/255, a)

STYLES = {
 'normal':   dict(fill=['#ffffff','#fff4dc','#f2d7a6'], o='#1a0d05', ex='#5a2e10'),
 'light':    dict(fill=['#ffffff','#f7ead0','#e3c99a'], o='#1a0d05', ex='#5a2e10'),
 'heavy':    dict(fill=['#fffbe0','#ffd24a','#ff8a1c'], o='#250c00', ex='#7a2c00', glow=('#ffaa28',.55)),
 'kill':     dict(fill=['#fff6d0','#ffb02a','#e2380e'], o='#200500', ex='#6e1200', glow=('#ff5a1e',.6)),
 'hurt':     dict(fill=['#ffe2dc','#ff7a64','#c8231a'], o='#220302', ex='#5e0a06'),
 'heal':     dict(fill=['#f0fff4','#8ff0ae','#2ea85c'], o='#031a0b', ex='#0d4a22'),
 # —— 克制属性：取色自《属性图标最终版》（主色 = 图标外圈，亮色 = 图标内芯）——
 'physical': dict(fill=['#ffffff','#e2e2e2','#a8a8a8'], o='#141414', ex='#3a3a3a', glow=('#e0e0e0',.45), tex='steel'),  # 物理 #b5b5b5/#cecece
 'fire':     dict(fill=['#fff3d6','#ff6a4a','#e0290b'], o='#2a0500', ex='#7a1000', glow=('#ff4a2a',.7),  tex='fire'),   # 炎 #e0290b/#ed4c37
 'water':    dict(fill=['#f0fdff','#6ce3ff','#0abaea'], o='#012034', ex='#06507a', glow=('#4fd8ff',.65), tex='ice'),    # 冰 #0abaea/#6ce3ff
 'wood':     dict(fill=['#f4ffd8','#8fde52','#55a521'], o='#0a2004', ex='#22520e', glow=('#7fd040',.55), tex='wind'),   # 风 #55a521/#6dc634
 'earth':    dict(fill=['#fff0d8','#d9823a','#a5490a'], o='#241002', ex='#5a2604', glow=('#d07030',.5),  tex='earth'),  # 岩 #a5490a/#bf6421
 'light_attr':dict(fill=['#ffffff','#ffe600','#ffbc00'], o='#3a2600', ex='#7a5200', glow=('#fff066',.9), tex='light'),  # 星 #ffbc00/#ffe600
 'dark':     dict(fill=['#e8ecff','#4f62ff','#1424c0'], o='#02061e', ex='#08105a', glow=('#3a50ff',.7),  tex='dark'),   # 暗 #051399/#2234dd（填色提亮保证可读）
 'poison':   dict(fill=['#f3e6ff','#9a4fe0','#5a14a0'], o='#10011c', ex='#2c0650', glow=('#8a3ad8',.65), tex='poison'), # 毒 #3f0077/#611da8（后端暂无此属性，预留）
}

def cell_w(ch):
    g = GL[ch]
    return int(math.ceil(g['w'] * U + abs(SHEAR) * 10 * U + PAD * 2))

def origin(ch):
    # 字形单位坐标 (0,0) 在单元格中的像素位置；右倾使底部左移，所以向右预留 |SHEAR|*10*U
    return (PAD + abs(SHEAR) * 10 * U, PAD)

def matrix(ch, dx=0.0, dy=0.0):
    ox, oy = origin(ch)
    return cairo.Matrix(xx=U, yx=0, xy=SHEAR * U, yy=U, x0=ox + (dx + SHEAR * dy) * U, y0=oy + dy * U)

def path(ctx, ch):
    for s in GL[ch]['s']:
        ctx.move_to(*s[0])
        for p in s[1:]: ctx.line_to(*p)

def stroke(ctx, ch, width, src, dx=0.0, dy=0.0):
    ctx.save(); ctx.set_matrix(matrix(ch, dx, dy)); path(ctx, ch)
    ctx.set_line_width(width * (THIN_SW / SW if GL[ch].get('thin') else 1.0))
    ctx.set_line_join(cairo.LINE_JOIN_MITER); ctx.set_miter_limit(6); ctx.set_line_cap(cairo.LINE_CAP_SQUARE)
    if isinstance(src, tuple): ctx.set_source_rgba(*src)
    else: ctx.set_source(src)
    ctx.stroke(); ctx.restore()

def surf_to_pil(s):
    s.flush(); w, h = s.get_width(), s.get_height()
    im = Image.frombuffer('RGBA', (w, h), bytes(s.get_data()), 'raw', 'BGRa', s.get_stride(), 1)
    return im.copy()

def render_back(ch, st):
    w = cell_w(ch)
    out = Image.new('RGBA', (w, CELL_H), (0, 0, 0, 0))
    if 'glow' in st:
        s = cairo.ImageSurface(cairo.FORMAT_ARGB32, w, CELL_H); c = cairo.Context(s)
        stroke(c, ch, SW + OW * 2 + 0.6, hexrgb(st['glow'][0], st['glow'][1]))
        out = Image.alpha_composite(out, surf_to_pil(s).filter(ImageFilter.GaussianBlur(RENDER * 0.16)))
    s = cairo.ImageSurface(cairo.FORMAT_ARGB32, w, CELL_H); c = cairo.Context(s)
    for i in range(DEPTH, 0, -1):
        stroke(c, ch, SW + OW * 2, hexrgb(st['o'] if i == DEPTH else st['ex']), i * 0.11, i * 0.15)
    stroke(c, ch, SW + OW * 2, hexrgb(st['o']))
    return Image.alpha_composite(out, surf_to_pil(s))

def render_face(ch, st, rng):
    w = cell_w(ch)
    s = cairo.ImageSurface(cairo.FORMAT_ARGB32, w, CELL_H); c = cairo.Context(s)
    g = cairo.LinearGradient(0, 1, 0, 9.4)
    for stop, col in zip((0, .45, 1), st['fill']): g.add_color_stop_rgba(stop, *hexrgb(col))
    stroke(c, ch, SW, g)
    # 纹理：只画在已有像素上（OPERATOR_ATOP）
    c.set_operator(cairo.OPERATOR_ATOP); H = CELL_H; R = lambda a, b: rng.uniform(a, b)
    tex = st.get('tex')
    if tex == 'fire':
        for _ in range(5):
            c.save(); c.translate(R(0, w), R(H*.35, H*.75)); c.rotate(R(-.4, .4)); c.scale(R(2, 6), R(6, 16))
            c.arc(0, 0, 1, 0, 2*math.pi); c.restore(); c.set_source_rgba(1, 1, .78, R(.25, .55)); c.fill()
        c.rectangle(0, H*.66, w, H); c.set_source_rgba(.47, 0, 0, .35); c.fill()
    elif tex == 'ice':
        c.set_source_rgba(1, 1, 1, .55); c.set_line_width(max(1.2, RENDER*.025))
        for _ in range(5):
            x0, y0 = R(0, w), R(PAD, H-PAD); a = R(0, math.pi); L = R(RENDER*.15, RENDER*.35)
            c.move_to(x0 - math.cos(a)*L, y0 - math.sin(a)*L); c.line_to(x0 + math.cos(a)*L, y0 + math.sin(a)*L); c.stroke()
        c.rectangle(0, H*.62, w, H); c.set_source_rgba(0, .2, .45, .25); c.fill()
    elif tex == 'wind':
        c.set_source_rgba(1, 1, 1, .5); c.set_line_width(max(1.5, RENDER*.035))
        for k in range(3):
            y0 = PAD + RENDER*(.25 + .25*k); c.move_to(0, y0)
            c.curve_to(w*.3, y0 - RENDER*.12, w*.6, y0 + RENDER*.12, w, y0 - RENDER*.05); c.stroke()
    elif tex == 'poison':
        for _ in range(6):
            r = R(RENDER*.04, RENDER*.1); c.arc(R(0, w), R(H*.4, H*.85), r, 0, 2*math.pi)
            c.set_source_rgba(.95, .8, 1, .45); c.set_line_width(1.5); c.stroke()
    elif tex == 'water':
        c.set_source_rgba(1, 1, 1, .75); c.set_line_width(max(1.5, RENDER*.05))
        for x in range(0, w + 4, 4): c.line_to(x, H*.47 + math.sin(x/(RENDER*.18))*RENDER*.05)
        c.stroke(); c.rectangle(0, H*.6, w, H); c.set_source_rgba(0, .16, .47, .28); c.fill()
    elif tex == 'wood':
        c.set_source_rgba(.08, .27, .04, .35); c.set_line_width(1.2); x = -H
        while x < w: c.move_to(x, H); c.line_to(x + H*.6, 0); x += RENDER*.16
        c.stroke()
    elif tex == 'earth':
        c.set_source_rgba(.2, .08, 0, .7); c.set_line_width(max(1.2, RENDER*.03))
        for _ in range(2):
            x, y = R(0, w), R(PAD, H-PAD); c.move_to(x, y)
            for _k in range(4): x += R(-RENDER*.18, RENDER*.18); y += R(-RENDER*.14, RENDER*.14); c.line_to(x, y)
            c.stroke()
    elif tex == 'light':
        rg = cairo.RadialGradient(w/2, H/2, 0, w/2, H/2, w*.5)
        rg.add_color_stop_rgba(0, 1, 1, 1, .9); rg.add_color_stop_rgba(.5, 1, 1, 1, .2); rg.add_color_stop_rgba(1, 1, 1, 1, 0)
        c.rectangle(0, 0, w, H); c.set_source(rg); c.fill()
    elif tex == 'dark':
        for _ in range(4):
            c.arc(R(0, w), R(H*.45, H*.85), R(RENDER*.08, RENDER*.22), 0, 2*math.pi); c.set_source_rgba(.08, 0, .16, R(.2, .45)); c.fill()
    elif tex == 'steel':
        c.save(); c.translate(w*.55, 0); c.rotate(.5)
        c.rectangle(-RENDER*.06, -H, RENDER*.12, H*3); c.set_source_rgba(1, 1, 1, .75); c.fill()
        c.rectangle(RENDER*.14, -H, RENDER*.05, H*3); c.set_source_rgba(1, 1, 1, .35); c.fill(); c.restore()
    c.rectangle(0, 0, w, PAD + RENDER*.42); c.set_source_rgba(1, 1, 1, .22); c.fill()   # 上半凿刻亮面
    c.set_operator(cairo.OPERATOR_OVER)
    if not GL[ch].get('thin'): stroke(c, ch, SW*.22, (1, 1, 1, .55), -.12, -.16)       # 中线亮棱
    return surf_to_pil(s)

metrics = dict(font="LieRen", unitsPerEm=10, renderSize=RENDER, shear=SHEAR, tracking=TRACK, cellHeight=CELL_H, glyphs=[])
x = 0
for ch in CHARS:
    ox, oy = origin(ch)
    metrics['glyphs'].append(dict(ch=ch, x=x, w=cell_w(ch), originX=ox, originY=oy, advance=GL[ch]['w'] + TRACK))
    x += cell_w(ch)
STRIP_W = x
for name, st in STYLES.items():
    rng = random.Random(hash(name) & 0xffff if False else sum(map(ord, name)))   # 固定种子，结果可复现
    atlas = Image.new('RGBA', (STRIP_W, CELL_H * 2), (0, 0, 0, 0))
    for ch in CHARS:
        gx = next(g for g in metrics['glyphs'] if g['ch'] == ch)['x']
        atlas.paste(render_back(ch, st), (gx, 0))
        atlas.paste(render_face(ch, st, rng), (gx, CELL_H))
    atlas.save(os.path.join(OUT, f"DamageDigits_{name}.png"), optimize=True)

# ---------- 白色粒子与徽章底图 ----------
FX = 64
fx_names = ['spark', 'ember', 'drop', 'leaf', 'rock', 'star', 'smoke', 'ring', 'badge', 'diamond', 'shard', 'streak', 'bubble']
fx = Image.new('RGBA', (FX * len(fx_names), FX), (0, 0, 0, 0))
for i, n in enumerate(fx_names):
    s = cairo.ImageSurface(cairo.FORMAT_ARGB32, FX, FX); c = cairo.Context(s); c.translate(FX/2, FX/2); c.set_source_rgba(1, 1, 1, 1)
    if n == 'spark': c.set_line_width(5); c.set_line_cap(cairo.LINE_CAP_ROUND); c.move_to(-26, 0); c.line_to(26, 0); c.stroke()
    elif n == 'ember':
        rg = cairo.RadialGradient(0, 0, 0, 0, 0, 26); rg.add_color_stop_rgba(0, 1, 1, 1, 1); rg.add_color_stop_rgba(.5, 1, 1, 1, .8); rg.add_color_stop_rgba(1, 1, 1, 1, 0)
        c.set_source(rg); c.arc(0, 0, 26, 0, 7); c.fill()
    elif n == 'drop': c.save(); c.scale(.55, 1); c.arc(0, 6, 18, 0, 7); c.restore(); c.fill(); c.move_to(0, -26); c.line_to(-9, 0); c.line_to(9, 0); c.fill()
    elif n == 'leaf': c.save(); c.rotate(.6); c.scale(1, .42); c.arc(0, 0, 26, 0, 7); c.restore(); c.fill()
    elif n == 'rock':
        c.move_to(-22, -12); c.line_to(18, -20); c.line_to(24, 8); c.line_to(6, 22); c.line_to(-20, 14); c.close_path(); c.fill_preserve()
        c.set_source_rgba(0, 0, 0, .55); c.set_line_width(3); c.stroke()
    elif n == 'star':
        c.set_line_width(5); c.set_line_cap(cairo.LINE_CAP_ROUND)
        for a in (0, math.pi/2): c.move_to(-28*math.cos(a), -28*math.sin(a)); c.line_to(28*math.cos(a), 28*math.sin(a))
        c.stroke()
    elif n == 'smoke':
        rg = cairo.RadialGradient(0, 0, 0, 0, 0, 30); rg.add_color_stop_rgba(0, 1, 1, 1, .8); rg.add_color_stop_rgba(1, 1, 1, 1, 0); c.set_source(rg); c.arc(0, 0, 30, 0, 7); c.fill()
    elif n == 'ring': c.set_line_width(4); c.arc(0, 0, 26, 0, 7); c.stroke()
    elif n == 'badge': c.move_to(-30, -14); c.line_to(31, -14); c.line_to(26, 14); c.line_to(-31, 14); c.close_path(); c.fill()   # 斜切徽章底（9 宫格横向拉伸）
    elif n == 'diamond': c.rotate(math.pi/4); c.rectangle(-17, -17, 34, 34); c.fill()
    elif n == 'shard': c.move_to(0, -28); c.line_to(8, 0); c.line_to(0, 28); c.line_to(-8, 0); c.close_path(); c.fill()
    elif n == 'streak': c.set_line_width(5); c.set_line_cap(cairo.LINE_CAP_ROUND); c.arc(0, 18, 26, math.pi*1.15, math.pi*1.85); c.stroke()
    elif n == 'bubble': c.set_line_width(4); c.arc(0, 0, 20, 0, 7); c.stroke(); c.arc(-7, -7, 5, 0, 7); c.fill()
    fx.paste(surf_to_pil(s), (i * FX, 0))
fx.save(os.path.join(OUT, "DamageFx.png"), optimize=True)
metrics['fx'] = dict(cell=FX, names=fx_names)
metrics['styles'] = list(STYLES.keys())
json.dump(metrics, open(os.path.join(OUT, "DamageDigits.json"), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print("strip", STRIP_W, "x", CELL_H * 2, "styles", len(STYLES))
