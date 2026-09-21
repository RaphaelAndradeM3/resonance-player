import os
import re
import colorsys
from PIL import Image

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MEDIA_DIR = os.path.join(REPO_ROOT, "media")
ASSETS_DIR = os.path.join(REPO_ROOT, "src", "Resonance.WinUI", "Assets")

def green_to_purple_hex(hex_str, target_hue=0.78):
    """Convert green hex color to purple hex color preserving lightness and saturation."""
    s = hex_str.lstrip('#')
    r, g, b = tuple(int(s[i:i+2], 16)/255.0 for i in (0, 2, 4))
    h, l, sat = colorsys.rgb_to_hls(r, g, b)
    # Green is ~0.38 (137 deg). Target purple is ~0.78 (280 deg)
    delta_h = h - 0.38
    new_h = (target_hue + delta_h * 0.7) % 1.0
    new_sat = min(1.0, sat * 1.08)
    r2, g2, b2 = colorsys.hls_to_rgb(new_h, l, new_sat)
    return '#{:02x}{:02x}{:02x}'.format(int(r2*255+0.5), int(g2*255+0.5), int(b2*255+0.5))

def process_svg():
    svg_path = os.path.join(MEDIA_DIR, "AppLogo.svg")
    with open(svg_path, "r", encoding="utf-8") as f:
        content = f.read()

    # 1. Update colors in <style>
    def replace_fill(m):
        prefix = m.group(1)
        hex_color = m.group(2)
        return prefix + green_to_purple_hex(hex_color)

    new_content = re.sub(r'(\.s\d+\s*\{\s*fill:\s*)(#[0-9a-fA-F]{6})', replace_fill, content)

    # 2. Invert geometry horizontally:
    # Wrap all <g id="..."> elements inside <g transform="translate(1024, 0) scale(-1, 1)">
    if '<g transform="translate(1024, 0) scale(-1, 1)">' not in new_content:
        # Find start of groups
        pattern = r'(</style>\s*)(<g id=)'
        replacement = r'\1<g transform="translate(1024, 0) scale(-1, 1)">\n    \2'
        new_content = re.sub(pattern, replacement, new_content)
        # Close the wrapper before </svg>
        new_content = re.sub(r'(</svg>)', r'    </g>\n\1', new_content)

    with open(svg_path, "w", encoding="utf-8") as f:
        f.write(new_content)
    print("Updated AppLogo.svg with inverted geometry and purple palette.")

def shift_image_green_to_purple(im):
    """Invert (flip horizontally) and shift green pixels to purple."""
    # Flip horizontally
    flipped = im.transpose(Image.FLIP_LEFT_RIGHT)
    rgba = flipped.convert("RGBA")
    data = list(rgba.getdata())
    new_data = []

    for r, g, b, a in data:
        if a == 0:
            new_data.append((r, g, b, a))
            continue

        h, l, s = colorsys.rgb_to_hls(r/255.0, g/255.0, b/255.0)
        # Check if color is in green spectrum (approx 0.20 to 0.48)
        if 0.20 <= h <= 0.48 and s > 0.10:
            delta_h = h - 0.38
            new_h = (0.78 + delta_h * 0.7) % 1.0
            new_s = min(1.0, s * 1.08)
            nr, ng, nb = colorsys.hls_to_rgb(new_h, l, new_s)
            new_data.append((int(nr*255+0.5), int(ng*255+0.5), int(nb*255+0.5), a))
        else:
            new_data.append((r, g, b, a))

    res = Image.new("RGBA", rgba.size)
    res.putdata(new_data)
    return res

def process_png_assets():
    # 1. Update media/AppLogo.png
    media_logo_path = os.path.join(MEDIA_DIR, "AppLogo.png")
    orig_logo = Image.open(media_logo_path)
    purple_logo = shift_image_green_to_purple(orig_logo)
    purple_logo.save(media_logo_path, format="PNG")
    print("Updated media/AppLogo.png")

    # 2. Update src/Resonance.WinUI/Assets/AppLogo.png
    assets_logo_path = os.path.join(ASSETS_DIR, "AppLogo.png")
    purple_logo.save(assets_logo_path, format="PNG")
    print("Updated Assets/AppLogo.png")

    # 3. Update src/Resonance.WinUI/Assets/AppLogo.ico
    ico_path = os.path.join(ASSETS_DIR, "AppLogo.ico")
    icon_sizes = [(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)]
    purple_logo.save(ico_path, format="ICO", sizes=icon_sizes)
    print("Updated Assets/AppLogo.ico with multiple sizes.")

    # 4. Update all other PNG assets in Assets directory
    count = 0
    for filename in os.listdir(ASSETS_DIR):
        if filename.endswith(".png") and filename != "AppLogo.png":
            file_path = os.path.join(ASSETS_DIR, filename)
            try:
                asset_img = Image.open(file_path)
                updated_asset = shift_image_green_to_purple(asset_img)
                updated_asset.save(file_path, format="PNG")
                count += 1
            except Exception as ex:
                print(f"Failed to update {filename}: {ex}")

    print(f"Updated {count} tile/logo assets in Assets directory.")

if __name__ == "__main__":
    process_svg()
    process_png_assets()
