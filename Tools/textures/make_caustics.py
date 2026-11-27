# tileable caustic pattern for the sun's light cookie. layered warped voronoi edges,
# which is roughly what real caustic networks look like. output is greyscale png
import numpy as np, sys
from PIL import Image
N = 512
rng = np.random.default_rng(7)
y, x = np.mgrid[0:N, 0:N] / N

def voronoi_edges(cells, warp):
    pts = rng.random((cells * cells, 2))
    # tile the points 3x3 so the result wraps seamlessly
    tiles = np.concatenate([pts + [dx, dy] for dx in (-1, 0, 1) for dy in (-1, 0, 1)])
    wx = x + warp * np.sin(2 * np.pi * (y * 2 + 0.3))
    wy = y + warp * np.sin(2 * np.pi * (x * 2 + 0.7))
    d1 = np.full((N, N), 9.0); d2 = np.full((N, N), 9.0)
    for px, py in tiles:
        d = np.hypot(wx % 1 - px, wy % 1 - py)
        m = d < d1
        d2 = np.where(m, d1, np.minimum(d2, d)); d1 = np.where(m, d, d1)
    e = d2 - d1
    return np.exp(-e * cells * 6)

c = voronoi_edges(7, 0.03) * 0.65 + voronoi_edges(11, 0.02) * 0.35
c = (c - c.min()) / (c.max() - c.min())
c = 0.55 + 0.45 * c ** 1.4  # keep a floor so the cookie never blacks the sun out
Image.fromarray((c * 255).astype(np.uint8), "L").save(sys.argv[1])
print("ok", sys.argv[1])
