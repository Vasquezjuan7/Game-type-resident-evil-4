# uso: deform.py tools in.obj out.obj preview pivx pivy ang x_in x_out ya xgap
import sys, math; sys.path.insert(0, sys.argv[1]); from objpose import *
src, dst, prev = sys.argv[2], sys.argv[3], sys.argv[4]
pivx, pivy, ang, x_in, x_out, ya, xgap = map(float, sys.argv[5:12])
lines, vi, ni, V, N, F = load(src)

def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)

theta = np.zeros(len(V))
for s in (1, -1):
    sx = s * V[:, 0]
    w = np.where(V[:, 1] >= ya, smooth(x_in, x_out, sx), (sx > xgap).astype(float))
    w *= (sx > 0)
    theta += -s * math.radians(ang) * w
px = np.sign(V[:, 0]) * pivx
c, sn = np.cos(theta), np.sin(theta)
dx, dy = V[:, 0] - px, V[:, 1] - pivy
V2 = V.copy()
V2[:, 0] = px + c * dx - sn * dy
V2[:, 1] = pivy + sn * dx + c * dy

out = list(lines)
for k, i in enumerate(vi):
    out[i] = 'v %.6f %.6f %.6f' % tuple(V2[k])
if N is not None:
    # cada normal toma el giro del vertice con el que se usa
    nth = np.zeros(len(N)); seen = np.zeros(len(N), bool)
    for l in lines:
        if l.startswith('f '):
            for p in l.split()[1:]:
                q = p.split('/')
                if len(q) == 3 and q[2]:
                    n = int(q[2]) - 1
                    if not seen[n]: seen[n] = True; nth[n] = theta[int(q[0]) - 1]
    cn, snn = np.cos(nth), np.sin(nth)
    N2 = N.copy(); N2[:, 0] = cn * N[:, 0] - snn * N[:, 1]; N2[:, 1] = snn * N[:, 0] + cn * N[:, 1]
    for k, i in enumerate(ni):
        out[i] = 'vn %.5f %.5f %.5f' % tuple(N2[k])
open(dst, 'w', encoding='utf-8', newline='\n').write('\n'.join(out))
print('min', V2.min(0), 'max', V2.max(0), 'rotados', int((theta != 0).sum()))
step = max(1, len(F) // 120000)
render(V2, F[::step], ((0, 1), 2), prev + '_front.png')
render(V2, F[::step], ((2, -1), 0), prev + '_side.png')
