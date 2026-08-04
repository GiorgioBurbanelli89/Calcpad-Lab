N=1000000; s=0.0
for i in range(1,N+1): s+=1.0/(i*i)
print("BENCH1 S=%.12f"%s)
import numpy as np
n=500; A=2*np.eye(n)-np.diag(np.ones(n-1),1)-np.diag(np.ones(n-1),-1); b=np.ones(n)
x=np.linalg.solve(A,b)
print("BENCH2 sum=%.10f x250=%.10f"%(x.sum(), x[249]))
