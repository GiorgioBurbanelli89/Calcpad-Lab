% =====================================================================================
%  VALIDACION FRAME no lineal: OpenSees forceBeamColumn (fibras) vs MATLAB 2017a
% -------------------------------------------------------------------------------------
%  Portico 3D: 2 columnas de fibra (Concrete01 nucleo/recubrimiento + Steel01) unidas por
%  viga elastica. Carga lateral en el tope. Elemento BASADO EN FUERZAS (Gauss-Lobatto 5 IP,
%  determinacion de estado por flexibilidad) + seccion de fibras + Newton-Raphson global.
%  Objetivo OpenSees: ux_top(nudo5) = 0.036622 m. Corre en MATLAB 2017a y Calcpad Lab.
% =====================================================================================
% ---- refinamiento de columnas (kref elem por cada metro; kref=1 => malla original del tcl) ----
if ~exist('kref','var'), kref=1; end
% ---- nodos: 2 columnas (z=0..4) refinadas + 5 nodos de viga en z=4 ----
nz=4*kref; zc=(0:nz)'/kref;                       % alturas de nodos de columna
Xcol1=[zeros(nz+1,1) zeros(nz+1,1) zc]; Xcol2=[6*ones(nz+1,1) zeros(nz+1,1) zc];
Xbeam=[(1:5)' zeros(5,1) 4*ones(5,1)];            % nodos 11..15 de la viga (x=1..5,z=4)
X=[Xcol1; Xcol2; Xbeam]; NN=size(X,1); ndof=6*NN;
n_top1=nz+1; n_top2=2*(nz+1); nb0=2*(nz+1);       % indices: tope col1, tope col2, base viga
node_load=n_top1;                                 % nudo con la carga (tope col izq = nudo 5 del tcl)
fib=buildsec();
FBE=[(1:nz)' (2:nz+1)'; (nz+2:2*nz+1)' (nz+3:2*(nz+1))'];   % columnas fibra
EBE=[n_top1 nb0+1; nb0+1 nb0+2; nb0+2 nb0+3; nb0+3 nb0+4; nb0+4 nb0+5; nb0+5 n_top2]; % viga elastica
% viga elastica: A E G Jx Iy Iz  (del tcl: 0.15 2.5e10 1.04e10 0.00625 0.003125 0.003125)
EB=struct('A',0.15,'E',2.5e10,'G',1.04e10,'Jx',0.00625,'Iy',0.003125,'Iz',0.003125);
nIP=5; GJ=1.0e9;                                  % 5 puntos Gauss-Lobatto; GJ torsional de la seccion
% ---- BC: bases de columna empotradas ----
fixed=false(ndof,1); for n=[1 nz+2], fixed(6*n-5:6*n)=true; end
freed=find(~fixed);
% ---- carga: Fx=2.2e5 en tope col izq ----
Pref=zeros(ndof,1); Pref(6*node_load-5)=2.2e5;
% ---- estado de materiales por fibra por IP por elemento fibra (comprometido) ----
nfib=size(fib,1); nFB=size(FBE,1);
matC=cell(nFB,1); for e=1:nFB, matC{e}=initmat(fib,nIP); end   % estado comprometido
Ug=zeros(ndof,1);
% ---- Newton-Raphson con control de carga (20 pasos de 0.05) ----
nstep=20; lam=0;
for st=1:nstep
  lam=st*0.05; Pext=lam*Pref;
  for it=1:40
    [Kg,Fint,matT]=assemble(X,FBE,EBE,EB,fib,nIP,GJ,matC,Ug);
    R=Pext-Fint; R(fixed)=0;
    if norm(R(freed))<1e-6*max(1,norm(Pext(freed))), break; end
    dU=zeros(ndof,1); dU(freed)=Kg(freed,freed)\R(freed);
    Ug=Ug+dU;
  end
  matC=matT;                                       % comprometer estado
  fprintf('paso %2d lam=%.2f  ux_top(n5)=%.6f m  (iters=%d)\n',st,lam,Ug(6*node_load-5),it);
end
fprintf('\nMATLAB ux_top = %.6f m   |   OpenSees = 0.036622 m   |   dif = %.2f%%\n',...
        Ug(6*node_load-5), 100*abs(Ug(6*node_load-5)-0.036622)/0.036622);
% --- fuerza del elemento base (elem 1) en ejes LOCALES para comparar con OpenSees ---
I=FBE(1,1); J=FBE(1,2); dof=[6*I-5:6*I, 6*J-5:6*J];
[ex,ey,ez,L]=locaxes(X(I,:),X(J,:),[1 0 0]); R=[ex;ey;ez]; T=blkdiag(R,R,R,R);
[~,fe]=fiberelem(X(I,:),X(J,:),[1 0 0],fib,nIP,GJ,matC{1},Ug(dof));
fl=T*fe;   % fuerza local: [N Vy Vz T My Mz]_I [..]_J
fprintf('BASE elem1 local: N_I=%.0f  Vz_I=%.0f  My_I=%.0f  (OpenSees: N=-106708 Vz=-69783 My=-222990)\n',fl(1),fl(3),fl(5));

% ============================ sub-funciones ============================
function fib=buildsec()
  % patches rect (matID nY nZ yI zI yJ zJ) + layers straight (matID nbar area y1 z1 y2 z2)
  P={1,12,8,-0.26,-0.15,0.26,0.15; 2,2,10,0.26,-0.19,0.3,0.19; 2,2,10,-0.3,-0.19,-0.26,0.19; ...
     2,12,2,-0.26,-0.19,0.26,-0.15; 2,12,2,-0.26,0.15,0.26,0.19};
  fib=zeros(0,4);
  for k=1:size(P,1)
    m=P{k,1}; nY=P{k,2}; nZ=P{k,3}; y1=P{k,4}; z1=P{k,5}; y2=P{k,6}; z2=P{k,7};
    dy=(y2-y1)/nY; dz=(z2-z1)/nZ; a=abs(dy*dz);
    for iy=1:nY, for iz=1:nZ
      yc=y1+(iy-0.5)*dy; zc=z1+(iz-0.5)*dz; fib(end+1,:)=[a yc zc m];
    end, end
  end
  L={3,4,0.000490874,0.26,-0.15,0.26,0.15; 3,4,0.000490874,-0.26,-0.15,-0.26,0.15};
  for k=1:size(L,1)
    m=L{k,1}; nb=L{k,2}; a=L{k,3}; y1=L{k,4}; z1=L{k,5}; y2=L{k,6}; z2=L{k,7};
    for b=1:nb
      t=(b-1)/(nb-1); yc=y1+t*(y2-y1); zc=z1+t*(z2-z1); fib(end+1,:)=[a yc zc m];  % barras INCLUYEN extremos (como OpenSees layer straight)
    end
  end
end
function m=initmat(fib,nIP)
  m.eps=zeros(size(fib,1),nIP); m.sig=zeros(size(fib,1),nIP);
  m.emax=zeros(size(fib,1),nIP);            % Concrete01: min strain (mas negativo) alcanzado
  m.epl=zeros(size(fib,1),nIP);             % Steel01: deformacion plastica
end
function [Kg,Fint,matT]=assemble(X,FBE,EBE,EB,fib,nIP,GJ,matC,Ug)
  ndof=6*size(X,1); Kg=zeros(ndof); Fint=zeros(ndof,1); matT=matC;
  for e=1:size(FBE,1)
    I=FBE(e,1); J=FBE(e,2); dof=[6*I-5:6*I, 6*J-5:6*J];
    [ke,fe,mt]=fiberelem(X(I,:),X(J,:),[1 0 0],fib,nIP,GJ,matC{e},Ug(dof));
    matT{e}=mt; Kg(dof,dof)=Kg(dof,dof)+ke; Fint(dof)=Fint(dof)+fe;
  end
  for e=1:size(EBE,1)
    I=EBE(e,1); J=EBE(e,2); dof=[6*I-5:6*I, 6*J-5:6*J];
    ke=elasticbeam(X(I,:),X(J,:),[0 1 0],EB);
    Kg(dof,dof)=Kg(dof,dof)+ke; Fint(dof)=Fint(dof)+ke*Ug(dof);
  end
end
function [ex,ey,ez,L]=locaxes(pI,pJ,vecxz)
  d=pJ-pI; L=norm(d); ex=d/L;
  ey=cross(vecxz,ex); ey=ey/norm(ey); ez=cross(ex,ey);
end
function [xi,wt]=lobatto(n,L)
  % Gauss-Lobatto en [0,L] (n=5)
  nd=[-1 -sqrt(3/7) 0 sqrt(3/7) 1]; w=[0.1 49/90 32/45 49/90 0.1];
  xi=(nd+1)*L/2; wt=w*L/2;
end
function B=Bmat(x,L)
  B=zeros(4,12); B(1,1)=-1/L; B(1,7)=1/L;                        % e0 axial
  bv=[12*x/L^3-6/L^2, 6*x/L^2-4/L, 6/L^2-12*x/L^3, 6*x/L^2-2/L];
  B(2,[2 6 8 12])=bv;                                            % kz (v,rz)
  bw=[12*x/L^3-6/L^2, -(6*x/L^2-4/L), 6/L^2-12*x/L^3, -(6*x/L^2-2/L)];
  B(3,[3 5 9 11])=bw;                                            % ky (w,ry)
  B(4,4)=-1/L; B(4,10)=1/L;                                      % torsion
end
function [ke,fe,mt]=fiberelem(pI,pJ,vecxz,fib,nIP,GJ,mat,ug)
  [ex,ey,ez,L]=locaxes(pI,pJ,vecxz); R=[ex;ey;ez]; T=blkdiag(R,R,R,R);
  ul=T*ug; [xi,wt]=lobatto(nIP,L); Kl=zeros(12); fl=zeros(12,1); mt=mat;
  for g=1:nIP
    B=Bmat(xi(g),L); d=B*ul;
    [r3,ks3,mt]=secstate(fib,d(1),d(2),d(3),mt,g);
    r=[r3; GJ*d(4)]; D=blkdiag(ks3,GJ);
    Kl=Kl+wt(g)*(B'*D*B); fl=fl+wt(g)*(B'*r);
  end
  ke=T'*Kl*T; fe=T'*fl;
end
function [r3,ks3,mt]=secstate(fib,e0,kz,ky,mt,g)
  n=size(fib,1); N=0; Mz=0; My=0; k=zeros(3);
  for i=1:n
    y=fib(i,2); z=fib(i,3); A=fib(i,1); m=fib(i,4); eps=e0-y*kz+z*ky;
    if m==1||m==2
      [sig,Et,em]=concrete01(eps,mt.emax(i,g),m); mt.emax(i,g)=em;
    else
      [sig,Et,ep]=steel01(eps,mt.epl(i,g)); mt.epl(i,g)=ep;
    end
    mt.eps(i,g)=eps; mt.sig(i,g)=sig;
    N=N+sig*A; Mz=Mz-sig*A*y; My=My+sig*A*z;
    v=[1;-y;z]; k=k+Et*A*(v*v');
  end
  r3=[N;Mz;My]; ks3=k;
end
function [sig,Et,emax]=concrete01(eps,emax_prev,m)
  if m==1, fpc=-3.45e7; ec0=-0.004; fcu=-2.4e7; ecu=-0.014;
  else     fpc=-2.8e7;  ec0=-0.002; fcu=0;      ecu=-0.006; end
  Ec=2*fpc/ec0; emax=min(emax_prev,eps);
  [smin,~]=cenv(emax,fpc,ec0,fcu,ecu);
  r=max(min(emax/ec0,1),0); ep=ec0*(0.145*r^2+0.13*r);          % plastica Karsan-Jirsa
  if abs(emax-ep)>1e-12, Eu=smin/(emax-ep); else Eu=Ec; end
  if eps<=emax
    [sig,Et]=cenv(eps,fpc,ec0,fcu,ecu);
  else
    sig=smin+Eu*(eps-emax);
    if sig>0, sig=0; Et=1e-9; else Et=Eu; end
  end
  if Et==0, Et=1e-9; end
end
function [s,t]=cenv(e,fpc,ec0,fcu,ecu)
  if e>=0, s=0; t=1e-9; return; end
  if e>=ec0,      s=fpc*(2*e/ec0-(e/ec0)^2); t=fpc*(2/ec0-2*e/ec0^2);
  elseif e>=ecu,  s=fpc+(fcu-fpc)*(e-ec0)/(ecu-ec0); t=(fcu-fpc)/(ecu-ec0);
  else            s=fcu; t=1e-9; end
end
function [sig,Et,epl]=steel01(eps,epl_prev)
  E=2e11; fy=4.2e8; b=0.01; Hk=E*b/(1-b); alpha=Hk*epl_prev;
  sig_tr=E*(eps-epl_prev); f=abs(sig_tr-alpha)-fy;
  if f<=0, sig=sig_tr; Et=E; epl=epl_prev;
  else dg=f/(E+Hk); s=sign(sig_tr-alpha); epl=epl_prev+dg*s; sig=sig_tr-E*dg*s; Et=E*b; end
end
function ke=elasticbeam(pI,pJ,vecxz,EB)
  [ex,ey,ez,L]=locaxes(pI,pJ,vecxz); R=[ex;ey;ez]; T=blkdiag(R,R,R,R);
  E=EB.E; A=EB.A; G=EB.G; Jx=EB.Jx; Iy=EB.Iy; Iz=EB.Iz;
  kl=zeros(12); EA=E*A/L; GJl=G*Jx/L;
  kl(1,1)=EA; kl(1,7)=-EA; kl(7,1)=-EA; kl(7,7)=EA;             % axial
  kl(4,4)=GJl; kl(4,10)=-GJl; kl(10,4)=-GJl; kl(10,10)=GJl;      % torsion
  az=E*Iz/L^3;                                                   % flexion x-y (v,rz): 2,6,8,12
  i=[2 6 8 12]; Kz=az*[12 6*L -12 6*L; 6*L 4*L^2 -6*L 2*L^2; -12 -6*L 12 -6*L; 6*L 2*L^2 -6*L 4*L^2];
  kl(i,i)=kl(i,i)+Kz;
  ay=E*Iy/L^3;                                                   % flexion x-z (w,ry): 3,5,9,11
  j=[3 5 9 11]; Ky=ay*[12 -6*L -12 -6*L; -6*L 4*L^2 6*L 2*L^2; -12 6*L 12 6*L; -6*L 2*L^2 6*L 4*L^2];
  kl(j,j)=kl(j,j)+Ky;
  ke=T'*kl*T;
end
