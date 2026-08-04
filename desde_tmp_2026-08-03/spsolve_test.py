import numpy as np
n=500
rows=[]; cols=[]; vals=[]
for i in range(n):
    rows.append(i); cols.append(i); vals.append(2.0)
    if i>0:
        rows.append(i); cols.append(i-1); vals.append(-1.0)
    if i<n-1:
        rows.append(i); cols.append(i+1); vals.append(-1.0)
b=np.ones(n)
x=np.linalg.spsolve(np.array(rows,dtype=float), np.array(cols,dtype=float), np.array(vals), b)
print("SPSOLVE sum=%.6f x250=%.6f"%(x.sum(), x[249]))
