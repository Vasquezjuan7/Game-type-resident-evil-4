import sys, zlib, struct, numpy as np

def load(path):
    lines = open(path, encoding='utf-8', errors='replace').read().split('\n')
    vi = [i for i,l in enumerate(lines) if l.startswith('v ')]
    ni = [i for i,l in enumerate(lines) if l.startswith('vn ')]
    V = np.array([[float(x) for x in lines[i].split()[1:4]] for i in vi])
    N = np.array([[float(x) for x in lines[i].split()[1:4]] for i in ni]) if ni else None
    F = []
    for l in lines:
        if l.startswith('f '):
            idx = [int(p.split('/')[0]) for p in l.split()[1:]]
            for k in range(1, len(idx)-1): F.append((idx[0], idx[k], idx[k+1]))
    F = np.array(F) - 1
    return lines, vi, ni, V, N, F

def png(path, img):
    h, w = img.shape[:2]
    raw = b''.join(b'\x00' + img[y].tobytes() for y in range(h))
    def chunk(t, d): return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    open(path, 'wb').write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(raw)) + chunk(b'IEND', b''))

def render(V, F, axes, path, size=500):
    # axes: (horizontal index, sign), depth index; draws triangles shaded by normal
    lo = V.min(0); hi = V.max(0); ext = (hi - lo).max()
    img = np.full((size, size, 3), 30, np.uint8); zb = np.full((size, size), -1e9)
    (ha, hs), da = axes
    P = np.zeros((len(V), 3))
    P[:,0] = ((V[:,ha]*hs - (lo[ha] if hs>0 else -hi[ha])) / ext * 0.9 + 0.05) * size
    P[:,1] = (1 - ((V[:,1] - lo[1]) / ext * 0.9 + 0.05)) * size
    P[:,2] = V[:,da] * (1 if da == 2 else -hs)
    A, B, C = V[F[:,0]], V[F[:,1]], V[F[:,2]]
    n = np.cross(B - A, C - A); n /= np.linalg.norm(n, axis=1, keepdims=True) + 1e-12
    for t in range(len(F)):
        p = P[F[t]]
        x0, y0 = np.floor(p[:,:2].min(0)).astype(int); x1, y1 = np.ceil(p[:,:2].max(0)).astype(int)
        x0 = max(x0, 0); y0 = max(y0, 0); x1 = min(x1, size-1); y1 = min(y1, size-1)
        if x1 < x0 or y1 < y0: continue
        xs, ys = np.meshgrid(np.arange(x0, x1+1)+.5, np.arange(y0, y1+1)+.5)
        d = (p[1,1]-p[2,1])*(p[0,0]-p[2,0]) + (p[2,0]-p[1,0])*(p[0,1]-p[2,1])
        if abs(d) < 1e-9: continue
        l1 = ((p[1,1]-p[2,1])*(xs-p[2,0]) + (p[2,0]-p[1,0])*(ys-p[2,1]))/d
        l2 = ((p[2,1]-p[0,1])*(xs-p[2,0]) + (p[0,0]-p[2,0])*(ys-p[2,1]))/d
        l3 = 1 - l1 - l2
        m = (l1 >= 0) & (l2 >= 0) & (l3 >= 0)
        if not m.any(): continue
        z = l1*p[0,2] + l2*p[1,2] + l3*p[2,2]
        yy, xx = (ys[m]-.5).astype(int), (xs[m]-.5).astype(int); zz = z[m]
        ok = zz > zb[yy, xx]
        zb[yy[ok], xx[ok]] = zz[ok]
        c = int(60 + 190 * abs(n[t, da]))
        img[yy[ok], xx[ok]] = (c, c, c)
    png(path, img)
