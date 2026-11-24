from __future__ import annotations

import hashlib
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage


PROJECT_ROOT = Path(__file__).resolve().parents[1]
SOURCE_ROOT = Path(r"D:\unity相关\模拟经营模式面板素材")
OUTPUT_ROOT = PROJECT_ROOT / "Assets" / "Resources" / "UI"

BASE_SOURCE = SOURCE_ROOT / "c68cac754aa14ce1bb2f0088eb4bfccc.png"
CONTROL_SOURCE = SOURCE_ROOT / "04a4b7ea2b7b40afa3b15c3c247de726.png"
POPUP_SOURCE = SOURCE_ROOT / "ed90c166e8cf48b98980a180d7e6f42a.png"

CONTROL_COMPONENTS = {
    "ButtonPrimary": (133, 135, 1017, 573),
    "ButtonSecondary": (1189, 134, 2070, 573),
    "ButtonDanger": (2278, 137, 3161, 573),
    "ButtonDisabled": (3359, 135, 4242, 574),
    "ButtonCompact": (4570, 178, 5211, 530),
    "ButtonSquare": (333, 731, 814, 1214),
    "Close": (1458, 782, 1838, 1159),
    "BadgeGreen": (2363, 811, 3082, 1133),
    "BadgeBlue": (3439, 811, 4158, 1133),
    "BadgeOrange": (4505, 813, 5222, 1133),
    "BadgeGray": (213, 1415, 934, 1734),
    "StatusGreen": (1254, 1362, 2039, 1789),
    "StatusOrange": (2323, 1362, 3111, 1790),
    "StatusBlue": (3398, 1362, 4183, 1787),
    "StatusGray": (4469, 1362, 5254, 1789),
    "ProgressEmpty": (130, 2093, 1025, 2304),
    "ProgressFill": (1198, 2091, 2094, 2305),
    "Upgrade": (2483, 1946, 2961, 2423),
    "Move": (3567, 1946, 4038, 2422),
    "AssignNpc": (4634, 1946, 5113, 2422),
    "Demolish": (375, 2566, 838, 3038),
    "Inspect": (1435, 2567, 1883, 3022),
    "Terrain": (2455, 2683, 2991, 2926),
    "Wheat": (3603, 2561, 4002, 3034),
    "SyncDot": (4745, 2681, 4993, 2927),
}

POPUP_COMPONENTS = {
    "BuildingListPanel": (778, 186, 2465, 1895),
    "NpcListPanel": (2982, 234, 4211, 1894),
    "ListItem": (626, 2258, 2737, 2975),
    "ConfirmPanel": (3118, 2146, 4313, 3055),
}


def checkerboard_alpha(image: Image.Image) -> Image.Image:
    rgb = np.asarray(image.convert("RGB"), dtype=np.uint8)
    channel_range = rgb.max(axis=2).astype(np.int16) - rgb.min(axis=2).astype(np.int16)
    channel_min = rgb.min(axis=2)
    channel_max = rgb.max(axis=2)
    checker_like = (channel_range <= 35) & (channel_min >= 80) & (channel_max <= 250)

    border_seed = np.zeros(checker_like.shape, dtype=bool)
    border_seed[0, :] = checker_like[0, :]
    border_seed[-1, :] = checker_like[-1, :]
    border_seed[:, 0] = checker_like[:, 0]
    border_seed[:, -1] = checker_like[:, -1]
    exterior = ndimage.binary_propagation(border_seed, mask=checker_like)
    foreground = ~exterior

    labels, count = ndimage.label(foreground)
    if count:
        component_sizes = np.bincount(labels.ravel())
        keep = np.zeros(count + 1, dtype=bool)
        minimum_area = max(24, int(foreground.size * 0.00001))
        keep[np.where(component_sizes >= minimum_area)[0]] = True
        keep[0] = False
        foreground = keep[labels]

    distance = ndimage.distance_transform_edt(foreground)
    alpha = np.clip(distance * 120.0, 0.0, 255.0).astype(np.uint8)
    rgba = np.dstack((rgb, alpha))
    return Image.fromarray(rgba, "RGBA")


def crop_component(source: Image.Image, bounds: tuple[int, int, int, int], padding: int = 18) -> Image.Image:
    left, top, right, bottom = bounds
    expanded = (
        max(0, left - padding),
        max(0, top - padding),
        min(source.width, right + padding),
        min(source.height, bottom + padding),
    )
    component = checkerboard_alpha(source.crop(expanded))
    rgba = np.asarray(component).copy()
    alpha_array = rgba[:, :, 3]
    labels, count = ndimage.label(alpha_array > 0)
    if count:
        sizes = np.bincount(labels.ravel())
        sizes[0] = 0
        largest = int(np.argmax(sizes))
        rgba[:, :, 3] = np.where(labels == largest, alpha_array, 0).astype(np.uint8)
        component = Image.fromarray(rgba, "RGBA")

    alpha = component.getchannel("A")
    visible = alpha.getbbox()
    if visible is None:
        raise RuntimeError(f"No visible pixels detected for {bounds}")
    trim = 8
    visible = (
        max(0, visible[0] - trim),
        max(0, visible[1] - trim),
        min(component.width, visible[2] + trim),
        min(component.height, visible[3] + trim),
    )
    return component.crop(visible)


def fit_component(image: Image.Image, scale: float) -> Image.Image:
    width = max(1, round(image.width * scale))
    height = max(1, round(image.height * scale))
    return image.resize((width, height), Image.Resampling.LANCZOS)


def pack_atlas(
    components: dict[str, Image.Image],
    atlas_size: tuple[int, int],
    padding: int = 16,
) -> tuple[Image.Image, dict[str, tuple[int, int, int, int]]]:
    atlas = Image.new("RGBA", atlas_size, (0, 0, 0, 0))
    placements: dict[str, tuple[int, int, int, int]] = {}
    cursor_x = padding
    cursor_y = padding
    row_height = 0

    for name, component in components.items():
        if cursor_x + component.width + padding > atlas.width:
            cursor_x = padding
            cursor_y += row_height + padding
            row_height = 0
        if cursor_y + component.height + padding > atlas.height:
            raise RuntimeError(f"Atlas {atlas_size} is too small for {name}")

        atlas.alpha_composite(component, (cursor_x, cursor_y))
        placements[name] = (cursor_x, cursor_y, component.width, component.height)
        cursor_x += component.width + padding
        row_height = max(row_height, component.height)

    return atlas, placements


def write_manifest(path: Path, placements: dict[str, tuple[int, int, int, int]]) -> None:
    lines = ["# x, y-from-top, width, height"]
    lines.extend(f"{name}={x},{y},{width},{height}" for name, (x, y, width, height) in placements.items())
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def build_base() -> None:
    source = Image.open(BASE_SOURCE).convert("RGB")
    base = crop_component(source, (83, 94, 5358, 3059), padding=24)
    target_width = 2048
    target_height = round(base.height * target_width / base.width)
    base = base.resize((target_width, target_height), Image.Resampling.LANCZOS)
    base.save(OUTPUT_ROOT / "LandInfoPanelBase.png", optimize=True)


def build_controls() -> None:
    source = Image.open(CONTROL_SOURCE).convert("RGB")
    components = {
        name: fit_component(crop_component(source, bounds), 0.42)
        for name, bounds in CONTROL_COMPONENTS.items()
    }
    atlas, placements = pack_atlas(components, (2048, 1536))
    atlas.save(OUTPUT_ROOT / "LandInfoPanelControls.png", optimize=True)
    write_manifest(OUTPUT_ROOT / "LandInfoPanelControls.txt", placements)


def build_popups() -> None:
    source = Image.open(POPUP_SOURCE).convert("RGB")
    components = {
        name: fit_component(crop_component(source, bounds), 0.48)
        for name, bounds in POPUP_COMPONENTS.items()
    }
    atlas, placements = pack_atlas(components, (2048, 1536))
    atlas.save(OUTPUT_ROOT / "LandInfoPanelPopups.png", optimize=True)
    write_manifest(OUTPUT_ROOT / "LandInfoPanelPopups.txt", placements)


def main() -> None:
    for source in (BASE_SOURCE, CONTROL_SOURCE, POPUP_SOURCE):
        if not source.exists():
            raise FileNotFoundError(source)

    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    build_base()
    build_controls()
    build_popups()

    for filename in ("LandInfoPanelBase.png", "LandInfoPanelControls.png", "LandInfoPanelPopups.png"):
        path = OUTPUT_ROOT / filename
        digest = hashlib.sha256(path.read_bytes()).hexdigest()[:16]
        with Image.open(path) as image:
            print(f"{filename}: {image.size}, {image.mode}, sha256={digest}")


if __name__ == "__main__":
    main()

