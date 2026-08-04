import numpy as np
K = np.array([[1.0,-1.0],[-1.0,1.0]])
print("K=",K)
S = K[1:, 1:]
print("K[1:,1:]=",S)
print("shape", S.shape)
b = np.array([5.0])
x = np.linalg.solve(S, b)
print("x=",x)
