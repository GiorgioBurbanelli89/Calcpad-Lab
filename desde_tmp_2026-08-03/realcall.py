import scipy
print("version:", scipy.__version__)
import numpy as np
import scipy.linalg as la
A=np.array([[3.,0],[0,2]])
try:
    U,s,Vt = la.svd(A); print("svd LLAMA OK s0=%.3f (=> fallback REAL o implementado)" % s[0])
except Exception as e: print("svd FALLA:", type(e).__name__, "(=> embebido, FALTA)")
try:
    import scipy.stats as st
    print("stats.norm.cdf(0)=%.3f (=> real)" % st.norm.cdf(0))
except Exception as e: print("stats FALLA:", type(e).__name__, "(=> falta)")
try:
    from scipy.optimize import minimize
    r=minimize(lambda x:(x[0]-1)**2+(x[1]-2)**2, np.array([0.,0.]))
    print("minimize LLAMA OK (=> real o impl)")
except Exception as e: print("minimize FALLA:", type(e).__name__)
