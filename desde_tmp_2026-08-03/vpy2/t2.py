import numpy as np
K = np.array([[2.0,-1.0,0.0],[-1.0,2.0,-1.0],[0.0,-1.0,1.0]])
print("A", K[1:, 1:])
print("B", K[1:][:,1:])
print("C", np.delete(np.delete(K,0,axis=0),0,axis=1))
print("D", K[0:2, 0:2])
print("E", K[1, 1])
