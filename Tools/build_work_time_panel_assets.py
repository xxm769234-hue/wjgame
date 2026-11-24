import argparse
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFont


PROJECT_ROOT = Path(__file__).resolve().parents[1]
RESOURCE_ROOT = PROJECT_ROOT / "Assets/Resources/UI"


def build_resource_card():
    atlas = Image.open(RESOURCE_ROOT / "LandInfoPanelPopups.png").convert("RGBA")
    card = atlas.crop((16, 840, 1026, 1154))
    paper = card.crop((310, 40, 610, 95)).resize((930, 250), Image.Resampling.BICUBIC)
    mask = Image.new("L", paper.size)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, 929, 249), radius=18, fill=255)
    card.paste(paper, (40, 40), mask)
    card = card.resize((505, 157), Image.Resampling.LANCZOS)
    card.save(RESOURCE_ROOT / "WorkTimeResourceCard.png")
    return card


def nine_slice(source, size, source_border, target_border):
    result = Image.new("RGBA", size)
    source_columns = (0, source_border, source.width - source_border, source.width)
    source_rows = (0, source_border, source.height - source_border, source.height)
    target_columns = (0, target_border, size[0] - target_border, size[0])
    target_rows = (0, target_border, size[1] - target_border, size[1])
    for row in range(3):
        for column in range(3):
            tile = source.crop((source_columns[column], source_rows[row], source_columns[column + 1], source_rows[row + 1]))
            tile = tile.resize((target_columns[column + 1] - target_columns[column], target_rows[row + 1] - target_rows[row]), Image.Resampling.LANCZOS)
            result.alpha_composite(tile, (target_columns[column], target_rows[row]))
    return result


def render_preview(card, output):
    controls = Image.open(RESOURCE_ROOT / "LandInfoPanelControls.png").convert("RGBA")
    popups = Image.open(RESOURCE_ROOT / "LandInfoPanelPopups.png").convert("RGBA")
    icon_root = PROJECT_ROOT / "Assets/_Imported/weijun-game-project/Assets/UI/资源图标"
    preview = Image.new("RGBA", (700, 730), "#433b32")
    preview.alpha_composite(popups.crop((16, 16, 823, 824)).resize((620, 620), Image.Resampling.LANCZOS), (40, 40))

    def paste(sprite, position, size, tint=None, border=None):
        if border is None:
            sprite = sprite.resize(size, Image.Resampling.LANCZOS)
        else:
            sprite = nine_slice(sprite, size, border, border // 2)
        if tint is not None:
            sprite = ImageChops.multiply(sprite, Image.new("RGBA", sprite.size, tint))
        preview.alpha_composite(sprite, (round(350 + position[0] - size[0] / 2), round(350 - position[1] - size[1] / 2)))

    def label(value, position, size, color="#4d291c", anchor="mm", outline=False):
        ImageDraw.Draw(preview).text((350 + position[0], 350 - position[1]), value,
                                    font=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", size), fill=color, anchor=anchor,
                                    stroke_width=1 if outline else 0, stroke_fill="#20110b")

    label("补充工时", (0, 277), 26, "white", outline=True)
    label("剩余工时  0秒", (0, 214), 20)
    label("上限 24小时0分  ·  生产暂停", (0, 190), 16, "#6b452e")
    label("选择补充资源", (-258, 161), 16, "#6b452e", anchor="lm")
    for index, (name, duration) in enumerate((("面包", "1小时0分"), ("烤肉", "1小时30分"), ("烤鱼", "2小时0分"))):
        row_center = 110 - index * 74
        paste(card, (0, row_center), (532, 68), (240, 235, 224, 255), border=30)
        icon = Image.open(icon_root / (name + ".png")).convert("RGBA")
        icon.thumbnail((60, 54), Image.Resampling.LANCZOS)
        paste(icon, (-220, row_center), icon.size)
        label(name, (-176, row_center + 14), 21, anchor="lm")
        label("每个 +" + duration, (-176, row_center - 15), 16, "#6b452e", anchor="lm")
        label("拥有 0", (248, row_center + 12), 16, "#6b452e", anchor="rm")
        label("暂无可用资源", (248, row_center - 12), 16, "#6b452e", anchor="rm")
    label("请选择补充物品", (0, -95), 17)
    square = controls.crop((16, 214, 217, 414))
    disabled = (199, 191, 179, 255)
    for position, text in (((-136, -134), "−"), ((52, -134), "+")):
        paste(square, position, (42, 42), disabled, border=32)
        label(text, position, 18, "white", outline=True)
    paste(card, (-42, -134), (112, 42), border=30)
    label("0", (-42, -134), 23)
    paste(controls.crop((1558, 16, 1826, 162)), (150, -134), (106, 42), disabled, border=32)
    label("最大", (150, -134), 18, "white", outline=True)
    label("消耗资源，为全部生产建筑补充工时", (0, -182), 17, "#6b452e")
    for position, bounds, text in (((-126, -261), (402, 16, 771, 198), "预览补充"), ((126, -261), (16, 16, 386, 198), "确认补充")):
        paste(controls.crop(bounds), position, (212, 44), disabled, border=32)
        label(text, position, 18, "white", outline=True)
    paste(square, (261, 249), (38, 38), border=32)
    paste(controls.crop((233, 214, 391, 372)), (261, 249), (24, 24))
    label("素材与坐标排版校对图 · 非 Unity 运行截图", (0, -345), 16, "#e7d5b7")
    output.parent.mkdir(parents=True, exist_ok=True)
    preview.convert("RGB").save(output)
    print(output)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    arguments = parser.parse_args()
    result = build_resource_card()
    if arguments.preview:
        render_preview(result, arguments.preview)
