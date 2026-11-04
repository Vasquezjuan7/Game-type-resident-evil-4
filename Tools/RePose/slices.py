import sys; sys.path.insert(0, sys.argv[1]); from objpose import *
V = load(sys.argv[2])[3]
for y in np.arange(0.9, 1.6, 0.04):
    m = (V[:,1] > y) & (V[:,1] < y + 0.04) & (V[:,0] > 0)
    h, _ = np.histogram(V[m,0], bins=30, range=(0, 0.6))
    print('%.2f ' % y + ''.join('#' if c > 20 else ('.' if c else ' ') for c in h))
