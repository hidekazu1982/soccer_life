"""試合画面の背景（観客席スタンド）を生成する。
出力:
  bg_match_stands.png          完成版（既定カラー：赤×緑）
  bg_match_stands_base.png     ベース（tint1/tint2 を重ねる前提。完成版と同じ絵）
  bg_match_stands_tint1.png    クラブカラー1用マスク（白～灰の陰影。UnityのImage.colorで着色）
  bg_match_stands_tint2.png    クラブカラー2用マスク
"""
import math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

random.seed(7)
W, H = 1920, 1080
S = 2                      # スーパーサンプリング倍率
SW, SH = W * S, H * S
HORIZON = 776              # ピッチ画像の芝が始まる高さ（Figma game フレーム実測）

C1 = (214, 48, 44)         # 既定カラー1（ユニフォームの赤）
C2 = (46, 140, 70)         # 既定カラー2（パンツの緑）
OUT = (58, 40, 32)         # 線画の焦げ茶

col = Image.new("RGB", (SW, SH), (255, 255, 255))
mid = Image.new("L", (SW, SH), 0)      # 0=その他 1=カラー1 2=カラー2
shd = Image.new("L", (SW, SH), 255)    # マスク用の陰影
dc, di, ds = ImageDraw.Draw(col), ImageDraw.Draw(mid), ImageDraw.Draw(shd)


def s(v):
    return int(round(v * S))


def shade(c, k):
    return tuple(max(0, min(255, int(v * k))) for v in c)


def mix(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


NEUTRAL = (250, 250, 250)  # ベースではクラブカラー部分を白で描く（上から tint で着色）


def team(kind, k):
    """kind: 1/2 → (rgb, id, shade)"""
    base = NEUTRAL
    return shade(base, k), kind, max(0, min(255, int(255 * k)))


def shape(fn, xy, fill, kind=0, k=1.0, outline=None, width=0, **kw):
    if kind:
        fill, idv, sv = team(kind, k)
    else:
        idv, sv = (None, None) if fill is None else (0, 255)
    getattr(dc, fn)(xy, fill=fill, outline=outline, width=width, **kw)
    getattr(di, fn)(xy, fill=idv, outline=0 if outline else None, width=width, **kw)
    getattr(ds, fn)(xy, fill=sv, outline=sv if outline else None, width=width, **kw)


def line(xy, fill, width, kind=0, k=1.0):
    if kind:
        fill, idv, sv = team(kind, k)
    else:
        idv, sv = 0, 255
    dc.line(xy, fill=fill, width=width, joint="curve")
    di.line(xy, fill=idv, width=width, joint="curve")
    ds.line(xy, fill=sv, width=width, joint="curve")


# ---------------------------------------------------------------- 空
SKY_TOP, SKY_BOT = (150, 200, 235), (215, 236, 246)
ROOF_Y = 70
for y in range(s(ROOF_Y) + 1):
    dc.line([(0, y), (SW, y)], fill=mix(SKY_TOP, SKY_BOT, y / s(ROOF_Y)))
for cx, cy, cw in [(180, 30, 220), (720, 18, 300), (1250, 40, 240), (1700, 22, 280)]:
    for i in range(5):
        ox = (i - 2) * cw * 0.22
        r = cw * (0.22 - abs(i - 2) * 0.04)
        dc.ellipse([s(cx + ox - r), s(cy - r * 0.6), s(cx + ox + r), s(cy + r * 0.6)], fill=(248, 251, 252))

# ---------------------------------------------------------------- 屋根
# 屋根の鼻先（白い帯）とその下の影
shape("rectangle", [0, s(ROOF_Y), SW, s(ROOF_Y + 34)], (236, 232, 222))
shape("rectangle", [0, s(ROOF_Y + 34), SW, s(ROOF_Y + 40)], (196, 190, 178))
line([0, s(ROOF_Y), SW, s(ROOF_Y)], OUT, s(3))
UNDER_TOP, UNDER_BOT = ROOF_Y + 40, 175
for y in range(s(UNDER_TOP), s(UNDER_BOT)):
    t = (y - s(UNDER_TOP)) / (s(UNDER_BOT) - s(UNDER_TOP))
    dc.line([(0, y), (SW, y)], fill=mix((70, 74, 86), (104, 108, 118), t))
# 屋根裏のトラス
for x in range(-40, W + 80, 120):
    line([(s(x), s(UNDER_TOP)), (s(x + 60), s(UNDER_BOT - 8))], (88, 92, 104), s(4))
    line([(s(x + 120), s(UNDER_TOP)), (s(x + 60), s(UNDER_BOT - 8))], (88, 92, 104), s(4))
line([(0, s(UNDER_BOT - 8)), (SW, s(UNDER_BOT - 8))], (80, 84, 96), s(5))
# 照明（屋根下の投光器）
glow = Image.new("RGBA", (SW, SH), (0, 0, 0, 0))
gd = ImageDraw.Draw(glow)
for x in range(110, W, 260):
    y = ROOF_Y + 54
    gd.ellipse([s(x - 70), s(y - 40), s(x + 70), s(y + 50)], fill=(255, 246, 210, 90))
    shape("rounded_rectangle", [s(x - 26), s(y - 10), s(x + 26), s(y + 12)], (62, 62, 70), radius=s(5))
    shape("rounded_rectangle", [s(x - 21), s(y - 6), s(x + 21), s(y + 8)], (255, 248, 222), radius=s(4))

# ---------------------------------------------------------------- スタンド（客席）
STAND_TOP, STAND_BOT = UNDER_BOT, 678
# 段の高さは手前ほど大きく（遠近感）
rows = []
y = STAND_TOP
h0, h1 = 40, 58
n_est = 14
while y < STAND_BOT - 4:
    t = (y - STAND_TOP) / (STAND_BOT - STAND_TOP)
    rh = h0 + (h1 - h0) * t
    rows.append((y, rh))
    y += rh
scale_fix = (STAND_BOT - STAND_TOP) / (y - STAND_TOP)
rows = [(STAND_TOP + (ry - STAND_TOP) * scale_fix, rh * scale_fix) for ry, rh in rows]

# 客席の下地（コンクリートの段）
for i, (ry, rh) in enumerate(rows):
    shape("rectangle", [0, s(ry), SW, s(ry + rh)], (128, 128, 132) if i % 2 == 0 else (120, 120, 126))
    shape("rectangle", [0, s(ry + rh - 4), SW, s(ry + rh)], (98, 98, 106))

# 通路（階段）
AISLES = [300, 960, 1620]
AISLE_W = 34

SKIN = [(250, 214, 180), (240, 196, 160), (222, 172, 132), (196, 142, 104), (150, 104, 74), (252, 224, 196)]
HAIR = [(40, 30, 26), (70, 48, 34), (120, 80, 50), (30, 30, 34), (190, 150, 90), (160, 60, 40), (90, 90, 96)]

flags = []  # (x, row_index)
for fx in [120, 560, 760, 1180, 1430, 1800]:
    flags.append((fx + random.randint(-30, 30), random.randint(1, len(rows) - 5)))


def draw_person(cx, base_y, rh, light):
    """cx: 中心x, base_y: 段の下端, rh: 段の高さ, light: 明るさ(0.75-1.05)"""
    hr = rh * 0.27                              # 頭の半径
    stand_up = random.random() < 0.85
    head_y = base_y - rh * (0.98 if stand_up else 0.80)
    bw = hr * 2.5
    body_top = head_y + hr * 0.75
    # 服：8割がホームカラー1、1割がカラー2、残りは白
    r = random.random()
    kind = 1 if r < 0.80 else (2 if r < 0.90 else 0)
    k = light * random.uniform(0.86, 1.04)
    shirt = shade((236, 236, 232), light) if kind == 0 else None
    shape("rounded_rectangle", [s(cx - bw / 2), s(body_top), s(cx + bw / 2), s(base_y + rh * 0.4)],
          shirt, kind, k, outline=OUT, width=s(1.4), radius=s(hr * 0.9))
    # 腕
    pose = random.random()
    sk = shade(random.choice(SKIN), light)
    arm_w = max(2, s(hr * 0.55))
    sx_l, sx_r, sy = cx - bw * 0.42, cx + bw * 0.42, body_top + hr * 0.35
    if pose < 0.16:  # マフラーを頭上に掲げる
        top = head_y - hr * 2.1
        line([(s(sx_l), s(sy)), (s(cx - bw * 0.75), s(top))], None, arm_w, kind or 1, k)
        line([(s(sx_r), s(sy)), (s(cx + bw * 0.75), s(top))], None, arm_w, kind or 1, k)
        mw, mh = bw * 1.9, hr * 0.62
        x0 = cx - mw / 2
        seg = mw / 6
        for j in range(6):
            shape("rectangle", [s(x0 + j * seg), s(top - mh / 2), s(x0 + (j + 1) * seg), s(top + mh / 2)],
                  None, 1 if j % 2 == 0 else 2, light)
        shape("rectangle", [s(x0), s(top - mh / 2), s(x0 + mw), s(top + mh / 2)], None, outline=OUT, width=s(1.2))
        for hx in (cx - bw * 0.75, cx + bw * 0.75):
            shape("ellipse", [s(hx - hr * 0.33), s(top - hr * 0.33), s(hx + hr * 0.33), s(top + hr * 0.33)], sk)
    elif pose < 0.55:  # 両手（または片手）を上げて応援
        both = random.random() < 0.6
        sway = random.uniform(-0.3, 0.3) * hr
        for side, sx in ((-1, sx_l), (1, sx_r)):
            if not both and side == -1 and random.random() < 0.5:
                continue
            hx = sx + side * hr * random.uniform(0.5, 1.1) + sway
            hy = head_y - hr * random.uniform(1.3, 2.2)
            line([(s(sx), s(sy)), (s(hx), s(hy))], None, arm_w, kind or 1, k) if kind else \
                line([(s(sx), s(sy)), (s(hx), s(hy))], shirt, arm_w)
            shape("ellipse", [s(hx - hr * 0.33), s(hy - hr * 0.33), s(hx + hr * 0.33), s(hy + hr * 0.33)], sk)
    # 頭
    shape("ellipse", [s(cx - hr), s(head_y - hr), s(cx + hr), s(head_y + hr)], sk, outline=OUT, width=s(1.3))
    hair = shade(random.choice(HAIR), light)
    if random.random() < 0.12:  # ホームカラーの帽子
        shape("chord", [s(cx - hr * 1.05), s(head_y - hr * 1.15), s(cx + hr * 1.05), s(head_y + hr * 0.7)],
              None, 1, k, outline=OUT, width=s(1.2), start=180, end=360)
    else:
        shape("chord", [s(cx - hr * 1.02), s(head_y - hr * 1.08), s(cx + hr * 1.02), s(head_y + hr * 0.5)],
              hair, start=180, end=360)


for i, (ry, rh) in enumerate(rows):
    base_y = ry + rh
    t = i / (len(rows) - 1)
    light = 0.80 + 0.22 * t                     # 屋根の影：奥ほど暗い
    spacing = rh * 0.80
    x = -spacing * random.random()
    while x < W + spacing:
        in_aisle = any(abs(x - a) < AISLE_W / 2 + rh * 0.3 for a in AISLES)
        if not in_aisle and random.random() > 0.03:
            draw_person(x + random.uniform(-2, 2), base_y, rh, light)
        x += spacing * random.uniform(0.92, 1.08)
    # 通路の段差（手前の段の上に描く）
    for a in AISLES:
        shape("rectangle", [s(a - AISLE_W / 2), s(ry), s(a + AISLE_W / 2), s(base_y)], shade((176, 174, 170), light))
        shape("rectangle", [s(a - AISLE_W / 2), s(base_y - 4), s(a + AISLE_W / 2), s(base_y)], shade((140, 138, 136), light))
    # 旗
    for fx, fr in flags:
        if fr != i:
            continue
        pole_top = ry - rh * 3.4
        line([(s(fx), s(base_y)), (s(fx), s(pole_top))], (90, 70, 56), s(3))
        fw, fh = rh * 2.8, rh * 1.7
        pts_top, pts_bot = [], []
        ph = random.uniform(0, 6)
        for j in range(21):
            u = j / 20
            wave = math.sin(u * 5 + ph) * fh * 0.12 * u
            pts_top.append((s(fx + u * fw), s(pole_top + wave)))
            pts_bot.append((s(fx + u * fw), s(pole_top + fh + wave)))
        poly = pts_top + pts_bot[::-1]
        shape("polygon", poly, None, 1, light)
        # カラー2の帯
        band = [(p[0], p[1] + s(fh * 0.38)) for p in pts_top] + [(p[0], p[1] - s(fh * 0.38)) for p in pts_bot[::-1]]
        shape("polygon", band, None, 2, light)
        dc.line(poly + [poly[0]], fill=OUT, width=s(1.5))

# ---------------------------------------------------------------- 最前列のフェンスと横断幕
WALL_TOP, WALL_BOT = STAND_BOT, 708
shape("rectangle", [0, s(WALL_TOP), SW, s(WALL_BOT)], (84, 88, 98))
shape("rectangle", [0, s(WALL_TOP), SW, s(WALL_TOP + 4)], (60, 62, 70))
# 横断幕（文字なしの縞柄：どのクラブでも使える）
for bx, bw in [(40, 380), (520, 300), (1100, 300), (1500, 380)]:
    by0, by1 = WALL_TOP - 14, WALL_BOT - 2
    shape("rectangle", [s(bx), s(by0), s(bx + bw), s(by1)], None, 1, 1.0)
    stripe = (by1 - by0) / 5
    shape("rectangle", [s(bx), s(by0 + stripe * 2), s(bx + bw), s(by0 + stripe * 3)], None, 2, 1.0)
    for j in range(5):
        sx = bx + bw * (j + 0.5) / 5
        cy = by0 + stripe * 2.5
        r = stripe * 0.8
        star = []
        for q in range(10):
            a = -math.pi / 2 + q * math.pi / 5
            rr = r if q % 2 == 0 else r * 0.45
            star.append((s(sx + rr * math.cos(a)), s(cy + rr * math.sin(a))))
        shape("polygon", star, (250, 248, 240))
    shape("rectangle", [s(bx), s(by0), s(bx + bw), s(by1)], None, outline=OUT, width=s(2))
    for ex in (bx + 6, bx + bw - 6):
        shape("ellipse", [s(ex - 3), s(by0 + 2), s(ex + 3), s(by0 + 8)], (220, 220, 220))

# ---------------------------------------------------------------- 広告ボード（ピッチ脇）
AD_TOP, AD_BOT = 716, HORIZON + 6
# ボード裏の通路
shape("rectangle", [0, s(WALL_BOT), SW, s(AD_TOP)], (126, 160, 108))
font = ImageFont.truetype("/usr/share/fonts/truetype/google-fonts/ofl/dmsans/DMSans[opsz,wght].ttf", s(30))
try:
    font.set_variation_by_name("Bold")
except Exception:
    pass
ADS = [((30, 60, 110), (255, 255, 255), "SOCCER LIFE"),
       ((250, 250, 246), (40, 40, 46), "FOOTBALL"),
       ((240, 170, 40), (40, 30, 20), "GOAL!"),
       ((46, 46, 54), (120, 220, 255), "LIVE MATCH")]
bwid = 320
x = -60
k = 0
while x < W:
    bg, fg, txt = ADS[k % len(ADS)]
    shape("rectangle", [s(x), s(AD_TOP), s(x + bwid), s(AD_BOT)], bg)
    shape("rectangle", [s(x), s(AD_TOP), s(x + bwid), s(AD_TOP + 5)], shade(bg, 1.15))
    tw = dc.textlength(txt, font=font)
    dc.text((s(x + bwid / 2) - tw / 2, s((AD_TOP + AD_BOT) / 2 - 4)), txt, font=font, fill=fg, anchor="lm")
    line([s(x), s(AD_TOP), s(x), s(AD_BOT)], (30, 30, 34), s(2))
    x += bwid
    k += 1
line([0, s(AD_TOP), SW, s(AD_TOP)], OUT, s(2))
# 芝（ピッチ画像の下に隠れる部分。継ぎ目対策で同系色にする）
for y in range(s(AD_BOT), SH):
    t = (y - s(AD_BOT)) / (SH - s(AD_BOT))
    dc.line([(0, y), (SW, y)], fill=mix((168, 195, 130), (140, 176, 112), t))
    di.line([(0, y), (SW, y)], fill=0)

# ---------------------------------------------------------------- 仕上げ
col = Image.alpha_composite(col.convert("RGBA"), glow.filter(ImageFilter.GaussianBlur(s(14))))
# 屋根の影を上段にかぶせる
shadow = Image.new("RGBA", (SW, SH), (0, 0, 0, 0))
sd = ImageDraw.Draw(shadow)
for y in range(s(STAND_TOP), s(STAND_TOP + 140)):
    a = int(70 * (1 - (y - s(STAND_TOP)) / s(140)))
    sd.line([(0, y), (SW, y)], fill=(20, 22, 40, a))
col = Image.alpha_composite(col, shadow).convert("RGB")
# 紙っぽい軽いノイズ
arr = np.asarray(col).astype(np.int16)
noise = np.random.default_rng(3).normal(0, 3.0, arr.shape[:2])[..., None]
arr = np.clip(arr + noise, 0, 255).astype(np.uint8)
col = Image.fromarray(arr)

raw = col.resize((W, H), Image.LANCZOS).filter(ImageFilter.GaussianBlur(0.6))
import os
os.makedirs("layers", exist_ok=True)

# もや（UIの文字を読みやすくするため、客席を少し白っぽく後退させる）をベースに焼き込む
HAZE = np.array([238, 244, 248], np.float32)
HAZE_A = 0.30
y = np.arange(H)[:, None, None]
ha = np.where(y < AD_TOP, HAZE_A, np.where(y < HORIZON, 0.12, 0.0)).astype(np.float32)
rawf = np.asarray(raw).astype(np.float32)
base = Image.fromarray(np.clip(rawf * (1 - ha) + HAZE * ha, 0, 255).astype(np.uint8))
base.save("layers/bg_match_stands.png", optimize=True)

ids = np.asarray(mid)
sh = np.asarray(shd).astype(np.float32)
yy = np.arange(SH)[:, None]
roof = np.clip(1 - (yy - s(STAND_TOP)) / s(140), 0, 1) * (yy >= s(STAND_TOP)) * 0.27
sh = np.clip(sh * (1 - roof), 0, 255)
tints = {}
for kind in (1, 2):
    a = (ids == kind).astype(np.uint8) * 255
    g = sh.astype(np.uint8)
    im = Image.fromarray(np.dstack([g, g, g, a]), "RGBA").resize((W, H), Image.LANCZOS).filter(ImageFilter.GaussianBlur(0.6))
    im.save(f"layers/bg_match_stands_tint{kind}.png", optimize=True)
    tints[kind] = np.asarray(im).astype(np.float32) / 255


def compose(c1, c2):
    """Unity と同じ合成：ベースの上に tint を (クラブカラー, 不透明度 1-もや) で重ねる"""
    out = np.asarray(base).astype(np.float32)
    for kind, c in ((1, c1), (2, c2)):
        a = tints[kind]
        alpha = a[..., 3:4] * (1 - HAZE_A)
        rgb = a[..., :3] * np.array(c, np.float32)
        out = out * (1 - alpha) + rgb * alpha
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8))


compose(C1, C2).save("bg_match_stands_red.png", optimize=True)
compose((0x20, 0x40, 0xE0), (250, 250, 250)).save("sample_blue_white.png")
compose((250, 200, 40), (30, 30, 40)).save("sample_yellow_black.png")
