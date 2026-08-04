import scipy
print("scipy version:", scipy.__version__)
# scipy.sparse + spsolve (FEM): tridiag(2,-1) n=200
from scipy.sparse import coo_matrix
from scipy.sparse.linalg import spsolve
import numpy as np
n=200
row=[]; col=[]; data=[]
for i in range(n):
    row.append(i); col.append(i); data.append(2.0)
    if i>0:
        row.append(i); col.append(i-1); data.append(-1.0)
    if i<n-1:
        row.append(i); col.append(i+1); data.append(-1.0)
A = coo_matrix((np.array(data), (np.array(row,dtype=float), np.array(col,dtype=float))), shape=(n,n))
b = np.ones(n)
x = spsolve(A, b)
print("SPARSE spsolve: sum=%.6f x100=%.6f  shape=%s nnz=%d" % (x.sum(), x[100], A.shape, A.nnz))
# scipy.linalg
import scipy.linalg as la
M = np.array([[4.,1,0],[1,4,1],[0,1,4]])
print("LINALG solve sum=%.8f  det=%.4f  norm(b)=%.4f" % (la.solve(M, np.array([1.,2,3])).sum(), la.det(M), la.norm(np.array([3.,4.]))))
# scipy.optimize
from scipy.optimize import newton, fsolve
r = newton(lambda z: z**3 - 2*z - 5, 2.0)
print("OPTIMIZE newton x^3-2x-5=0 -> %.8f" % r)
sol = fsolve(lambda v: np.array([v[0]**2+v[1]**2-4, v[0]*v[1]-1]), np.array([1.5,0.5]))
print("OPTIMIZE fsolve -> x=%.6f y=%.6f" % (sol[0], sol[1]))
