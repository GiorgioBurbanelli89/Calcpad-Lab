% =====================================================================================
%  MURO A CORTANTE - PUSHOVER NO LINEAL ANIMADO EN VIVO (Calcpad Lab)
% -------------------------------------------------------------------------------------
%  Empuja el tope del muro con desplazamiento CRECIENTE (monotono). En cada paso el
%  hormigon agrieta donde la traccion principal supera ft, y el dano PROPAGA en banda
%  diagonal (~45 grados, muro chato H/W<1.5 -> manda el cortante).
%  La grieta CRECE frente a tus ojos: se calcula y se dibuja paso a paso (set+drawnow).
%  Pasa el cursor por la malla para leer los numeros de cada elemento.
%  Unidades: N, mm, MPa.
% =====================================================================================
W=3000; H=2400; t=200; nx=40; ny=32;                 % H/W=0.80 -> CHATO -> cortante (diagonal)
E=25000; nu=0.2; ft=2.6; be=0.15*W;                  % ft traccion; be = ancho borde confinado
umax=6.0; nstep=150; nstop=80; visc=0.004;           % pasos: la grieta propaga, no inunda
D0=E/(1-nu^2)*[1 nu 0; nu 1 0; 0 0 (1-nu)/2];
gp=[-1 -1;1 -1;1 1;-1 1]/sqrt(3);

% ---- malla Q4 ----
nid=@(i,j) j*(nx+1)+i+1; XY=zeros((nx+1)*(ny+1),2);
for j=0:ny, for i=0:nx, XY(nid(i,j),:)=[W*i/nx, H*j/ny]; end; end
els=zeros(nx*ny,4); e=0;
for j=0:ny-1, for i=0:nx-1, e=e+1; els(e,:)=[nid(i,j) nid(i+1,j) nid(i+1,j+1) nid(i,j+1)]; end; end
NE=size(els,1); NN=size(XY,1); ng=2*NN;
Bc=cell(NE,1); Ke0=cell(NE,1); Dof=zeros(NE,8); Vole=zeros(NE,1); cent=zeros(NE,2);
for ee=1:NE
  co=XY(els(ee,:),:); Ke=zeros(8); vv=0;
  for q=1:4, dN=shp(gp(q,1),gp(q,2)); J=dN*co; dJ=abs(det(J)); B=Bm(J\dN); Ke=Ke+B'*D0*B*dJ*t; vv=vv+dJ*t; end
  dN0=shp(0,0); Bc{ee}=Bm((dN0*co)\dN0); Ke0{ee}=Ke; Vole(ee)=vv; cent(ee,:)=mean(co,1);
  d=zeros(1,8); for a=1:4, d(2*a-1:2*a)=[2*els(ee,a)-1 2*els(ee,a)]; end; Dof(ee,:)=d;
end
I=zeros(NE*64,1); Jj=I; V0=I;
for ee=1:NE, d=Dof(ee,:); Ke=Ke0{ee};
  I((ee-1)*64+1:ee*64)=repmat(d',8,1); Jj((ee-1)*64+1:ee*64)=reshape(repmat(d,8,1),[],1); V0((ee-1)*64+1:ee*64)=Ke(:);
end
borde=(cent(:,1)<be)|(cent(:,1)>W-be); ftE=ft*ones(NE,1); ftE(borde)=2.5*ft;   % bordes confinados

% ---- apoyos: base empotrada, tope guiado en x (se impone ux) ----
base=[]; top=[]; for i=0:nx, base=[base nid(i,0)]; top=[top nid(i,ny)]; end
fix=[]; for n=base, fix=[fix 2*n-1 2*n]; end                    % base empotrada
for n=top, fix=[fix 2*n]; end                                   % tope: uy fijo (viga de carga rigida)
imp=2*top-1; fix=[fix imp]; free=setdiff(1:ng,fix);            % + ux impuesto

% ---- figura + patch: se crea UNA vez; el loop solo actualiza (modo MATLAB retenido) ----
dv=zeros(NE,1); epl=zeros(NE,3); epq=zeros(NE,1); U=zeros(ng,1); dts=1/nstep; Vbase=zeros(nstop,1); dr=zeros(nstop,1);
sf=60; figure; caxis([0 1]); colormap(jet(256));
ph=patch('Vertices',XY,'Faces',els,'FaceVertexCData',zeros(NE,1),'FaceColor','flat','EdgeColor',[.8 .8 .8],'LineWidth',0.2);
axis equal; xlabel('x (mm)'); ylabel('y (mm)'); ht=title('PUSHOVER CORTANTE');

% ---- PUSHOVER: desplazamiento creciente + dano de traccion smeared (ANIMA en vivo) ----
for st=1:nstop
  ux=umax*st/nstep;
  for it=1:10
    Vv=V0.*repelem(1-dv,64); K=sparse(I,Jj,Vv,ng,ng);
    Fpl=zeros(ng,1);
    for ee=1:NE, Fpl(Dof(ee,:))=Fpl(Dof(ee,:))+(1-dv(ee))*Vole(ee)*(Bc{ee}'*(D0*epl(ee,:)')); end
    U=zeros(ng,1); U(imp)=ux; R=Fpl-K*U; U(free)=K(free,free)\R(free);
    dold=dv;
    for ee=1:NE
      eps=Bc{ee}*U(Dof(ee,:)')-epl(ee,:)'; sb=D0*eps;
      savg=(sb(1)+sb(2))/2; rad=sqrt(((sb(1)-sb(2))/2)^2+sb(3)^2); s1=savg+rad;
      th=0.5*atan2(2*sb(3), sb(1)-sb(2));
      if s1>ftE(ee)
        c=cos(th); s2=sin(th); m=[c*c; s2*s2; 2*c*s2]; dl=(s1-ftE(ee))/(m'*D0*m);
        if dl>0, epl(ee,:)=epl(ee,:)+(dl*m)'; epq(ee)=epq(ee)+dl; end
      end
      if epq(ee)<=0, db=0; else db=min(0.9,interp1([0 0.0006 0.0020],[0 0.6 0.9],min(epq(ee),0.0020))); end
      dv(ee)=(dv(ee)+(dts/visc)*db)/(1+dts/visc);
    end
    if max(abs(dv-dold))<3e-3, break; end
  end
  R=full(sparse(I,Jj,V0.*repelem(1-dv,64),ng,ng))*U;   % cortante basal = -reaccion en base
  Vbase(st)=abs(sum(R(2*base-1)))/9806.65; dr(st)=ux/H*100;
  XYd=XY+sf*[U(1:2:end) U(2:2:end)];                          % --- ANIMA: muro deformado + grieta ---
  set(ph,'Vertices',XYd,'FaceVertexCData',dv);
  set(ht,'String',sprintf('PUSHOVER CORTANTE - paso %d/%d: V=%.0f tonf, deriva %.2f%%, deform x%d',st,nstop,Vbase(st),dr(st),sf));
  drawnow;                                                    % <-- repinta el frame en vivo
end
fprintf('FIN: V pico=%.0f tonf, deriva final=%.2f%%, dano max=%.2f\n', max(Vbase), dr(nstop), max(dv));

% ===================== helpers (malla Q4) =====================
function dN=shp(xi,et)
dN=0.25*[-(1-et) (1-et) (1+et) -(1+et); -(1-xi) -(1+xi) (1+xi) (1-xi)];
end
function B=Bm(dNx)
B=zeros(3,8); for a=1:4, B(1,2*a-1)=dNx(1,a); B(2,2*a)=dNx(2,a); B(3,2*a-1)=dNx(2,a); B(3,2*a)=dNx(1,a); end
end
