#!/usr/bin/env python3
"""Build the final GitHub banner (1280x640, with title) and multi-size icon.ico."""
import io
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ASSETS = "/home/user/azaroth-installer/assets"

# ---------------------------------------------------------------- banner
art = Image.open(f"{ASSETS}/banner-art.png").convert("RGB")
w, h = art.size
# center-crop to 2:1 then resize to 1280x640
new_h = int(w / 2.0)
top = (h - new_h) // 2
art = art.crop((0, top, w, top + new_h)).resize((1280, 640), Image.LANCZOS)

# dark gradient on the left for text readability
grad = Image.new("RGBA", (1280, 640), (0, 0, 0, 0))
gd = ImageDraw.Draw(grad)
for x in range(860):
    a = int(190 * (1 - x / 860) ** 1.6)
    gd.line([(x, 0), (x, 640)], fill=(5, 8, 16, a))
art = Image.alpha_composite(art.convert("RGBA"), grad)

d = ImageDraw.Draw(art)
fbold = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
freg = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"

def draw_text(draw, xy, text, font, fill, shadow=(0, 0, 0, 200), off=3):
    x, y = xy
    draw.text((x + off, y + off), text, font=font, fill=shadow)
    draw.text((x, y), text, font=font, fill=fill)

title_font = ImageFont.truetype(fbold, 92)
sub_font = ImageFont.truetype(freg, 30)
tag_font = ImageFont.truetype(fbold, 24)

draw_text(d, (56, 208), "AZAROTH CORE", title_font, (255, 205, 92, 255), off=5)
# gold underline accent
d.rectangle([(58, 330), (700, 336)], fill=(255, 205, 92, 230))
draw_text(d, (60, 356), "One-Click Installer  ·  AzerothCore 3.3.5a  +  PlayerBots", sub_font, (235, 240, 250, 255))
draw_text(d, (60, 408), "No terminal. No compiler. One setup.exe.", tag_font, (150, 190, 255, 235))

art.convert("RGB").save(f"{ASSETS}/banner.png", quality=92)
print("banner.png:", art.size)

# ---------------------------------------------------------------- icon ico
src = Image.open(f"{ASSETS}/icon-art.png").convert("RGBA")
# trim uniform background margin a touch (keep the rounded navy square full-bleed)
sizes = [256, 128, 64, 48, 32, 16]
pngs = {}
for s in sizes:
    buf = io.BytesIO()
    src.resize((s, s), Image.LANCZOS).save(buf, format="PNG")
    pngs[s] = buf.getvalue()

count = len(sizes)
header = b"\x00\x00\x01\x00" + count.to_bytes(2, "little")
entries = b""
offset = 6 + 16 * count
for s in sizes:
    w = 0 if s == 256 else s
    entries += bytes([w, w, 0, 0]) + (1).to_bytes(2, "little") + (32).to_bytes(2, "little") + \
             len(pngs[s]).to_bytes(4, "little") + offset.to_bytes(4, "little")
    offset += len(pngs[s])

with open(f"{ASSETS}/icon.ico", "wb") as f:
    f.write(header + entries)
    for s in sizes:
        f.write(pngs[s])
print("icon.ico written, sizes:", sizes)
