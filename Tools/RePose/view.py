import sys; sys.path.insert(0, sys.argv[1]); from objpose import *
lines, vi, ni, V, F = load(sys.argv[2])[:3] + load(sys.argv[2])[3:4] + load(sys.argv[2])[5:6]
print('verts', len(V), 'tris', len(F), 'min', V.min(0), 'max', V.max(0))
step = max(1, len(F)//150000)
render(V, F[::step], ((0, 1), 2), sys.argv[3] + '_front.png')
render(V, F[::step], ((2, -1), 0), sys.argv[3] + '_side.png')
