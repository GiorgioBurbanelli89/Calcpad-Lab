import numpy as np
v = np.ones(4)
D0 = np.diag(v)          # matriz diagonal 4x4
print("diag(v) shape:", D0.shape)
D1 = np.diag(v, 1)       # con offset k=1 -> deberia 5x5
print("diag(v,1) shape:", D1.shape)
