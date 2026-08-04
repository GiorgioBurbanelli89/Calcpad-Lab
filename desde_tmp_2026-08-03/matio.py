from scipy.io import loadmat, savemat
import numpy as np
for f in ['real_unc.mat','real_cmp.mat']:
    d = loadmat('C:/tmp/'+f)
    print(f, "A[1,2]=%.1f  v_sum=%.1f  name=%s" % (d['A'][1][2], np.array(d['v']).sum(), d['name']))
# savemat embebido -> escribe emb.mat
savemat('C:/tmp/emb.mat', {'B': np.array([[7.,8],[9,10]]), 'w': np.array([1.,2,3,4])})
print("savemat embebido -> emb.mat escrito")
