"""Prepares hand-drawn art for Unity: Assets/Art/{UI,Items,Environment}.

- Images on a white background (no alpha, e.g. JPEGs from chat): white becomes transparent with a soft edge, and
  the white fringe is removed from the edge pixels (un-blending against white). Line art keys out every white
  pixel; framed art ('border' mode) only the white connected to the image border, so the inside of a frame stays.
  'frame' mode keeps only the colored or dark strokes (the wood of a frame): light greys (a paper texture) go too.
- Every image is trimmed to its content (plus a small margin), so a bottom-center pivot sits on the drawing's base.

Usage: python3 Tools/prepare_art.py <source dir or file>... (see JOBS for names and destinations)
"""
import sys
from collections import deque
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent / 'Assets' / 'Art'
MARGIN = 6
WHITE_FLOOR = 200  # min channel above this counts as background when connected to the border

# (source file name, destination relative to Assets/Art[, 'border' = keep enclosed white])
JOBS = [
    ('WhatsApp Image 2026-11-24 at 2.44.07 PM (1).jpeg', 'UI/Heart_Full.png'),
    ('WhatsApp Image 2026-11-24 at 2.44.07 PM.jpeg', 'UI/Heart_Lost.png'),
    ('WhatsApp Image 2026-11-24 at 2.44.07 PM (2).jpeg', 'UI/Slot_Frame.png', 'frame'),
    ('madera.png', 'Items/Wood.png'),
    ('piedra.png', 'Items/Stone.png'),
    ('manzana.png', 'Items/Apple.png'),
    ('cuerda.png', 'Items/Rope.png'),
    ('pasto1.png', 'Environment/Grass_1.png'),
    ('pasto2.png', 'Environment/Grass_2.png'),
    ('pasto3.png', 'Environment/Grass_3.png'),
    # Trees (already transparent PNGs in Assets/Art/Environment/Trees): trimmed, each tree with its stump.
    ('IMG_3179.PNG', 'Environment/Trees/Oak.png'),
    ('IMG_3178.PNG', 'Environment/Trees/Oak_Stump.png'),
    ('IMG_3180.PNG', 'Environment/Trees/Pine.png'),
    ('IMG_3181.PNG', 'Environment/Trees/Pine_Stump.png'),
]


def key_out_white(image, border_only):
    rgb = image.convert('RGB')
    w, h = rgb.size
    px = rgb.load()
    background = bytearray(w * h)
    if not border_only:
        for y in range(h):
            for x in range(w):
                background[y * w + x] = min(px[x, y]) >= WHITE_FLOOR
    queue = deque()
    if border_only:
        for x in range(w):
            queue.extend(((x, 0), (x, h - 1)))
        for y in range(h):
            queue.extend(((0, y), (w - 1, y)))
    while queue:
        x, y = queue.popleft()
        i = y * w + x
        if background[i] or min(px[x, y]) < WHITE_FLOOR:
            continue
        background[i] = 1
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < w and 0 <= ny < h and not background[ny * w + nx]:
                queue.append((nx, ny))

    out = Image.new('RGBA', (w, h))
    opx = out.load()
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y]
            if not background[y * w + x]:
                opx[x, y] = (r, g, b, 255)
                continue
            alpha = (255 - min(r, g, b)) / (255 - WHITE_FLOOR)  # 0 = pure white, 1 = at the floor
            if alpha <= 0.02:
                opx[x, y] = (0, 0, 0, 0)
                continue
            alpha = min(alpha, 1.0)
            # Undo the blend with white: c = a*color + (1-a)*255.
            color = [max(0, min(255, round((c - (1 - alpha) * 255) / alpha))) for c in (r, g, b)]
            opx[x, y] = (*color, round(alpha * 255))
    return out


GREY_SPREAD = 42   # max - min channel below this counts as grey (no color)
GREY_FLOOR = 100   # greys lighter than this are paper, not stroke


def clear_light_grey(image):
    """Makes light, colorless pixels transparent (with a soft ramp), keeping colored and dark strokes."""
    px = image.load()
    w, h = image.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            spread, low = max(r, g, b) - min(r, g, b), min(r, g, b)
            greyness = min(1.0, max(0.0, (GREY_SPREAD - spread) / 12.0))
            lightness = min(1.0, max(0.0, (low - GREY_FLOOR) / 25.0))
            keep = 1.0 - greyness * lightness
            if keep < 1.0:
                px[x, y] = (r, g, b, int(a * keep))
    return image


def trim(image):
    box = image.getchannel('A').point(lambda a: 255 if a > 8 else 0).getbbox()
    if box is None:
        return image
    l, t, r, b = box
    return image.crop((max(0, l - MARGIN), max(0, t - MARGIN), min(image.width, r + MARGIN), min(image.height, b + MARGIN)))


def main(sources):
    files = {}
    for source in map(Path, sources):
        for path in ([source] if source.is_file() else source.iterdir()):
            files[path.name] = path
    for name, destination, *mode in JOBS:
        if name not in files:
            print(f'skip {name} (not found)')
            continue
        image = Image.open(files[name])
        image = image.convert('RGBA') if 'A' in image.getbands() else key_out_white(image, border_only=mode == ['border'])
        if mode == ['frame']:
            image = clear_light_grey(image)
        image = trim(image)
        target = ROOT / destination
        target.parent.mkdir(parents=True, exist_ok=True)
        image.save(target)
        print(f'{name} -> {target.relative_to(ROOT.parent.parent)} {image.size}')


if __name__ == '__main__':
    main(sys.argv[1:])
