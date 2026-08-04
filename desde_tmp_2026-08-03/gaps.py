import numpy as np
res=[]
# 1) operador @ y + en sparse
try:
    from scipy.sparse import csr_matrix
    K=csr_matrix((np.array([2.,-1,-1,2]),(np.array([0.,0,1,1]),np.array([0.,1,0,1]))),shape=(2,2))
    y=K @ np.array([1.,1.]); res.append("sparse @ OK "+str(round(y[0],1)))
except Exception as e: res.append("sparse @ FALTA: "+type(e).__name__)
# 2) linalg svd / lstsq / pinv / eig
import scipy.linalg as la
for fn in ['svd','qr','eig','lstsq','pinv']:
    res.append("linalg."+fn+(" OK" if hasattr(la,fn) else " FALTA"))
# 3) optimize least_squares / minimize / curve_fit
import scipy.optimize as op
for fn in ['least_squares','minimize','curve_fit']:
    res.append("optimize."+fn+(" OK" if hasattr(op,fn) else " FALTA"))
# 4) integrate solve_ivp / cumtrapz
import scipy.integrate as ig
for fn in ['solve_ivp','cumtrapz']:
    res.append("integrate."+fn+(" OK" if hasattr(ig,fn) else " FALTA"))
# 5) interpolate CubicSpline / griddata
import scipy.interpolate as ip
for fn in ['CubicSpline','griddata','splrep']:
    res.append("interpolate."+fn+(" OK" if hasattr(ip,fn) else " FALTA"))
# 6) scipy.stats
try:
    import scipy.stats as st; res.append("scipy.stats OK")
except Exception as e: res.append("scipy.stats FALTA: "+type(e).__name__)
# 7) special extras
import scipy.special as sp
for fn in ['jv','legendre','erfinv']:
    res.append("special."+fn+(" OK" if hasattr(sp,fn) else " FALTA"))
for r in res: print("PROBE:", r)
