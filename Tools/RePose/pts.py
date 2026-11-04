# render rapido por puntos con sombreado por normal de cara (centroides)
import sys; sys.path.insert(0, sys.argv[1]); from objpose import *
lines, vi, ni, V, N, F = load(sys.argv[2]); out = sys.argv[3]
A, B, C = V[F[:,0]], V[F[:,1]], V[F[:,2]]
n = np.cross(B-A, C-A); n /= np.linalg.norm(n, axis=1, keepdims=True) + 1e-12
for name, ha, hs, da in (('front', 0, 1, 2), ('side', 2, -1, 0)):
    size = 700; img = np.full((size, size, 3), 30, np.uint8); zb = np.full((size, size), -1e9)
    for t in np.linspace(0, 1, 6):
        for u in np.linspace(0, 1 - t, max(1, int(6 * (1 - t)))):
            P = A + (B - A) * t + (C - A) * u
            x = ((P[:,ha]*hs + 0.65) / 1.3 * size).astype(int)
            y = ((1.95 - P[:,1]) / 1.3 * size).astype(int)
            z = P[:,da] * (1 if da == 2 else -hs)
            m = (x >= 0) & (x < size) & (y >= 0) & (y < size)
            x, y, z, c = x[m], y[m], z[m], (60 + 190 * np.abs(n[m, da])).astype(np.uint8)
            o = np.argsort(z); x, y, z, c = x[o], y[o], z[o], c[o]
            zb[y, x] = z; img[y, x] = c[:, None]
    png(out + '_' + name + '.png', img)
