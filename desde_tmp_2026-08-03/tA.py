import scipy, numpy as np
import scipy.linalg as la
import scipy.sparse as sp
print("A version:", scipy.__version__)
print("A solve:", la.solve(np.array([[2.,0],[0,4]]), np.array([2.,8])).tolist())
