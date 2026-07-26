% =====================================================================================
%  CILINDRO A COMPRESION - CDP (dano) - ORBIT 3D (Calcpad Lab == MATLAB 2017a)
% -------------------------------------------------------------------------------------
%  FEM 3D C3D8 + dano a compresion Drucker-Prager (calibrado fb0/fc0=1.16). Confinamiento
%  de platos -> abarrilamiento. Se dibuja el cilindro 3D coloreado por el DANO (jet_r) con
%  PATCH -> en MATLAB 2017a es orbit 3D (rotate3d), en Calcpad Lab es orbit 3D (canvas WebGL).
%  MISMO script en ambos. Unidades: N, mm, MPa.
% =====================================================================================
R=75; Lz=300; nx=6; ny=6; nz=10; E=30000; nu=0.2;
fc=25; fb=1.16*fc; dp_a=(fb-fc)/((2*fb/3)-(fc/3)); dp_k=fc-dp_a*(fc/3);
umax=5.0; nstep=16; visc=0.05;
la=E*nu/((1+nu)*(1-2*nu)); mu=E/(2*(1+nu));
D0=[la+2*mu la la 0 0 0; la la+2*mu la 0 0 0; la la la+2*mu 0 0 0; 0 0 0 mu 0 0; 0 0 0 0 mu 0; 0 0 0 0 0 mu];
g=1/sqrt(3); gp=zeros(8,3); q=0;
for a=[-g g], for b=[-g g], for c=[-g g], q=q+1; gp(q,:)=[a b c]; end, end, end
% ---- malla squircle ----
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
ng=3*NN; Dof=zeros(NE,24); Bc=cell(NE,1); Vole=zeros(NE,1);
Ir=zeros(NE*576,1); Jc=Ir; V0=Ir;
for e=1:NE
  el=els(e,:); co=XYZ(el,:); d=zeros(1,24);
  for a=1:8, d(3*a-2:3*a)=[3*el(a)-2 3*el(a)-1 3*el(a)]; end
  Dof(e,:)=d; Ke=zeros(24); vv=0;
  for q=1:8, dN=dshp(gp(q,1),gp(q,2),gp(q,3)); Jm=dN'*co; dNx=(Jm\dN')'; B=Bmat(dNx);
    Ke=Ke+B'*D0*B*abs(det(Jm)); vv=vv+abs(det(Jm)); end
  Vole(e)=vv; dN0=dshp(0,0,0); J0=dN0'*co; Bc{e}=Bmat((J0\dN0')');
  idx=(e-1)*576+(1:576); Ir(idx)=reshape(repmat(d,24,1),[],1); Jc(idx)=reshape(repmat(d,1,24),[],1); V0(idx)=Ke(:);
end
fixd=false(ng,1); imp=[];
for i=0:nx, for j=0:ny
  nb=nid(i,j,0,nx,ny); fixd(3*nb-2)=1; fixd(3*nb-1)=1; fixd(3*nb)=1;
  nt=nid(i,j,nz,nx,ny); fixd(3*nt-2)=1; fixd(3*nt-1)=1; fixd(3*nt)=1; imp(end+1)=3*nt;
end, end
free=find(~fixd);
% ---- Newton por pasos ----
dv=zeros(NE,1); epl=zeros(NE,6); epc=zeros(NE,1); U=zeros(ng,1); dts=1/nstep;
for st=1:nstep
  uz=-umax*st/nstep;
  for it=1:8
    Vv=V0.*reshape(repmat((1-dv)',576,1),[],1); K=sparse(Ir,Jc,Vv,ng,ng);
    Fpl=zeros(ng,1);
    for e=1:NE, Fpl(Dof(e,:))=Fpl(Dof(e,:))+((1-dv(e))*Vole(e)*(Bc{e}'*(D0*epl(e,:)'))); end
    U=zeros(ng,1); U(imp)=uz; rhs=Fpl-K*U; U(free)=K(free,free)\rhs(free);
    dold=dv;
    for e=1:NE
      eps=Bc{e}*U(Dof(e,:))-epl(e,:)'; sb=D0*eps;
      pbar=-(sb(1)+sb(2)+sb(3))/3; tr=(sb(1)+sb(2)+sb(3))/3; dev=sb-[1;1;1;0;0;0]*tr;
      qv=sqrt(1.5*(dev(1)^2+dev(2)^2+dev(3)^2+2*(dev(4)^2+dev(5)^2+dev(6)^2)));
      F=qv-dp_a*pbar-dp_k;
      if F>0 && pbar>0
        nq=(1.5/max(qv,1e-9))*[dev(1);dev(2);dev(3);2*dev(4);2*dev(5);2*dev(6)];
        dl=F/max(nq'*D0*nq,1e-6); epl(e,:)=epl(e,:)+(dl*nq)'; epc(e)=epc(e)+dl;
      end
      dc=dcurve(epc(e)); dv(e)=(dv(e)+(dts/visc)*dc)/(1+dts/visc);
    end
    if max(abs(dv-dold))<3e-3, break; end
  end
  fprintf('paso %2d  acort=%.2fmm  DANO max=%.3f\n',st,-uz,max(dv));
end
% ---- caras de piel + PATCH 3D coloreado por dano (orbit 3D) ----
fmap=[1 2 3 4;5 6 7 8;1 2 6 5;2 3 7 6;3 4 8 7;4 1 5 8];
dirn=[0 0 -1;0 0 1;0 -1 0;1 0 0;0 1 0;-1 0 0];
F=[]; C=[]; sc=15; Xd=XYZ+sc*reshape(U,3,[])';
for e=1:NE
  i=eijk(e,1); j=eijk(e,2); k=eijk(e,3);
  for ff=1:6                                   % malla-grid completa: cara exterior = vecino fuera del grid
    ni=i+dirn(ff,1); nj=j+dirn(ff,2); nk=k+dirn(ff,3);
    ext = ni<0||ni>=nx||nj<0||nj>=ny||nk<0||nk>=nz;
    if ext, F(end+1,:)=els(e,fmap(ff,:)); C(end+1,1)=dv(e); end
  end
end
figure; patch('Faces',F,'Vertices',Xd,'FaceVertexCData',C,'FaceColor','flat','EdgeColor',[.4 .4 .4]);
axis equal; view(3); rotate3d on; colormap(flipud(jet)); caxis([0 0.9]); colorbar;
title(sprintf('Cilindro compresion CDP - orbit 3D (DANO max=%.3f)',max(dv)));
xlabel('x'); ylabel('y'); zlabel('z');

% ================= sub-funciones =================
function d=nid(i,j,k,nx,ny), d=k*(nx+1)*(ny+1)+j*(nx+1)+i+1; end
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
    B(4,3*a-2)=dNx(a,2); B(4,3*a-1)=dNx(a,1); B(5,3*a-1)=dNx(a,3); B(5,3*a)=dNx(a,2);
    B(6,3*a-2)=dNx(a,3); B(6,3*a)=dNx(a,1);
  end
end
function y=dcurve(epc)
  xx=[0 0.0008 0.012]; yy=[0 0.2 0.7];
  if epc<=0, y=0; elseif epc>=xx(end), y=0.7; else y=interp1(xx,yy,epc); end; y=min(0.9,y);
end
