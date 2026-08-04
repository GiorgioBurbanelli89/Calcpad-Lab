import numpy as np
k1=200000.0*2500.0/3000.0
k2=200000.0*2500.0/2000.0
Kred=np.array([[k1+k2,-k2],[-k2,k2]])
F=np.array([0.0,50000.0])
u=np.linalg.solve(Kred,F)
print("u=",u)
print("u1=%.4f u2=%.4f"%(u[0],u[1]))
