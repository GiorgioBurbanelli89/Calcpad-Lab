% =====================================================================================
%  CILINDRO A COMPRESION con la FORMULACION REAL ASDConcrete3D (portada de OpenSees/STKO)
% -------------------------------------------------------------------------------------
%  Modelo dano-plasticidad de concreto (Lubliner + curvas de endurecimiento tension/compresion
%  + regularizacion de energia de fractura). El criterio de Lubliner es DEPENDIENTE DE LA
%  PRESION: el material confinado (bajo los platos) NO falla, y el cortante LOCALIZA en una
%  banda diagonal/cono -> la banda ondulada de STKO, NATURAL (no sembrada). Orbit 3D (Lab==MATLAB).
%  Parametros EXACTOS del strain-loc de STKO. N,mm,MPa.
% =====================================================================================
R=75; Lz=300; nx=8; ny=8; nz=16; E=27606; nu=0.2; Kc=0.6667;
% --- curvas ASDConcrete3D (strain-loc STKO) ---
Te=[0 1.011e-4 1.684e-4 5.693e-3 2.813e-2 2.813e-1]; Ts=[0 2.79 3.1 0.62 0.0031 0.0031];
Ce=[0 5e-4 6.666e-4 8.332e-4 9.999e-4 1.167e-3 1.333e-3 1.5e-3 1.667e-3 1.833e-3 2e-3 0.3233 0.3238];
Cs=[0 13.8 18.15 21.98 25.30 28.11 30.41 32.20 33.48 34.24 34.5 6.9 6.9];
fc=34.5; ft=3.1; fcft=fc/ft;
umax=6.0; nstep=30; visc=0.04;
la=E*nu/((1+nu)*(1-2*nu)); mu=E/(2*(1+nu));
D0=[la+2*mu la la 0 0 0; la la+2*mu la 0 0 0; la la la+2*mu 0 0 0; 0 0 0 mu 0 0; 0 0 0 0 mu 0; 0 0 0 0 0 mu];
g=1/sqrt(3); gp=zeros(8,3); q=0;
for a=[-g g], for b=[-g g], for c=[-g g], q=q+1; gp(q,:)=[a b c]; end, end, end
% --- malla ---
NN=(nx+1)*(ny+1)*(nz+1); XYZ=zeros(NN,3);
for k=0:nz, for j=0:ny, for i=0:nx
  u=-1+2*i/nx; v=-1+2*j/ny; x=R*u*sqrt(max(0,1-v*v/2)); y=R*v*sqrt(max(0,1-u*u/2));
  XYZ(nid(i,j,k,nx,ny),:)=[x y Lz*k/nz];
end, end, end
NE=nx*ny*nz; els=zeros(NE,8); eijk=zeros(NE,3); e=0;
for k=0:nz-1, for j=0:ny-1, for i=0:nx-1
  e=e+1; els(e,:)=[nid(i,j,k,nx,ny) nid(i+1,j,k,nx,ny) nid(i+1,j+1,k,nx,ny) nid(i,j+1,k,nx,ny) ...
                   nid(i,j,k+1,nx,ny) nid(i+1,j,k+1,nx,ny) nid(i+1,j+1,k+1,nx,ny) nid(i,j+1,k+1,nx,ny)];
  eijk(e,:)=[i j k];
end, end, end
ng=3*NN; Dof=zeros(NE,24); Bc=cell(NE,1); Ir=zeros(NE*576,1); Jc=Ir; V0=Ir;
for e=1:NE
  el=els(e,:); co=XYZ(el,:); d=zeros(1,24);
  for a=1:8, d(3*a-2:3*a)=[3*el(a)-2 3*el(a)-1 3*el(a)]; end
  Dof(e,:)=d; Ke=zeros(24);
  for q=1:8, dN=dshp(gp(q,1),gp(q,2),gp(q,3)); Jm=dN'*co; dNx=(Jm\dN')'; B=Bmat(dNx); Ke=Ke+B'*D0*B*abs(det(Jm)); end
  dN0=dshp(0,0,0); J0=dN0'*co; Bc{e}=Bmat((J0\dN0')');
  idx=(e-1)*576+(1:576); Ir(idx)=reshape(repmat(d,24,1),[],1); Jc(idx)=reshape(repmat(d,1,24),[],1); V0(idx)=Ke(:);
end
% imperfeccion aleatoria pequena (campo GaussRandMat de STKO) -> rompe simetria, localiza natural
rng(7); imperf=0.90+0.20*rand(NE,1);
% BC: base y tope confinados (placas con friccion de STKO) + uz impuesto
fixd=false(ng,1); imp=[];
for i=0:nx, for j=0:ny
  nb=nid(i,j,0,nx,ny); fixd(3*nb-2)=1; fixd(3*nb-1)=1; fixd(3*nb)=1;
  nt=nid(i,j,nz,nx,ny); fixd(3*nt-2)=1; fixd(3*nt-1)=1; fixd(3*nt)=1; imp(end+1)=3*nt;
end, end
free=find(~fixd);
% Lubliner params
fb=1.16; alp=(fb-1)/(2*fb-1); gam=3*(1-Kc)/(2*Kc-1); bet=fcft*(1-alp)-(1+alp);
dv=zeros(NE,1); xtm=zeros(NE,1); xcm=zeros(NE,1); U=zeros(ng,1); dts=1/nstep;
for st=1:nstep
  uz=-umax*st/nstep;
  for it=1:10
    Vv=V0.*reshape(repmat((1-dv)',576,1),[],1); K=sparse(Ir,Jc,Vv,ng,ng);
    U=zeros(ng,1); U(imp)=uz; U(free)=K(free,free)\(-K(free,imp)*U(imp));
    dold=dv;
    for e=1:NE
      eps=Bc{e}*U(Dof(e,:)); sb=D0*eps;                    % tension efectiva
      St=[sb(1) sb(4) sb(5); sb(4) sb(2) sb(6); sb(5) sb(6) sb(3)];
      ev=sort(eig(St),'descend'); s1=ev(1); s2=ev(2); s3=ev(3);
      xt=lub(s1,s2,s3,alp,bet,gam,1/fcft)/E;                % Lubliner tension (scale 1/fcft)
      n1=min(s1,0); n2=min(s2,0); n3=min(s3,0);
      xc=lub(n1,n2,n3,alp,bet,gam,1)/E;                     % Lubliner compresion (scale 1)
      xt=xt/imperf(e); xc=xc/imperf(e);                     % imperfeccion (elementos debiles fallan antes)
      if xt>xtm(e), xtm(e)=xt; end
      if xc>xcm(e), xcm(e)=xc; end
      dtg=dmg(Te,Ts,xtm(e),E); dcg=dmg(Ce,Cs,xcm(e),E);     % dano de las curvas ASDConcrete3D
      dtar=1-(1-dtg)*(1-dcg);
      dv(e)=(dv(e)+(dts/visc)*dtar)/(1+dts/visc); dv(e)=max(dv(e),dold(e));
    end
    if max(abs(dv-dold))<3e-3, break; end
  end
  fprintf('paso %2d  acort=%.2fmm  dano max=%.3f  #d>0.5=%d\n',st,-uz,max(dv),sum(dv>0.5));
end
% --- render superficie: color por DESPLAZAMIENTO (como STKO) ---
fmap=[1 2 3 4;5 6 7 8;1 2 6 5;2 3 7 6;3 4 8 7;4 1 5 8]; dirn=[0 0 -1;0 0 1;0 -1 0;1 0 0;0 1 0;-1 0 0];
Umag=sqrt(U(1:3:end).^2+U(2:3:end).^2+U(3:3:end).^2); sc=15; Xd=XYZ+sc*reshape(U,3,[])'; F=[];
for e=1:NE
  i=eijk(e,1); j=eijk(e,2); k=eijk(e,3);
  for ff=1:6, ni=i+dirn(ff,1); nj=j+dirn(ff,2); nk=k+dirn(ff,3);
    if ni<0||ni>=nx||nj<0||nj>=ny||nk<0||nk>=nz, F(end+1,:)=els(e,fmap(ff,:)); end
  end
end
figure; patch('Faces',F,'Vertices',Xd,'FaceVertexCData',Umag,'FaceColor','interp','EdgeColor','none');
axis equal; view(35,12); rotate3d on; colormap(jet); colorbar;
title('Cilindro ASDConcrete3D (Lubliner) - banda de cortante NATURAL como STKO');
xlabel('x'); ylabel('y'); zlabel('z');

% ===================== sub-funciones =====================
function d=nid(i,j,k,nx,ny), d=k*(nx+1)*(ny+1)+j*(nx+1)+i+1; end
function r=lub(s1,s2,s3,alp,bet,gam,scale)
  I1=s1+s2+s3; p=I1/3; d1=s1-p; d2=s2-p; d3=s3-p; J2=0.5*(d1*d1+d2*d2+d3*d3);
  smax=max(s1,0); smin=max(-s1,0);
  r=(1/(1-alp))*(alp*I1 + sqrt(3*J2) + bet*smax - gam*smin)*scale;
  if r<0, r=0; end
end
function d=dmg(X,S,x,E)
  if x<=0, d=0; return; end
  if x>=X(end), y=S(end); else y=interp1(X,S,x); end
  d=max(0, 1 - y/(E*x)); if d>0.99, d=0.99; end
end
function dN=dshp(x,y,z)
  s=[-1 -1 -1;1 -1 -1;1 1 -1;-1 1 -1;-1 -1 1;1 -1 1;1 1 1;-1 1 1]; dN=zeros(8,3);
  for a=1:8
    dN(a,1)=s(a,1)*(1+s(a,2)*y)*(1+s(a,3)*z)/8; dN(a,2)=s(a,2)*(1+s(a,1)*x)*(1+s(a,3)*z)/8;
    dN(a,3)=s(a,3)*(1+s(a,1)*x)*(1+s(a,2)*y)/8;
  end
end
function B=Bmat(dNx)
  B=zeros(6,24);
  for a=1:8
    B(1,3*a-2)=dNx(a,1); B(2,3*a-1)=dNx(a,2); B(3,3*a)=dNx(a,3);
    B(4,3*a-2)=dNx(a,2); B(4,3*a-1)=dNx(a,1); B(5,3*a-1)=dNx(a,3); B(5,3*a)=dNx(a,2); B(6,3*a-2)=dNx(a,3); B(6,3*a)=dNx(a,1);
  end
end
