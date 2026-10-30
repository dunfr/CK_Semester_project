"""Generate a linear cylindrical hair control map, not a color or lighting bake.

R = highlight coverage, G = band height offset, B = width variation.
U wraps around the head; V is head height. Run from the Unity project root.
"""
from pathlib import Path
import numpy as np
from PIL import Image

width, height = 512, 256
v, u = np.mgrid[0:height, 0:width].astype(np.float32)
u /= width
# PNG's first row becomes the top of Unity's UV space.
v = 1 - v / (height - 1)
angle = u * np.pi * 2
strand_phase = angle * 19 + .55 * np.sin(angle * 5) + v * .8
seams = (.5 + .5 * np.cos(strand_phase)) ** 14
clumps = .72 + .18 * np.sin(angle * 7 + .4) + .10 * np.sin(angle * 3 - .7)
coverage = np.clip(clumps * (1 - .90 * seams), 0, 1)
height_offset = .50 + .20 * np.sin(angle * 7 + .6) + .10 * np.sin(angle * 13)
band_width = .48 + .20 * np.cos(angle * 7 - .3) + .08 * np.sin(angle * 11)
array = np.stack([coverage, height_offset, band_width], axis=-1)
output = Path('Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading/Textures')
Image.fromarray(np.round(np.clip(array, 0, 1) * 255).astype(np.uint8)).save(
    output / 'character_fuchsia_hair_control_img.png')
print('Generated cylindrical hair highlight control map')
