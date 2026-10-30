"""Analytic cheek/nose shadow threshold field, sampled in head projection UVs.

This is a linear data map, not face color or baked scene lighting.
Run from the Unity project root with Python, NumPy, Pillow installed.
"""
from pathlib import Path
import numpy as np
from PIL import Image

size = 256
y, x = np.mgrid[0:size, 0:size].astype(np.float32) / (size - 1)
x = x * 2 - 1
y = 1 - y * 2  # PNG top row corresponds to Unity UV v=1.
# Smooth cheek edge with a small offset around the nose. Mirroring handles either side.
cheek = .095 * np.exp(-((y + .18) / .30) ** 2)
nose = .18 * np.exp(-(x / .17) ** 2 - ((y + .05) / .13) ** 2)
jaw = -.06 * np.exp(-((y + .62) / .20) ** 2)
threshold = np.clip(.50 + x * .46 + cheek - nose + jaw, .015, .985)
output = Path('Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading/Textures')
Image.fromarray((threshold * 255).round().astype(np.uint8)).save(
    output / 'character_fuchsia_face_shadow_mask_img.png')
print('Generated analytic face shadow threshold map')
