"""Generate deterministic, tileable PBR surface maps for the corridor test.

Requires Python, NumPy and Pillow. Run from the Unity project root.
Packed mask: R=metallic, G=occlusion, B=unused, A=smoothness.
These maps encode surface properties only, not baked directional lighting.
"""
from pathlib import Path
import numpy as np
from PIL import Image

OUTPUT = Path('Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading/Textures/Corridor')
SIZE = 512
OUTPUT.mkdir(parents=True, exist_ok=True)
y, x = np.mgrid[0:SIZE, 0:SIZE].astype(np.float32) / SIZE
rng = np.random.default_rng(22018)
noise = rng.random((SIZE, SIZE)).astype(np.float32)
coarse = (np.sin(x * np.pi * 14) * np.sin(y * np.pi * 10) + 1) * .5
edge = np.minimum.reduce([x, y, 1-x, 1-y])
seam = np.exp(-np.square((edge-.018)/.006))
brush = .5 + .5*np.sin(y*np.pi*360 + np.sin(x*np.pi*4)*.12)


def write_surface(name, base, metal, smooth, height, occlusion):
    def plane(value):
        return np.broadcast_to(value, (SIZE, SIZE)).astype(np.float32)
    base, metal, smooth, height, occlusion = map(plane, [base, metal, smooth, height, occlusion])
    # Central differences wrap at the edge for seamless repeating surface normals.
    dx = (np.roll(height, -1, axis=1)-np.roll(height, 1, axis=1)) * SIZE*.5
    dy = (np.roll(height, -1, axis=0)-np.roll(height, 1, axis=0)) * SIZE*.5
    normal = np.stack([-dx, dy, np.ones_like(dx)], axis=-1)
    normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
    normal = normal*.5+.5
    albedo = np.stack([base, base, base], axis=-1)
    mask = np.stack([metal, occlusion, np.zeros_like(metal), smooth], axis=-1)
    for suffix, array in [('base', albedo), ('mask', mask), ('normal', normal)]:
        path = OUTPUT / f'object_corridor_{name}_{suffix}_img.png'
        Image.fromarray((np.clip(array, 0, 1)*255).round().astype(np.uint8)).save(path)


# Painted wall: small paint texture and a recessed panel joint. Paint is dielectric.
chips = ((edge < .026) & (noise > .992)).astype(np.float32)
base = .94 + (noise-.5)*.02 - seam*.10 - chips*.22
write_surface('wall', base, chips*.85, .30+(noise-.5)*.05-chips*.08,
              (noise-.5)*.00010-seam*.0015, 1-seam*.14)

# Painted shell and metallic inset are separate finishes, rather than one metal wall.
# Separate maps preserve the rough ceiling coating that uses the original wall set.
write_surface('wall_coated', .96+(noise-.5)*.008-seam*.08-chips*.18,
              chips*.85, .59+(noise-.5)*.018-chips*.16,
              (noise-.5)*.000025-seam*.0012, 1-seam*.12)

# Steel floor: narrow joints, fasteners, shallow brushing, scuffs in the walking area.
rivets = np.zeros_like(x)
for px, py in [(.06,.06),(.94,.06),(.06,.94),(.94,.94)]:
    d = np.sqrt((x-px)**2+(y-py)**2)
    rivets += np.exp(-np.square(d/.009))
scuffs = np.zeros_like(x)
for _ in range(28):
    cx, cy = rng.uniform(.08,.92,2)
    length = rng.uniform(.025,.16)
    stroke = np.exp(-np.square((x-cx)/length)-np.square((y-cy)/.0015))
    scuffs = np.maximum(scuffs, stroke)
base = .73+(noise-.5)*.025+(brush-.5)*.025-seam*.16-rivets*.27+scuffs*.06
write_surface('floor', base, .94-seam*.15, .80+(brush-.5)*.04-scuffs*.38-rivets*.24,
              (brush-.5)*.00002-seam*.0016-rivets*.0008, 1-seam*.20-rivets*.18)

# Exposed aluminum: fine directional microstructure, almost no diffuse response.
write_surface('trim', .87+(brush-.5)*.04+(noise-.5)*.01, .96,
              .58+(brush-.5)*.04, (brush-.5)*.000015, 1)

# Structural dark steel: rougher than aluminum, metallic highlights remain visible.
write_surface('steel', .58+(noise-.5)*.04+(coarse-.5)*.025, .92,
              .43+(noise-.5)*.09, (noise-.5)*.00008, 1)

# Coated blue pipe: metallic core only where the coating has tiny chips.
pipe_chips = ((noise > .998) & (coarse>.65)).astype(np.float32)
write_surface('pipe', .93+(noise-.5)*.018-pipe_chips*.22,
              pipe_chips*.9, .56+(noise-.5)*.035-pipe_chips*.14,
              (noise-.5)*.00008-pipe_chips*.00016, 1)

# Rubber: low smoothness, no metallic response and subdued fine pores.
write_surface('rubber', .84+(noise-.5)*.12, 0,
              .14+(noise-.5)*.06, (noise-.5)*.00010, 1)

# Door panels: painted steel with horizontal stamped reinforcement grooves.
groove = np.exp(-np.square((y-.22)/.007))+np.exp(-np.square((y-.78)/.007))
write_surface('door', .93+(noise-.5)*.018-groove*.09, 0,
              .38+(noise-.5)*.05, (noise-.5)*.00009-groove*.0012, 1-groove*.14)
write_surface('wall_inset_coated', .95+(noise-.5)*.008-groove*.08, .58,
              .62+(noise-.5)*.018, (noise-.5)*.000025-groove*.0012, 1-groove*.14)

# Frosted diffuser: a subtle regular microtexture, not a mirror or transparent pane.
dots = (.5+.5*np.sin(x*np.pi*128))*(.5+.5*np.sin(y*np.pi*128))
write_surface('diffuser', .91+dots*.07, 0, .20+dots*.08,
              dots*.0001, 1)
print(f'Generated {10*3} surface maps in {OUTPUT}')
