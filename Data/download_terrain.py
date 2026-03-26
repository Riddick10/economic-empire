#!/usr/bin/env python3
"""
Terrain Data Generator
Creates realistic elevation and land cover textures for the game map.
Generates heightmap, relief shading, and terrain type overlays.
Uses Natural Earth data for accurate land/ocean boundaries.
"""

import os
import zipfile
import requests
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance
import numpy as np

# Try to import shapefile libraries
try:
    import shapefile
    from shapely.geometry import shape, Point, box
    from shapely.prepared import prep
    SHAPEFILE_AVAILABLE = True
except ImportError:
    SHAPEFILE_AVAILABLE = False
    print("Warning: shapefile/shapely not available, using fallback land mask")

# Output folder
OUTPUT_DIR = os.path.dirname(os.path.abspath(__file__))
TERRAIN_DIR = os.path.join(OUTPUT_DIR, "Terrain")

# Create terrain folder
os.makedirs(TERRAIN_DIR, exist_ok=True)

# Map dimensions (matching game)
MAP_WIDTH = 2000
MAP_HEIGHT = 1000

# Geographic bounds (matching game)
MIN_LON = -180
MAX_LON = 180
MIN_LAT = -60
MAX_LAT = 85


def geo_to_pixel(lon, lat):
    """Convert geographic coordinates to pixel coordinates"""
    px = int((lon - MIN_LON) / (MAX_LON - MIN_LON) * MAP_WIDTH)
    py = int((MAX_LAT - lat) / (MAX_LAT - MIN_LAT) * MAP_HEIGHT)
    return px, py


def download_natural_earth_land():
    """Download Natural Earth land shapefile if not present"""
    land_dir = os.path.join(OUTPUT_DIR, "NaturalEarth")
    os.makedirs(land_dir, exist_ok=True)

    shp_file = os.path.join(land_dir, "ne_10m_land.shp")

    if os.path.exists(shp_file):
        print("Natural Earth land data already downloaded")
        return shp_file

    print("Downloading Natural Earth land data (10m resolution)...")
    url = "https://naciscdn.org/naturalearth/10m/physical/ne_10m_land.zip"

    zip_path = os.path.join(land_dir, "ne_10m_land.zip")

    response = requests.get(url, stream=True)
    response.raise_for_status()

    with open(zip_path, 'wb') as f:
        for chunk in response.iter_content(chunk_size=8192):
            f.write(chunk)

    print("Extracting shapefile...")
    with zipfile.ZipFile(zip_path, 'r') as zip_ref:
        zip_ref.extractall(land_dir)

    os.remove(zip_path)
    print("Natural Earth land data ready")

    return shp_file


def create_land_mask():
    """Create a precise land/ocean mask using Natural Earth data.
    Returns a numpy array where True = land, False = ocean"""
    print("Creating land mask...")

    # Check for cached mask
    mask_cache = os.path.join(TERRAIN_DIR, "land_mask.png")
    if os.path.exists(mask_cache):
        print("Loading cached land mask...")
        mask_img = Image.open(mask_cache).convert('L')
        return np.array(mask_img) > 128

    if not SHAPEFILE_AVAILABLE:
        print("ERROR: shapefile/shapely not available!")
        print("Install with: pip install pyshp shapely")
        raise ImportError("Required packages not available")

    # Download Natural Earth data
    shp_file = download_natural_earth_land()

    print("Loading land polygons...")
    sf = shapefile.Reader(shp_file)

    # Combine all land polygons
    land_shapes = []
    for shape_rec in sf.shapeRecords():
        geom = shape(shape_rec.shape.__geo_interface__)
        if geom.is_valid:
            land_shapes.append(geom)
        else:
            # Try to fix invalid geometries
            geom = geom.buffer(0)
            if geom.is_valid:
                land_shapes.append(geom)

    print(f"Loaded {len(land_shapes)} land polygons")

    # Create union of all land (this is the slow part)
    print("Creating land union (this may take a moment)...")
    from shapely.ops import unary_union
    land_union = unary_union(land_shapes)
    prepared_land = prep(land_union)

    # Create mask image
    print("Rasterizing land mask...")
    mask = np.zeros((MAP_HEIGHT, MAP_WIDTH), dtype=np.uint8)

    # Process in chunks for progress reporting
    chunk_size = 50
    total_rows = MAP_HEIGHT

    for y_start in range(0, MAP_HEIGHT, chunk_size):
        y_end = min(y_start + chunk_size, MAP_HEIGHT)

        for y in range(y_start, y_end):
            lat = MAX_LAT - (y / MAP_HEIGHT) * (MAX_LAT - MIN_LAT)

            for x in range(MAP_WIDTH):
                lon = MIN_LON + (x / MAP_WIDTH) * (MAX_LON - MIN_LON)

                point = Point(lon, lat)
                if prepared_land.contains(point):
                    mask[y, x] = 255

        progress = (y_end / total_rows) * 100
        print(f"  Progress: {progress:.0f}%", end='\r')

    print("  Progress: 100%")

    # Save mask for caching
    mask_img = Image.fromarray(mask, 'L')
    mask_img.save(mask_cache)
    print(f"Saved land mask cache to {mask_cache}")

    return mask > 128


def create_procedural_heightmap():
    """Create a procedural heightmap based on real-world elevation patterns"""
    print("Creating procedural heightmap...")

    img = Image.new('L', (MAP_WIDTH, MAP_HEIGHT), 100)
    pixels = np.array(img, dtype=np.float32)

    # Comprehensive list of mountain ranges with realistic parameters
    # Format: center (lon, lat), radius (degrees), height (0-255), spread, elongation (x, y scale)
    mountain_ranges = [
        # === EUROPE ===
        # Alps - main chain
        {"center": (10, 46.5), "radius": 4, "height": 235, "spread": 3, "elongation": (2.5, 1)},
        # Western Alps (France/Italy)
        {"center": (7, 45), "radius": 2, "height": 230, "spread": 2, "elongation": (1.2, 1.5)},
        # Eastern Alps (Austria)
        {"center": (13, 47), "radius": 3, "height": 220, "spread": 2.5, "elongation": (2, 1)},
        # Pyrenees
        {"center": (0, 42.7), "radius": 3, "height": 210, "spread": 2, "elongation": (3, 1)},
        # Carpathians - Arc shape
        {"center": (25, 47), "radius": 5, "height": 185, "spread": 3, "elongation": (1.5, 1.2)},
        {"center": (22, 45), "radius": 3, "height": 175, "spread": 2.5, "elongation": (1.2, 1.5)},
        # Scandinavian Mountains
        {"center": (8, 62), "radius": 4, "height": 175, "spread": 4, "elongation": (1, 4)},
        {"center": (15, 68), "radius": 3, "height": 165, "spread": 3, "elongation": (1, 3)},
        # Scottish Highlands
        {"center": (-5, 57), "radius": 2, "height": 145, "spread": 2, "elongation": (1.5, 1)},
        # Apennines (Italy)
        {"center": (13, 42), "radius": 2.5, "height": 165, "spread": 2, "elongation": (1, 3)},
        # Balkans/Dinaric Alps
        {"center": (18, 44), "radius": 3, "height": 170, "spread": 2.5, "elongation": (1, 2)},
        # Pindus (Greece)
        {"center": (21, 39.5), "radius": 2, "height": 160, "spread": 2, "elongation": (1, 1.5)},

        # === ASIA ===
        # Ural Mountains
        {"center": (60, 58), "radius": 3, "height": 155, "spread": 4, "elongation": (1, 5)},
        {"center": (59, 52), "radius": 2.5, "height": 150, "spread": 3, "elongation": (1, 3)},
        # Caucasus - major range
        {"center": (43, 42.5), "radius": 4, "height": 230, "spread": 2, "elongation": (3.5, 1)},
        # Greater Caucasus
        {"center": (45, 43), "radius": 3, "height": 220, "spread": 2, "elongation": (2.5, 1)},
        # Himalayas - the roof of the world
        {"center": (85, 28), "radius": 6, "height": 255, "spread": 4, "elongation": (4, 1)},
        {"center": (78, 32), "radius": 4, "height": 250, "spread": 3, "elongation": (2.5, 1)},
        {"center": (92, 27), "radius": 5, "height": 248, "spread": 3.5, "elongation": (3, 1)},
        # Hindu Kush
        {"center": (70, 36), "radius": 4, "height": 235, "spread": 3, "elongation": (2.5, 1)},
        # Karakoram
        {"center": (76, 36), "radius": 3, "height": 250, "spread": 2.5, "elongation": (2, 1)},
        # Tibetan Plateau (high elevation base)
        {"center": (90, 33), "radius": 12, "height": 200, "spread": 8, "elongation": (2, 1)},
        {"center": (85, 35), "radius": 8, "height": 190, "spread": 6, "elongation": (1.8, 1)},
        # Kunlun Mountains
        {"center": (85, 36), "radius": 5, "height": 225, "spread": 4, "elongation": (4, 1)},
        # Tian Shan
        {"center": (78, 42), "radius": 5, "height": 215, "spread": 4, "elongation": (4, 1)},
        # Altai Mountains
        {"center": (88, 49), "radius": 4, "height": 195, "spread": 3, "elongation": (2.5, 1.5)},
        # Sayan Mountains
        {"center": (96, 52), "radius": 3, "height": 175, "spread": 3, "elongation": (2, 1)},
        # Verkhoyansk Range
        {"center": (130, 67), "radius": 4, "height": 155, "spread": 5, "elongation": (1, 3)},
        # Chersky Range
        {"center": (145, 65), "radius": 3, "height": 150, "spread": 4, "elongation": (1.5, 2)},
        # Japanese Alps
        {"center": (138, 36), "radius": 2, "height": 185, "spread": 1.5, "elongation": (1, 2)},
        # Taiwan Mountains
        {"center": (121, 24), "radius": 1.5, "height": 175, "spread": 1, "elongation": (1, 2)},
        # Western Ghats (India)
        {"center": (75, 12), "radius": 2, "height": 155, "spread": 2.5, "elongation": (1, 5)},
        # Eastern Ghats
        {"center": (80, 15), "radius": 1.5, "height": 140, "spread": 2, "elongation": (1, 4)},
        # Zagros Mountains (Iran)
        {"center": (50, 33), "radius": 4, "height": 190, "spread": 3, "elongation": (1, 3)},
        # Elburz Mountains (Iran)
        {"center": (52, 36), "radius": 3, "height": 200, "spread": 2, "elongation": (3, 1)},

        # === AFRICA ===
        # Atlas Mountains
        {"center": (-5, 32), "radius": 4, "height": 180, "spread": 3, "elongation": (3, 1)},
        {"center": (0, 34), "radius": 3, "height": 170, "spread": 2.5, "elongation": (2.5, 1)},
        # Ethiopian Highlands
        {"center": (38, 9), "radius": 5, "height": 190, "spread": 4, "elongation": (1.5, 1.5)},
        {"center": (40, 7), "radius": 3, "height": 185, "spread": 3, "elongation": (1.2, 1.5)},
        # East African Rift highlands
        {"center": (36, -3), "radius": 3, "height": 175, "spread": 3, "elongation": (1, 2)},
        # Rwenzori Mountains
        {"center": (30, 0.5), "radius": 1.5, "height": 190, "spread": 1.5, "elongation": (1, 1)},
        # Drakensberg
        {"center": (29, -29), "radius": 3, "height": 165, "spread": 3, "elongation": (1, 2)},
        # Hoggar Mountains
        {"center": (6, 23), "radius": 2.5, "height": 150, "spread": 3, "elongation": (1, 1)},
        # Tibesti Mountains
        {"center": (18, 21), "radius": 2, "height": 155, "spread": 2.5, "elongation": (1, 1)},

        # === NORTH AMERICA ===
        # Rocky Mountains - extensive chain
        {"center": (-110, 45), "radius": 6, "height": 210, "spread": 5, "elongation": (1, 4)},
        {"center": (-105, 40), "radius": 5, "height": 215, "spread": 4, "elongation": (1.5, 3)},
        {"center": (-115, 50), "radius": 5, "height": 205, "spread": 4, "elongation": (1, 3)},
        {"center": (-120, 55), "radius": 4, "height": 195, "spread": 3.5, "elongation": (1, 2.5)},
        # Sierra Nevada
        {"center": (-119, 37), "radius": 3, "height": 200, "spread": 2.5, "elongation": (1, 2.5)},
        # Cascade Range
        {"center": (-121, 44), "radius": 2.5, "height": 195, "spread": 2.5, "elongation": (1, 3)},
        # Coast Mountains (Canada)
        {"center": (-125, 52), "radius": 3, "height": 185, "spread": 3, "elongation": (1, 2.5)},
        # Alaska Range
        {"center": (-150, 63), "radius": 4, "height": 225, "spread": 3, "elongation": (2.5, 1)},
        # Brooks Range
        {"center": (-152, 68), "radius": 3, "height": 165, "spread": 4, "elongation": (3, 1)},
        # Appalachians
        {"center": (-80, 38), "radius": 4, "height": 145, "spread": 3.5, "elongation": (1, 4)},
        {"center": (-83, 35), "radius": 3, "height": 150, "spread": 3, "elongation": (1, 2.5)},
        # Sierra Madre Oriental
        {"center": (-100, 24), "radius": 3, "height": 165, "spread": 3, "elongation": (1, 3)},
        # Sierra Madre Occidental
        {"center": (-106, 26), "radius": 3, "height": 170, "spread": 3, "elongation": (1, 3)},

        # === SOUTH AMERICA ===
        # Andes - longest continental range
        {"center": (-70, -15), "radius": 4, "height": 235, "spread": 3, "elongation": (1, 5)},
        {"center": (-68, -23), "radius": 4, "height": 240, "spread": 3, "elongation": (1, 4)},
        {"center": (-70, -33), "radius": 3.5, "height": 245, "spread": 3, "elongation": (1, 3.5)},
        {"center": (-72, -42), "radius": 3, "height": 210, "spread": 3, "elongation": (1, 3)},
        {"center": (-68, -5), "radius": 3.5, "height": 220, "spread": 3, "elongation": (1, 3)},
        {"center": (-75, 0), "radius": 3, "height": 225, "spread": 2.5, "elongation": (1, 2.5)},
        {"center": (-73, 5), "radius": 3, "height": 215, "spread": 2.5, "elongation": (1.5, 2)},
        # Patagonian Ice Field
        {"center": (-73, -49), "radius": 2.5, "height": 185, "spread": 2.5, "elongation": (1, 2)},
        # Brazilian Highlands
        {"center": (-45, -18), "radius": 5, "height": 140, "spread": 5, "elongation": (1.5, 1)},
        # Guiana Highlands
        {"center": (-62, 5), "radius": 3, "height": 150, "spread": 3.5, "elongation": (1.5, 1)},

        # === OCEANIA ===
        # Australian Alps
        {"center": (148, -36.5), "radius": 2, "height": 145, "spread": 2, "elongation": (2, 1)},
        # Great Dividing Range
        {"center": (150, -30), "radius": 2.5, "height": 135, "spread": 3, "elongation": (1, 4)},
        # New Zealand Southern Alps
        {"center": (170, -43.5), "radius": 1.5, "height": 175, "spread": 1.5, "elongation": (1, 2.5)},
        # New Guinea Highlands
        {"center": (145, -5), "radius": 3, "height": 185, "spread": 2.5, "elongation": (3, 1)},

        # === ANTARCTICA (visible part) ===
        {"center": (0, -75), "radius": 8, "height": 180, "spread": 6, "elongation": (1.5, 1)},
    ]

    for mt in mountain_ranges:
        cx, cy = mt["center"]
        px, py = geo_to_pixel(cx, cy)

        # Convert degrees to pixels (approximate)
        scale_x = MAP_WIDTH / 360
        scale_y = MAP_HEIGHT / 145

        radius = mt["radius"] * scale_x
        spread = mt["spread"] * scale_x
        height = mt["height"]
        elongation = mt.get("elongation", (1, 1))

        # Calculate bounding box
        max_extent = int((radius + spread) * max(elongation))
        y_start = max(0, py - max_extent)
        y_end = min(MAP_HEIGHT, py + max_extent)
        x_start = max(0, px - max_extent)
        x_end = min(MAP_WIDTH, px + max_extent)

        # Draw mountain influence
        for y in range(y_start, y_end):
            for x in range(x_start, x_end):
                # Calculate elliptical distance
                dx = (x - px) / elongation[0]
                dy = (y - py) / elongation[1]
                dist = np.sqrt(dx**2 + dy**2)

                if dist < radius:
                    # Core mountain area - smooth falloff from center
                    factor = 1.0 - (dist / radius) ** 1.5 * 0.35
                    new_height = height * factor
                    pixels[y, x] = max(pixels[y, x], new_height)
                elif dist < radius + spread:
                    # Gradual falloff area
                    falloff = 1.0 - (dist - radius) / spread
                    falloff = falloff ** 1.5  # Smoother falloff curve
                    influence = height * 0.55 * falloff
                    pixels[y, x] = max(pixels[y, x], 100 + influence * 0.6)

    # Add Perlin-like noise for natural variation
    np.random.seed(42)  # For reproducibility

    # Multi-octave noise
    for octave in range(4):
        scale = 2 ** octave
        amplitude = 12 / scale
        noise_size = (MAP_HEIGHT // (4 * scale) + 1, MAP_WIDTH // (4 * scale) + 1)
        small_noise = np.random.normal(0, 1, noise_size)
        noise = np.array(Image.fromarray(small_noise).resize((MAP_WIDTH, MAP_HEIGHT), Image.BILINEAR))
        pixels += noise * amplitude

    pixels = np.clip(pixels, 0, 255)

    # Smooth the result
    img = Image.fromarray(pixels.astype(np.uint8), 'L')
    img = img.filter(ImageFilter.GaussianBlur(radius=2))

    return img


def create_terrain_types():
    """Create a terrain type map (forests, farmland, desert, etc.)"""
    print("Creating terrain type map...")

    # Create RGBA image for terrain types
    # R = Forest intensity
    # G = Farmland/grassland intensity
    # B = Desert/barren intensity
    # A = Snow/ice intensity

    img = Image.new('RGBA', (MAP_WIDTH, MAP_HEIGHT), (50, 80, 30, 0))
    pixels = np.array(img, dtype=np.float32)

    np.random.seed(43)  # For reproducibility

    # Define biomes based on latitude and longitude with more detail
    for y in range(MAP_HEIGHT):
        lat = MAX_LAT - (y / MAP_HEIGHT) * (MAX_LAT - MIN_LAT)

        for x in range(MAP_WIDTH):
            lon = MIN_LON + (x / MAP_WIDTH) * (MAX_LON - MIN_LON)

            # Base biome by latitude
            forest = 0
            farmland = 0
            desert = 0
            snow = 0

            # High polar regions (Arctic)
            if lat > 75:
                snow = 220
                desert = 40  # Barren ice

            # Antarctic (visible part)
            elif lat < -55:
                snow = 200
                desert = 50

            # Arctic/Tundra
            elif lat > 65:
                snow = 100 + (lat - 65) * 12
                forest = max(0, 80 - (lat - 65) * 8)
                farmland = 20

            # Boreal/Taiga (Northern forests)
            elif lat > 50:
                # Great boreal forests
                if -170 < lon < -50 or 20 < lon < 180:  # North America & Eurasia
                    forest = 160
                    farmland = 40
                    snow = max(0, (lat - 55) * 15) if lat > 55 else 0
                else:
                    forest = 100
                    farmland = 60

            # Temperate zones
            elif lat > 35:
                # Temperate rainforests
                if -130 < lon < -120 and lat > 40:  # Pacific Northwest
                    forest = 180
                    farmland = 40
                elif 0 < lon < 30:  # Western Europe
                    forest = 110
                    farmland = 130
                elif 100 < lon < 145:  # East Asia
                    forest = 120
                    farmland = 100
                else:
                    forest = 90
                    farmland = 100

            # Subtropical - important desert belt
            elif lat > 20:
                # Great deserts
                is_desert = False

                # Sahara Desert
                if -15 < lon < 35 and 18 < lat < 32:
                    is_desert = True
                    desert = 220

                # Arabian Desert
                elif 35 < lon < 60 and 15 < lat < 32:
                    is_desert = True
                    desert = 210

                # Thar Desert (India/Pakistan)
                elif 68 < lon < 76 and 24 < lat < 30:
                    is_desert = True
                    desert = 180

                # Sonoran/Chihuahuan Desert
                elif -120 < lon < -100 and 25 < lat < 35:
                    is_desert = True
                    desert = 170

                # Gobi Desert
                elif 95 < lon < 115 and 38 < lat < 48:
                    is_desert = True
                    desert = 160

                # Australian Outback
                elif 120 < lon < 145 and -30 < lat < -20:
                    is_desert = True
                    desert = 190

                if not is_desert:
                    # Mediterranean climate
                    if (-10 < lon < 40 and 30 < lat < 45) or (115 < lon < 125 and 30 < lat < 40):
                        forest = 70
                        farmland = 120
                        desert = 30
                    else:
                        forest = 80
                        farmland = 100

            # Tropical zones
            else:
                # Tropical rainforests
                is_rainforest = False

                # Amazon Basin
                if -80 < lon < -45 and -15 < lat < 5:
                    is_rainforest = True
                    forest = 240
                    farmland = 20

                # Congo Basin
                elif 10 < lon < 35 and -10 < lat < 5:
                    is_rainforest = True
                    forest = 230
                    farmland = 25

                # Southeast Asian rainforests
                elif 95 < lon < 155 and -10 < lat < 15:
                    is_rainforest = True
                    forest = 220
                    farmland = 30

                # Central American rainforests
                elif -95 < lon < -75 and 5 < lat < 20:
                    is_rainforest = True
                    forest = 200
                    farmland = 40

                if not is_rainforest:
                    # Savanna/grasslands
                    if (10 < lon < 40 and 5 < lat < 15) or (15 < lon < 45 and -20 < lat < -5):  # African savanna
                        forest = 50
                        farmland = 130
                        desert = 40
                    else:
                        forest = 100
                        farmland = 90

            # Additional desert regions in southern hemisphere
            if -70 < lon < -65 and -30 < lat < -20:  # Atacama
                desert = 230
                farmland = 10
                forest = 5
            elif 15 < lon < 25 and -30 < lat < -20:  # Namib/Kalahari
                desert = 200
                farmland = 30
                forest = 20

            # Add some noise for natural variation
            noise_forest = np.random.normal(0, 12)
            noise_farm = np.random.normal(0, 10)
            noise_desert = np.random.normal(0, 8)

            pixels[y, x, 0] = np.clip(forest + noise_forest, 0, 255)
            pixels[y, x, 1] = np.clip(farmland + noise_farm, 0, 255)
            pixels[y, x, 2] = np.clip(desert + noise_desert, 0, 255)
            pixels[y, x, 3] = np.clip(snow, 0, 255)

    # Apply blur for smooth transitions
    img = Image.fromarray(pixels.astype(np.uint8), 'RGBA')

    # Blur each channel separately
    r, g, b, a = img.split()
    r = r.filter(ImageFilter.GaussianBlur(radius=6))
    g = g.filter(ImageFilter.GaussianBlur(radius=6))
    b = b.filter(ImageFilter.GaussianBlur(radius=6))
    a = a.filter(ImageFilter.GaussianBlur(radius=5))

    img = Image.merge('RGBA', (r, g, b, a))

    return img


def create_relief_shading(heightmap, land_mask):
    """Create relief shading from heightmap using balanced hillshade algorithm
    Ocean areas (where land_mask is False) are set to white (255) so multiply blend has no effect"""
    print("Creating relief shading...")

    pixels = np.array(heightmap, dtype=np.float32)

    # Calculate gradients for hillshade effect
    # Light from northwest (standard cartographic convention)
    gradient_x = np.gradient(pixels, axis=1)
    gradient_y = np.gradient(pixels, axis=0)

    # Light direction (azimuth 315 degrees, altitude 50 degrees - balanced)
    azimuth = np.radians(315)
    altitude = np.radians(50)  # Balanced light angle

    # Calculate slope and aspect - moderate divisor for visible but not extreme terrain
    slope = np.arctan(np.sqrt(gradient_x**2 + gradient_y**2) / 12)  # Balanced (between 8 and 20)
    aspect = np.arctan2(-gradient_y, gradient_x)

    # Calculate hillshade using standard formula
    hillshade = (np.sin(altitude) * np.cos(slope) +
                 np.cos(altitude) * np.sin(slope) * np.cos(azimuth - aspect))

    # Normalize to 0-255 with moderate contrast
    hillshade = (hillshade + 1) / 2  # Range 0-1

    # Balanced range - visible but not extreme
    hillshade = 0.30 + hillshade * 0.45  # Range 0.30-0.75 (77-191 in 8-bit)

    hillshade = np.clip(hillshade * 255, 0, 255)

    # Erst Image erstellen und Kontrast anwenden (nur auf Land)
    img = Image.fromarray(hillshade.astype(np.uint8), 'L')
    enhancer = ImageEnhance.Contrast(img)
    img = enhancer.enhance(1.08)

    # Ocean-Maske anwenden - nutze echte Land-Maske statt Heightmap-Werte
    img_array = np.array(img)
    ocean_mask = ~land_mask  # Invert: True wo Meer ist
    img_array[ocean_mask] = 255  # Weiss = keine Verdunkelung im Multiply-Blend

    return Image.fromarray(img_array, 'L')


def create_combined_terrain():
    """Create a combined terrain texture with all features"""
    print("Creating combined terrain texture...")

    land_mask = create_land_mask()
    heightmap = create_procedural_heightmap()
    terrain_types = create_terrain_types()
    relief = create_relief_shading(heightmap, land_mask)

    # Create final combined texture
    base = terrain_types.convert('RGBA')
    relief_rgba = relief.convert('RGBA')

    # Blend relief with terrain colors
    base_pixels = np.array(base, dtype=np.float32)
    relief_pixels = np.array(relief_rgba, dtype=np.float32)[:, :, 0:1]

    # Modulate terrain colors by relief
    relief_factor = relief_pixels / 128.0  # Normalize around 1.0

    # Apply relief shading to RGB channels - stronger effect
    base_pixels[:, :, 0:3] = np.clip(
        base_pixels[:, :, 0:3] * relief_factor * 0.65 + base_pixels[:, :, 0:3] * 0.35,
        0, 255
    )

    combined = Image.fromarray(base_pixels.astype(np.uint8), 'RGBA')

    return combined, heightmap, relief


def main():
    print("=" * 50)
    print("Terrain Data Generator - Enhanced Edition")
    print("=" * 50)

    # Generate all terrain data
    combined, heightmap, relief = create_combined_terrain()

    # Save files
    heightmap_path = os.path.join(TERRAIN_DIR, "heightmap.png")
    relief_path = os.path.join(TERRAIN_DIR, "relief.png")
    combined_path = os.path.join(TERRAIN_DIR, "terrain.png")

    heightmap.save(heightmap_path)
    print(f"Saved: {heightmap_path}")

    relief.save(relief_path)
    print(f"Saved: {relief_path}")

    combined.save(combined_path)
    print(f"Saved: {combined_path}")

    # Create a simple terrain legend/info
    print("\nTerrain types in terrain.png:")
    print("  R channel: Forest density (green areas)")
    print("  G channel: Farmland/grassland density")
    print("  B channel: Desert/barren density (brown/yellow areas)")
    print("  A channel: Snow/ice density (white areas)")
    print("\nAll files saved to:", TERRAIN_DIR)

    print("\n" + "=" * 50)
    print("Terrain generation complete!")
    print("=" * 50)


if __name__ == "__main__":
    main()
