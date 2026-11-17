"""앱 아이콘(app.ico) 생성 스크립트: python3 tools/make_icon.py"""
from pathlib import Path
from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent.parent / "src" / "GProX3BatteryChecker" / "app.ico"


def draw(size: int) -> Image.Image:
    scale = 4  # 슈퍼샘플링
    s = size * scale
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # 배경 (둥근 사각형, 어두운 색)
    d.rounded_rectangle([0, 0, s - 1, s - 1], radius=int(s * 0.22), fill=(28, 28, 30, 255))

    # 배터리 몸체
    bw, bh = s * 0.66, s * 0.40
    x0, y0 = s * 0.12, (s - bh) / 2
    stroke = max(scale, int(s * 0.055))
    d.rounded_rectangle([x0, y0, x0 + bw, y0 + bh], radius=int(bh * 0.18),
                        outline=(240, 240, 240, 255), width=stroke)
    # + 단자
    nw = s * 0.07
    d.rounded_rectangle([x0 + bw - stroke / 2, y0 + bh * 0.3, x0 + bw + nw, y0 + bh * 0.7],
                        radius=int(nw * 0.4), fill=(240, 240, 240, 255))
    # 게이지 (75%)
    gap = stroke * 1.8
    ix0, iy0 = x0 + gap, y0 + gap
    ix1, iy1 = x0 + bw - gap, y0 + bh - gap
    fill_w = (ix1 - ix0) * 0.75
    d.rounded_rectangle([ix0, iy0, ix0 + fill_w, iy1], radius=int((iy1 - iy0) * 0.15),
                        fill=(76, 194, 106, 255))
    return img.resize((size, size), Image.LANCZOS)


if __name__ == "__main__":
    sizes = [16, 20, 24, 32, 48, 64, 128, 256]
    images = [draw(sz) for sz in sizes]
    images[-1].save(OUT, format="ICO", sizes=[(sz, sz) for sz in sizes], append_images=images[:-1])
    print(f"wrote {OUT}")
