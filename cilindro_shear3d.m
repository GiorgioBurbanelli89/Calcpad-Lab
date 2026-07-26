% =====================================================================================
%  CILINDRO A COMPRESION -> BANDA DIAGONAL DE CORTANTE 3D (como STKO/OpenSees strain-loc)
% -------------------------------------------------------------------------------------
%  Cilindro 3D comprimido entre platos. Se siembra un PLANO DIAGONAL debil -> el cortante
%  LOCALIZA ahi (von Mises + softening) y el bloque superior DESLIZA en diagonal sobre el
%  inferior. Se colorea por DESPLAZAMIENTO (como STKO) sobre la deformada amplificada:
%  se VE la grieta = el corte diagonal donde el solido se desliza. Orbit 3D (Lab == MATLAB).
%  Unidades: N, mm, MPa.
% =====================================================================================
R=75; Lz=300; nx=10; ny=8; nz=20; E=30000; nu=0.2;
fy0=14; umax=3.5; nstep=28; visc=0.05;       % umax realista ~ STKO (antes 10 = exagerado)
la=E*nu/((1+nu)*(1-2*nu)); mu=E/(2*(1+nu));
D0=[la+2*mu la la 0 0 0; la la+2*mu la 0 0 0; la la la+2*mu 0 0 0; 0 0 0 mu 0 0; 0 0 0 0 mu 0; 0 0 0 0 0 mu];
g=1/sqrt(3); gp=zeros(8,3); q=0;
for a=[-g g], for b=[-g g], for c=[-g g], q=q+1; gp(q,:)=[a b c]; end, end, end
NN=(nx+1)*(ny+1)*(nz+1); XYZ=zeros(NN,3);
for k=0:nz, for j=0:ny, for i=0:nx
  u=-1+2*i/nx; v=-1+2*j/ny; x=R*u*sqrt(max(0,1-v*v/2)); y=R*v*sqrt(max(0,1-u*u/2));
  XYZ(nid(i,j,k,nx,ny),:)=[x y Lz*k/nz];
end, end, end
NE=nx*ny*nz; els=zeros(NE,8); eijk=zeros(NE,3); cent=zeros(NE,3); e=0;
for k=0:nz-1, for j=0:ny-1, for i=0:nx-1
  e=e+1; els(e,:)=[nid(i,j,k,nx,ny) nid(i+1,j,k,nx,ny) nid(i+1,j+1,k,nx,ny) nid(i,j+1,k,nx,ny) ...
                   nid(i,j,k+1,nx,ny) nid(i+1,j,k+1,nx,ny) nid(i+1,j+1,k+1,nx,ny) nid(i,j+1,k+1,nx,ny)];
  eijk(e,:)=[i j k]; cent(e,:)=mean(XYZ(els(e,:),:),1);
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
% ---- PLANO DIAGONAL debil (con ondulacion, como la superficie ondulada de STKO) ----
rng(7); fyE=fy0*(0.99+0.02*rand(NE,1)); tanb=1.3;
for e=1:NE
  zplane=Lz/2 + tanb*cent(e,1) + 18*sin(cent(e,2)/R*pi);   % plano diagonal ONDULADO (no recto)
  d2=abs(cent(e,3)-zplane)/sqrt(1+tanb^2);
  if d2<0.06*Lz, fyE(e)=0.30*fy0; end
end
% ---- BC: base empotrada; tope DESLIZA en X (ux libre) + uy=0 + uz impuesto ----
% (tope libre en X = puede deslizar como el plato de STKO -> el bloque superior corta en diagonal)
fixd=false(ng,1); imp=[];
for i=0:nx, for j=0:ny
  nb=nid(i,j,0,nx,ny); fixd(3*nb-2)=1; fixd(3*nb-1)=1; fixd(3*nb)=1;   % base empotrada
  nt=nid(i,j,nz,nx,ny); fixd(3*nt-1)=1; fixd(3*nt)=1; imp(end+1)=3*nt;  % tope: uy=0, uz impuesto, ux LIBRE (desliza)
end, end
free=find(~fixd);
% caras de piel (una vez)
fmap=[1 2 3 4;5 6 7 8;1 2 6 5;2 3 7 6;3 4 8 7;4 1 5 8];
dirn=[0 0 -1;0 0 1;0 -1 0;1 0 0;0 1 0;-1 0 0];
F=[];
for e=1:NE
  i=eijk(e,1); j=eijk(e,2); k=eijk(e,3);
  for ff=1:6
    ni=i+dirn(ff,1); nj=j+dirn(ff,2); nk=k+dirn(ff,3);
    if ni<0||ni>=nx||nj<0||nj>=ny||nk<0||nk>=nz, F(end+1,:)=els(e,fmap(ff,:)); end
  end
end
% figura+patch UNA vez; el pushover ANIMA en vivo (set+drawnow) y arma un GIF frame a frame
sc=6; figure; caxis([0 umax]); colormap(jet);
ph=patch('Faces',F,'Vertices',XYZ,'FaceVertexCData',zeros(NN,1),'FaceColor','interp','EdgeColor','none');
axis equal; axis manual; view(35,12); rotate3d on; colorbar; xlabel('x'); ylabel('y'); zlabel('z');
ht=title('BANDA DIAGONAL DE CORTANTE 3D');
gifname='cilindro_shear3d.gif';
dv=zeros(NE,1); epl=zeros(NE,6); epq=zeros(NE,1); U=zeros(ng,1); dts=1/nstep;
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
      p=(sb(1)+sb(2)+sb(3))/3; dvx=sb(1)-p; dvy=sb(2)-p; dvz=sb(3)-p;
      sve=sqrt(1.5*(dvx^2+dvy^2+dvz^2)+3*(sb(4)^2+sb(5)^2+sb(6)^2));   % von Mises 3D
      if sve>fyE(e)
        m=1.5*[dvx;dvy;dvz;2*sb(4);2*sb(5);2*sb(6)]/max(sve,1e-9);
        dl=(sve-fyE(e))/max(m'*D0*m,1e-6); epl(e,:)=epl(e,:)+(dl*m)'; epq(e)=epq(e)+dl;
      end
      if epq(e)<=0, db=0; else db=min(0.85,interp1([0 0.0012 0.005],[0 0.55 0.85],min(epq(e),0.005))); end   % cap 0.85 = corte nitido sin huecos
      dv(e)=(dv(e)+(dts/visc)*db)/(1+dts/visc);
    end
    if max(abs(dv-dold))<3e-3, break; end
  end
  Umag=sqrt(U(1:3:end).^2+U(2:3:end).^2+U(3:3:end).^2);   % |U| por nodo (como STKO)
  set(ph,'Vertices',XYZ+sc*reshape(U,3,[])','FaceVertexCData',Umag);   % ANIMA en vivo (Lab y MATLAB)
  set(ht,'String',sprintf('Cilindro cortante 3D - paso %d/%d  acort=%.2fmm  |U|max=%.2fmm',st,nstep,-uz,max(Umag)));
  drawnow;
end
fprintf('FIN: dano max=%.2f (banda diagonal de cortante)\n', max(dv));

% ===================== sub-funciones =====================
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
