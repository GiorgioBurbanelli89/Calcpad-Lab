# Momento-curvatura PURO de la seccion de fibras (zeroLengthSection) en OpenSees.
# Axial = -70468 N (el del frame). Curvatura kappaY (flexion usando z, como el frame). -> mc_opensees.csv
import openseespy.opensees as op, numpy as np
op.wipe(); op.model('basic','-ndm',3,'-ndf',6)
op.uniaxialMaterial('Concrete01',1,-3.45e7,-0.004,-2.4e7,-0.014)
op.uniaxialMaterial('Concrete01',2,-2.8e7,-0.002,0.0,-0.006)
op.uniaxialMaterial('Steel01',3,4.2e8,2e11,0.01)
op.section('Fiber',10,'-GJ',1e9)
op.patch('rect',1,12,8,-0.26,-0.15,0.26,0.15)
op.patch('rect',2,2,10,0.26,-0.19,0.30,0.19)
op.patch('rect',2,2,10,-0.30,-0.19,-0.26,0.19)
op.patch('rect',2,12,2,-0.26,-0.19,0.26,-0.15)
op.patch('rect',2,12,2,-0.26,0.15,0.26,0.19)
op.layer('straight',3,4,0.000490874,0.26,-0.15,0.26,0.15)
op.layer('straight',3,4,0.000490874,-0.26,-0.15,-0.26,0.15)
op.node(1,0,0,0); op.node(2,0,0,0)
op.fix(1,1,1,1,1,1,1); op.fix(2,0,1,1,1,0,0)   # nodo2: libre axial(1) y ry(5)=kappaY
op.element('zeroLengthSection',1,1,2,10)
# 1) axial constante
P=-70468.0
op.timeSeries('Constant',1); op.pattern('Plain',1,1); op.load(2,P,0,0,0,0,0)
op.constraints('Transformation'); op.numberer('Plain'); op.system('BandGeneral')
op.test('NormUnbalance',1e-8,30); op.algorithm('Newton'); op.integrator('LoadControl',1.0)
op.analysis('Static'); ok=op.analyze(1); print('axial converge:',ok,' ux=',op.nodeDisp(2,1))
op.loadConst('-time',0.0)   # mantiene axial
# 2) impone kappaY creciente (DOF 5)
op.test('NormDispIncr',1e-8,50); op.algorithm('Newton')
op.integrator('DisplacementControl',2,5,2e-5); op.analysis('Static')
res=[]
for i in range(300):
    ok=op.analyze(1)
    if ok!=0: print('no converge en paso',i); break
    ky=op.nodeDisp(2,5)
    op.reactions(); My=-op.nodeReaction(1,5)
    res.append([ky,My])
res=np.array(res); np.savetxt(r'C:\tmp\mc_opensees.csv',res,delimiter=',',fmt='%.8e')
print('mc_opensees.csv  pts=%d  kappaY max=%.5f  My max=%.0f'%(len(res),res[-1,0],res[:,1].max()))
# valor a la curvatura del frame (busca My=222990)
i=np.argmin(np.abs(res[:,1]-222990)); print('En My~222990: kappaY_opensees=%.6f'%res[i,0])
