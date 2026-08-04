# M-phi 2D por CONTROL DE CARGA (robusto): aplica momento creciente, registra curvatura.
# Seccion rotada: fiber-y-2D = z-original -> flexion == My del frame. -> mc_opensees.csv
import openseespy.opensees as op, numpy as np, sys
Nax=float(sys.argv[1]) if len(sys.argv)>1 else 0.0
Mmax=float(sys.argv[2]) if len(sys.argv)>2 else 2.0e5
op.wipe(); op.model('basic','-ndm',2,'-ndf',3)
op.uniaxialMaterial('Concrete01',1,-3.45e7,-0.004,-2.4e7,-0.014)
op.uniaxialMaterial('Concrete01',2,-2.8e7,-0.002,0.0,-0.006)
op.uniaxialMaterial('Steel01',3,4.2e8,2e11,0.01)
op.section('Fiber',10)
op.patch('rect',1, 8,12, -0.15,-0.26, 0.15,0.26)
op.patch('rect',2,10, 2, -0.19,0.26, 0.19,0.30);  op.patch('rect',2,10, 2, -0.19,-0.30, 0.19,-0.26)
op.patch('rect',2, 2,12, -0.19,-0.26,-0.15,0.26);  op.patch('rect',2, 2,12, 0.15,-0.26, 0.19,0.26)
op.layer('straight',3,4,0.000490874,-0.15,0.26, 0.15,0.26); op.layer('straight',3,4,0.000490874,-0.15,-0.26,0.15,-0.26)
op.node(1,0,0); op.node(2,0,0); op.fix(1,1,1,1); op.fix(2,0,1,0)
op.element('zeroLengthSection',1,1,2,10)
op.constraints('Plain'); op.numberer('Plain'); op.system('BandGeneral')
op.test('NormUnbalance',1e-8,50); op.algorithm('Newton'); op.analysis('Static')
# fase 1: axial constante
if abs(Nax)>0:
  op.timeSeries('Constant',1); op.pattern('Plain',1,1); op.load(2,Nax,0,0)
  op.integrator('LoadControl',1.0); op.analyze(1); op.loadConst('-time',0.0)
# fase 2: momento creciente
op.timeSeries('Linear',2); op.pattern('Plain',2,2); op.load(2,0,0,1.0)   # M unitario en DOF3
nst=200; op.integrator('LoadControl',Mmax/nst)
res=[]
for i in range(nst):
  if op.analyze(1)!=0: print('stop en',i,' M=',(i)*Mmax/nst); break
  fr=op.eleResponse(1,'section',1,'force'); res.append([op.nodeDisp(2,3),fr[1],fr[0]])
res=np.array(res)
if res.ndim==2:
  np.savetxt(r'C:\tmp\mc_opensees.csv',res[:,:2],delimiter=',',fmt='%.8e')
  print('pts=%d  M_max=%.0f  kappa_max=%.5f  N=%.0f'%(len(res),res[:,1].max(),res[-1,0],res[-1,2]))
  for k in [0.0005,0.001,0.002]:
    j=np.argmin(np.abs(res[:,0]-k)); print('kappa=%.4f -> M=%.0f'%(k,res[j,1]))
