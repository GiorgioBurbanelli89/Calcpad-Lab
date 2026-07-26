% =====================================================================================
%  PROBETA A COMPRESION - PUSHOVER ANIMADO -> BANDA DIAGONAL DE CORTANTE (como STKO/OpenSees)
% -------------------------------------------------------------------------------------
%  Se empuja el tope hacia ABAJO con desplazamiento CRECIENTE (pushover). Los extremos estan
%  CONFINADOS por friccion (ux=0 arriba y abajo) -> se forman BANDAS DIAGONALES de cortante
%  (los conos de la probeta). El dano de traccion smeared PROPAGA en las diagonales y la
%  grieta CRECE frente a tus ojos: set(...)+drawnow anima cada paso hasta ver la banda.
%  Igual en MATLAB 2017a y en Calcpad Lab. Unidades: N, mm, MPa.
% =====================================================================================
W=150; H=300; t=1; nx=20; ny=40;                 % probeta 150x300 (proporcion ASTM)
E=30000; nu=0.2; ft=2.6;
umax=3.0; nstep=120; nstop=90; visc=0.004;
D0=E/(1-nu^2)*[1 nu 0; nu 1 0; 0 0 (1-nu)/2];
gp=[-1 -1;1 -1;1 1;-1 1]/sqrt(3);
% ---- malla Q4 ----
nid=@(i,j) j*(nx+1)+i+1; XY=zeros((nx+1)*(ny+1),2);
for j=0:ny, for i=0:nx, XY(nid(i,j),:)=[W*i/nx, H*j/ny]; end, end
els=zeros(nx*ny,4); e=0;
for j=0:ny-1, for i=0:nx-1, e=e+1; els(e,:)=[nid(i,j) nid(i+1,j) nid(i+1,j+1) nid(i,j+1)]; end, end
NE=size(els,1); NN=size(XY,1); ng=2*NN;
Bc=cell(NE,1); Ke0=cell(NE,1); Dof=zeros(NE,8); Vole=zeros(NE,1); cent=zeros(NE,2);
for ee=1:NE
  co=XY(els(ee,:),:); Ke=zeros(8); vv=0;
  for q=1:4, dN=shp(gp(q,1),gp(q,2)); J=dN*co; dJ=abs(det(J)); B=Bm(J\dN); Ke=Ke+B'*D0*B*dJ*t; vv=vv+dJ*t; end
  dN0=shp(0,0); Bc{ee}=Bm((dN0*co)\dN0); Ke0{ee}=Ke; Vole(ee)=vv; cent(ee,:)=mean(co,1);
  d=zeros(1,8); for a=1:4, d(2*a-1:2*a)=[2*els(ee,a)-1 2*els(ee,a)]; end, Dof(ee,:)=d;
end
I=zeros(NE*64,1); Jj=I; V0=I;
for ee=1:NE, d=Dof(ee,:); Ke=Ke0{ee};
  I((ee-1)*64+1:ee*64)=repmat(d',8,1); Jj((ee-1)*64+1:ee*64)=reshape(repmat(d,8,1),[],1); V0((ee-1)*64+1:ee*64)=Ke(:);
end
% imperfeccion: fluencia por cortante (von Mises); poca dispersion + SEMILLA debil en una esquina
% para NUCLEAR una sola banda diagonal que propaga (como la localizacion de STKO).
rng(7); fyE=14*(0.98+0.04*rand(NE,1));
for ee=1:NE                                    % esquina inferior-izquierda debil -> banda diagonal /
  if cent(ee,1)<0.18*W && cent(ee,2)<0.18*H, fyE(ee)=0.55*14; end
end
% ---- apoyos: base empotrada (ux=uy=0, friccion); tope confinado (ux=0) + uy impuesto (baja) ----
base=[]; top=[]; for i=0:nx, base=[base nid(i,0)]; top=[top nid(i,ny)]; end
fix=[]; for n=base, fix=[fix 2*n-1 2*n]; end            % base: ux=uy=0
for n=top, fix=[fix 2*n-1]; end                          % tope: ux=0 (friccion/confinado)
imp=2*top; fix=[fix imp]; free=setdiff(1:ng,fix);        % tope: uy impuesto (compresion)
% ---- figura + patch (una vez); el loop solo actualiza (retenido) ----
dv=zeros(NE,1); epl=zeros(NE,3); epq=zeros(NE,1); U=zeros(ng,1); dts=1/nstep;
sf=8; figure; caxis([0 1]); colormap(flipud(jet));
ph=patch('Vertices',XY,'Faces',els,'FaceVertexCData',zeros(NE,1),'FaceColor','flat','EdgeColor',[.8 .8 .8],'LineWidth',0.2);
axis equal; xlabel('x (mm)'); ylabel('y (mm)'); ht=title('PUSHOVER COMPRESION');
% ---- PUSHOVER: desplazamiento creciente hacia abajo + dano traccion smeared (ANIMA en vivo) ----
for st=1:nstop
  uy=-umax*st/nstep;
  for it=1:10
    Vv=V0.*repelem(1-dv,64); K=sparse(I,Jj,Vv,ng,ng);
    Fpl=zeros(ng,1);
    for ee=1:NE, Fpl(Dof(ee,:))=Fpl(Dof(ee,:))+(1-dv(ee))*Vole(ee)*(Bc{ee}'*(D0*epl(ee,:)')); end
    U=zeros(ng,1); U(imp)=uy; R=Fpl-K*U; U(free)=K(free,free)\R(free);
    dold=dv;
    for ee=1:NE
      eps=Bc{ee}*U(Dof(ee,:)')-epl(ee,:)'; sb=D0*eps;
      sve=sqrt(sb(1)^2 - sb(1)*sb(2) + sb(2)^2 + 3*sb(3)^2);   % von Mises 2D (cortante)
      if sve>fyE(ee)                                            % fluencia por cortante -> banda diagonal
        m=[2*sb(1)-sb(2); 2*sb(2)-sb(1); 6*sb(3)]/(2*max(sve,1e-9));   % direccion desviadora
        dl=(sve-fyE(ee))/(m'*D0*m);
        if dl>0, epl(ee,:)=epl(ee,:)+(dl*m)'; epq(ee)=epq(ee)+dl; end
      end
      if epq(ee)<=0, db=0; else db=min(0.9,interp1([0 0.0006 0.0020],[0 0.6 0.9],min(epq(ee),0.0020))); end
      dv(ee)=(dv(ee)+(dts/visc)*db)/(1+dts/visc);
    end
    if max(abs(dv-dold))<3e-3, break; end
  end
  XYd=XY+sf*[U(1:2:end) U(2:2:end)];                       % --- ANIMA: probeta deformada + grieta ---
  set(ph,'Vertices',XYd,'FaceVertexCData',dv);
  set(ht,'String',sprintf('PUSHOVER COMPRESION - paso %d/%d  acort=%.2fmm  dano max=%.2f (banda cortante)',st,nstop,-uy,max(dv)));
  drawnow;
end
fprintf('FIN: dano max=%.2f, elementos agrietados=%d (banda diagonal de cortante)\n', max(dv), sum(dv>0.3));

% ===================== helpers (malla Q4) =====================
function dN=shp(xi,et)
dN=0.25*[-(1-et) (1-et) (1+et) -(1+et); -(1-xi) -(1+xi) (1+xi) (1-xi)];
end
function B=Bm(dNx)
B=zeros(3,8); for a=1:4, B(1,2*a-1)=dNx(1,a); B(2,2*a)=dNx(2,a); B(3,2*a-1)=dNx(2,a); B(3,2*a)=dNx(1,a); end
end
