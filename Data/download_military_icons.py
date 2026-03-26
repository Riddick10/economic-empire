#!/usr/bin/env python3
"""
Military Icons Generator
Creates simple military unit icons for the game.
"""

import os
from PIL import Image, ImageDraw

# Output folder
OUTPUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Icons", "Military")
os.makedirs(OUTPUT_DIR, exist_ok=True)

ICON_SIZE = 32

def create_infantry_icon():
    """Create infantry soldier icon"""
    img = Image.new('RGBA', (ICON_SIZE, ICON_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Soldier silhouette
    # Head
    draw.ellipse([12, 2, 20, 10], fill=(60, 80, 60, 255))
    # Helmet
    draw.arc([11, 1, 21, 9], 180, 0, fill=(40, 60, 40, 255), width=2)
    # Body
    draw.rectangle([13, 10, 19, 22], fill=(60, 80, 60, 255))
    # Arms
    draw.line([13, 12, 8, 18], fill=(60, 80, 60, 255), width=3)
    draw.line([19, 12, 24, 18], fill=(60, 80, 60, 255), width=3)
    # Legs
    draw.line([14, 22, 12, 30], fill=(60, 80, 60, 255), width=3)
    draw.line([18, 22, 20, 30], fill=(60, 80, 60, 255), width=3)
    # Rifle
    draw.line([24, 16, 26, 6], fill=(80, 70, 50, 255), width=2)

    return img

def create_tank_icon():
    """Create tank icon"""
    img = Image.new('RGBA', (ICON_SIZE, ICON_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Tank body
    draw.rectangle([4, 16, 28, 26], fill=(80, 90, 70, 255))
    # Turret
    draw.ellipse([10, 10, 22, 20], fill=(70, 80, 60, 255))
    # Barrel
    draw.rectangle([20, 13, 30, 17], fill=(60, 70, 50, 255))
    # Tracks
    draw.ellipse([2, 22, 10, 30], fill=(50, 50, 50, 255))
    draw.ellipse([22, 22, 30, 30], fill=(50, 50, 50, 255))
    draw.rectangle([6, 24, 26, 28], fill=(50, 50, 50, 255))

    return img

def create_artillery_icon():
    """Create artillery icon"""
    img = Image.new('RGBA', (ICON_SIZE, ICON_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Base/platform
    draw.rectangle([6, 22, 26, 28], fill=(70, 70, 60, 255))
    # Wheels
    draw.ellipse([4, 24, 12, 32], fill=(50, 50, 50, 255))
    draw.ellipse([20, 24, 28, 32], fill=(50, 50, 50, 255))
    # Barrel (angled up)
    draw.polygon([(14, 20), (16, 4), (20, 6), (18, 20)], fill=(60, 60, 50, 255))
    # Base mechanism
    draw.rectangle([12, 18, 20, 24], fill=(80, 80, 70, 255))

    return img

def create_division_icon():
    """Create military division/army icon"""
    img = Image.new('RGBA', (ICON_SIZE, ICON_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Shield shape
    points = [(16, 2), (28, 8), (28, 20), (16, 30), (4, 20), (4, 8)]
    draw.polygon(points, fill=(100, 120, 80, 255), outline=(60, 80, 50, 255))

    # Star in center
    star_points = [
        (16, 8), (18, 14), (24, 14), (19, 18), (21, 24),
        (16, 20), (11, 24), (13, 18), (8, 14), (14, 14)
    ]
    draw.polygon(star_points, fill=(220, 200, 100, 255))

    return img

def create_recruiting_icon():
    """Create recruiting/training icon"""
    img = Image.new('RGBA', (ICON_SIZE, ICON_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Clock/timer circle
    draw.ellipse([4, 4, 28, 28], fill=(60, 80, 100, 255), outline=(40, 60, 80, 255))
    # Clock hands
    draw.line([16, 16, 16, 8], fill=(220, 220, 220, 255), width=2)
    draw.line([16, 16, 22, 16], fill=(220, 220, 220, 255), width=2)
    # Center dot
    draw.ellipse([14, 14, 18, 18], fill=(220, 220, 220, 255))
    # Small soldier silhouette
    draw.ellipse([12, 20, 16, 24], fill=(200, 200, 200, 150))
    draw.rectangle([13, 24, 15, 28], fill=(200, 200, 200, 150))

    return img

def create_army_marker():
    """Create army marker for map display"""
    img = Image.new('RGBA', (ICON_SIZE, ICON_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Circle background
    draw.ellipse([2, 2, 30, 30], fill=(80, 100, 60, 230), outline=(50, 70, 40, 255))

    # Simplified soldier shape
    # Head
    draw.ellipse([13, 6, 19, 12], fill=(220, 200, 180, 255))
    # Body
    draw.rectangle([14, 12, 18, 20], fill=(60, 80, 60, 255))
    # Rifle
    draw.line([18, 14, 22, 10], fill=(80, 70, 50, 255), width=2)
    # Legs
    draw.line([15, 20, 13, 26], fill=(60, 80, 60, 255), width=2)
    draw.line([17, 20, 19, 26], fill=(60, 80, 60, 255), width=2)

    return img

def main():
    print("=" * 50)
    print("Military Icons Generator")
    print("=" * 50)

    icons = {
        "infantry.png": create_infantry_icon(),
        "tank.png": create_tank_icon(),
        "artillery.png": create_artillery_icon(),
        "division.png": create_division_icon(),
        "recruiting.png": create_recruiting_icon(),
        "army_marker.png": create_army_marker(),
    }

    for filename, icon in icons.items():
        path = os.path.join(OUTPUT_DIR, filename)
        icon.save(path)
        print(f"Saved: {path}")

    print("\n" + "=" * 50)
    print("Military icons generation complete!")
    print("=" * 50)

if __name__ == "__main__":
    main()
