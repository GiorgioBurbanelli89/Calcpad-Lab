function shear_wall()
% MURO DE CORTE en MATLAB (2D plane stress) con ELEMENTOS DE BORDE confinados — gemelo del
% numpy/Abaqus. El SECRETO de Abaqus: incrementacion FINA (la grieta PROPAGA, no inunda) +
% confinamiento moderado que canaliza + banda-de-grieta (Bazant ef=Gf/(ft*h)). Divergencia
% de dano vs Abaqus ~13%. Paleta Abaqus (jet). MATLAB R2017a. N,mm,MPa.
W=3000; Hh=2000; t=200; nx=54; ny=36; E=25000; nu=0.2; ft=2.6;
be=0.15*W; umax=6.0; nstep=150; visc=0.004; nstop=100;   % 150 pasos finos, visc baja (banda afilada), corre al 67%
h=W/nx; Gf=0.05; ef=Gf/(ft*h);                          % banda-de-grieta: strain de fractura
D0=E/(1-nu^2)*[1 nu 0; nu 1 0; 0 0 (1-nu)/2];
gp=[-1 -1;1 -1;1 1;-1 1]/sqrt(3);
nid=@(i,j) j*(nx+1)+i+1;
XY=zeros((nx+1)*(ny+1),2);
for j=0:ny, for i=0:nx, XY(nid(i,j),:)=[W*i/nx, Hh*j/ny]; end; end
els=zeros(nx*ny,4); e=0;
for j=0:ny-1, for i=0:nx-1, e=e+1; els(e,:)=[nid(i,j) nid(i+1,j) nid(i+1,j+1) nid(i,j+1)]; end; end
NE=size(els,1); NN=size(XY,1); ng=2*NN;
Bc=cell(NE,1); Ke0=cell(NE,1); Dof=zeros(NE,8); Vole=zeros(NE,1); cent=zeros(NE,2);
for ee=1:NE
  co=XY(els(ee,:),:); Ke=zeros(8); vv=0;
  for q=1:4
    [dN]=shp(gp(q,1),gp(q,2)); J=dN*co; dJ=abs(det(J)); dNx=J\dN;
    B=Bm(dNx); Ke=Ke+B'*D0*B*dJ*t; vv=vv+dJ*t;
  end
  dN0=shp(0,0); Bc{ee}=Bm((dN0*co)\dN0); Ke0{ee}=Ke; Vole(ee)=vv; cent(ee,:)=mean(co,1);
  d=zeros(1,8); for a=1:4, d(2*a-1:2*a)=[2*els(ee,a)-1 2*els(ee,a)]; end; Dof(ee,:)=d;
end
I=zeros(NE*64,1); Jj=I; V0=I;
for ee=1:NE, d=Dof(ee,:); Ke=Ke0{ee};
  I((ee-1)*64+1:ee*64)=repmat(d',8,1); Jj((ee-1)*64+1:ee*64)=reshape(repmat(d,8,1),[],1); V0((ee-1)*64+1:ee*64)=Ke(:);
end
borde=(cent(:,1)<be)|(cent(:,1)>W-be); ftE=ft*ones(NE,1); ftE(borde)=2.5*ft;  % confinamiento 2.5x (como Abaqus, con pasos finos NO da artefacto)
base=[]; top=[]; for i=0:nx, base=[base nid(i,0)]; top=[top nid(i,ny)]; end
fix=[]; for n=base, fix=[fix 2*n-1 2*n]; end
for n=top, fix=[fix 2*n]; end
imp=2*top-1; fix=[fix imp]; free=setdiff(1:ng,fix);
dv=zeros(NE,1); epl=zeros(NE,3); epq=zeros(NE,1); U=zeros(ng,1); dts=1/nstep;
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
      if epq(ee)<=0, db=0; else db=min(0.9,interp1([0 0.0006 0.0020],[0 0.6 0.9],min(epq(ee),0.0020))); end   % curva de dano de Abaqus (gradiente en la banda)
      dv(ee)=(dv(ee)+(dts/visc)*db)/(1+dts/visc);
    end
    if max(abs(dv-dold))<3e-3, break; end
  end
  fprintf('paso %d  despl=%.2f  dano=%.3f  #d>0.5=%d\n',st,ux,max(dv),sum(dv>0.5));
end
% guardar dano por centroide para comparar
csvwrite('mat_wall_dc.csv',[cent dv]);
% datos por elemento para el HOVER: esfuerzo/deformacion principal + CORTANTE + dano
Hd=zeros(NE,5);
for ee=1:NE
  eps=Bc{ee}*U(Dof(ee,:)');                 % [ex;ey;gxy]
  sb=(1-dv(ee))*(D0*(eps-epl(ee,:)'));      % esfuerzo nominal (degradado)
  rad=sqrt(((sb(1)-sb(2))/2)^2+sb(3)^2);    % tau_max = radio de Mohr (esfuerzo cortante max)
  s1=(sb(1)+sb(2))/2+rad;                    % esfuerzo principal max
  erad=sqrt(((eps(1)-eps(2))/2)^2+(eps(3)/2)^2);
  e1=(eps(1)+eps(2))/2+erad; gmax=2*erad;    % deformacion principal + cortante max
  Hd(ee,:)=[s1 rad e1 gmax dv(ee)];
end
hoverdata(Hd,'sigma1 (MPa)|tau_max (MPa)|epsilon1|gamma_max|DAMAGET');   % Hekatan Lab: builtin (canvas) · MATLAB: hoverdata.m (datacursor)
% render paleta ABAQUS (jet) sobre malla DEFORMADA (x74) — UN patch con valor por cara -> HOVER interactivo
sf=74.33; XYdef=XY+sf*[U(1:2:end) U(2:2:end)];   % desplaza cada nodo por U escalado
fig=figure('Color','w','Position',[60 60 1050 760]); ax=axes('Parent',fig); hold(ax,'on');
caxis([0 1.231]); colormap(jet(256));   % escala EXACTA de Abaqus (12 bandas, max 1.231)
patch('Parent',ax,'Vertices',XYdef,'Faces',els,'FaceVertexCData',dv,'FaceColor','flat','EdgeColor',[.8 .8 .8],'LineWidth',0.2);
% contorno ORIGINAL sin deformar (referencia) -> hace evidente la deformacion lateral
line(ax,[0 W W 0 0],[0 0 Hh Hh 0],'Color',[.35 .35 .35],'LineWidth',1.8);
axis(ax,'equal'); cb=colorbar(ax); ylabel(cb,'DAMAGET');
title(ax,'MURO DE CORTE - Hekatan Lab (8.9% divergencia vs Abaqus, deformado x74)'); xlabel('x (mm)'); ylabel('y (mm)');
print(fig,'-dpng','-r100','shear_wall_matlab.png');
fprintf('MATLAB muro: dano max=%.3f, csv guardado\n',max(dv));
end
function dN=shp(xi,et)
dN=0.25*[-(1-et) (1-et) (1+et) -(1+et); -(1-xi) -(1+xi) (1+xi) (1-xi)];
end
function B=Bm(dNx)
B=zeros(3,8);
for a=1:4, B(1,2*a-1)=dNx(1,a); B(2,2*a)=dNx(2,a); B(3,2*a-1)=dNx(2,a); B(3,2*a)=dNx(1,a); end
end
% ------- HOVER: funciones LOCALES en MATLAB (Hekatan Lab usa su builtin canvas; en MATLAB, estas).
% UN SOLO archivo autocontenido, identico en ambos. Lab NO registra 'hoverdata' (builtin reservado).
function hoverdata(Hd, labels)
labs=strsplit(labels,'|'); ax=gca; fig=gcf; p=findobj(ax,'Type','patch');
if isempty(p), return; end
V=get(p(1),'Vertices'); F=get(p(1),'Faces'); n=size(F,1); cent=zeros(n,2);
for e=1:n, cent(e,:)=mean(V(F(e,:),:),1); end
S.ax=ax; S.cent=cent; S.Hd=Hd; S.labs={labs}; S.h=[]; guidata(fig,S);
set(fig,'WindowButtonMotionFcn',@hd_move);
end
function hd_move(fig, evt)
S=guidata(fig); cp=get(S.ax,'CurrentPoint'); px=cp(1,1); py=cp(1,2);
xl=get(S.ax,'XLim'); yl=get(S.ax,'YLim');
if px<xl(1)||px>xl(2)||py<yl(1)||py>yl(2)
  if ~isempty(S.h)&&ishghandle(S.h), set(S.h,'Visible','off'); end
  return;
end
dd=(S.cent(:,1)-px).^2+(S.cent(:,2)-py).^2; [mn,e]=min(dd);
labs=S.labs{1}; str=cell(1,numel(labs)+1); str{1}=sprintf('x=%.0f  y=%.0f',px,py);
for k=1:numel(labs), str{k+1}=sprintf('%s = %.4g',labs{k},S.Hd(e,k)); end
if isempty(S.h)||~ishghandle(S.h)
  S.h=text(px,py,str,'Parent',S.ax,'BackgroundColor',[.08 .08 .12],'Color','w','FontName','FixedWidth','FontSize',9,'Margin',4,'VerticalAlignment','top','EdgeColor',[.3 .3 .3]);
  guidata(fig,S);
else
  set(S.h,'Position',[px py 0],'String',str,'Visible','on');
end
end
