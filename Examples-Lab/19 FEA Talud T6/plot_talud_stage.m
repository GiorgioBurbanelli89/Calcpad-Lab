function plot_talud_stage(X,Y,ELE,EMAT,Fs,Fa,MAT,u,fs,gfs,name,fn,si,campo)
if nargin<14; campo='dx'; end   % 'dx' | 'dz' (asiento +, como GEO5) | 'd' (resultante)
% GRAFICA del talud Demo04 (MATLAB 2017a / Hekatan Lab) = talud_geo5_plot.py (matplotlib):
% campo d_x [mm] (signo GEO5) en rejilla regular por coordenadas baricentricas, 12 bandas
% uniformes de 0 a un tope redondo con la paleta de GEO5, isolineas; y ENCIMA la geometria:
% malla T6 (gris), contorno del talud (negro), interfaz suelo 1 / suelo 2 (negro a trazos) con
% las etiquetas de cada suelo, y las cargas de la etapa (sobrecarga q en la corona, ancla).
nn=numel(X); ne=size(ELE,1);
if strcmp(campo,'dz'); dx=-u(2:2:end)*1000; lab='d_z';
elseif strcmp(campo,'d'); dx=sqrt(u(1:2:end).^2+u(2:2:end).^2)*1000; lab='d';
else; dx=-u(1:2:end)*1000; lab='d_x'; end   % campo en mm, signo de GEO5
NX=220; NZ=170;
xmin=min(X); xmax=max(X); zmin=min(Y); zmax=max(Y);
xg=linspace(xmin,xmax,NX); zg=linspace(zmin,zmax,NZ);
Z=nan(NZ,NX); dxg=(xmax-xmin)/(NX-1); dzg=(zmax-zmin)/(NZ-1);
for e=1:ne
  n0=ELE(e,1); n1=ELE(e,2); n2=ELE(e,3);
  ax=X(n0); ay=Y(n0); bx=X(n1); by=Y(n1); cx=X(n2); cy=Y(n2);
  det=(by-cy)*(ax-cx)+(cx-bx)*(ay-cy);
  i0=max(floor((min([ax bx cx])-xmin)/dxg)+1,1); i1=min(ceil((max([ax bx cx])-xmin)/dxg)+1,NX);
  j0=max(floor((min([ay by cy])-zmin)/dzg)+1,1); j1=min(ceil((max([ay by cy])-zmin)/dzg)+1,NZ);
  v0=dx(n0); v1=dx(n1); v2=dx(n2);
  for j=j0:j1
    zz=zg(j);
    for i=i0:i1
      xx=xg(i);
      L1=((by-cy)*(xx-cx)+(cx-bx)*(zz-cy))/det;
      L2=((cy-ay)*(xx-cx)+(ax-cx)*(zz-cy))/det;
      L3=1-L1-L2;
      if L1>=-1e-9 && L2>=-1e-9 && L3>=-1e-9; Z(j,i)=L1*v0+L2*v1+L3*v2; end
    end
  end
end
% ---- aristas: clave numerica a*(nn+1)+b con a<b; contorno = aparece 1 vez; interfaz = 2 suelos ----
K=zeros(3*ne,1); EA=zeros(3*ne,1); EB=zeros(3*ne,1); EM=zeros(3*ne,1); k=0;
pares=[1 2; 2 3; 3 1];
for e=1:ne
  for r=1:3
    a=ELE(e,pares(r,1)); b=ELE(e,pares(r,2)); if a>b; t=a; a=b; b=t; end
    k=k+1; K(k)=a*(nn+1)+b; EA(k)=a; EB(k)=b; EM(k)=EMAT(e);
  end
end
xm=[]; ym=[]; xb=[]; yb=[]; xi=[]; yi=[]; visto=zeros(size(K));
for i=1:numel(K)
  if visto(i); continue; end
  same=find(K==K(i)); visto(same)=1;
  xm=[xm X(EA(i)) X(EB(i)) NaN]; ym=[ym Y(EA(i)) Y(EB(i)) NaN];                  % malla
  if numel(same)==1; xb=[xb X(EA(i)) X(EB(i)) NaN]; yb=[yb Y(EA(i)) Y(EB(i)) NaN]; end   % contorno
  if numel(same)>1 && any(EM(same)~=EM(i)); xi=[xi X(EA(i)) X(EB(i)) NaN]; yi=[yi Y(EA(i)) Y(EB(i)) NaN]; end  % interfaz
end
esq=unique(ELE(:,1:3)); vmin=min(dx(esq)); vmax=max(dx(esq));   % extremos de la barra = nodos ESQUINA (medido en la GUI de GEO5, 2026-09-04)
lv=geo5_levels(vmin,vmax); nb=numel(lv)-1;   % ESCALA DE GEO5 (bordes de banda de su barra)
[XX,ZZ]=meshgrid(xg,zg);
Zc=Z; Zc(Z<lv(1))=lv(1); Zc(Z>lv(end))=lv(end);   % recorte con indexado logico: el NaN de fuera del talud se conserva
% colores de la barra de GEO5, MUESTREADOS de sus capturas: 11 bandas (etapa 2) y 13 (etapa 1); otro nb = interpolar la de 13
G5_11=[0 0 255; 0 0 176; 0 88 88; 0 176 0; 0 255 0; 128 255 0; 255 255 0; 255 128 0; 255 64 0; 255 0 0; 176 0 0]/255;
G5_13=[0 0 255; 0 0 224; 0 0 176; 0 176 0; 0 216 0; 0 255 0; 128 255 0; 255 255 0; 255 192 0; 255 128 0; 255 0 0; 213 0 0; 176 0 0]/255;
G5_12=[0 0 255; 0 0 220; 0 0 176; 0 176 0; 0 216 0; 0 255 0; 255 255 0; 255 192 0; 255 128 0; 255 0 0; 211 0 0; 176 0 0]/255;   % 12 bandas (capturas d_z de la GUI de GEO5, 2026-09-03)
if nb==11; CMAP=G5_11; elseif nb==12; CMAP=G5_12; elseif nb==13; CMAP=G5_13;
else; x13=linspace(0,1,13); xq=linspace(0,1,nb); CMAP=[interp1(x13,G5_13(:,1),xq)' interp1(x13,G5_13(:,2),xq)' interp1(x13,G5_13(:,3),xq)']; end
figure('Position',[80 80 990 594]); set(gcf,'PaperPositionMode','auto');   % 9.0 x 5.4 in a 110 dpi = matplotlib
% bandas NO uniformes (la primera y la ultima son mas cortas): se pinta el INDICE de banda para que
% cada banda tenga exactamente su color de la barra, como en GEO5
Zb=nan(size(Zc));
for b=1:nb; Zb(Zc>=lv(b) & Zc<=lv(b+1))=b; end
contourf(XX,ZZ,Zb,0.5:1:nb+0.5,'LineStyle','none'); hold on;
contour(XX,ZZ,Zc,lv,'LineColor',[0.2 0.2 0.2],'LineWidth',0.4);
colormap(CMAP); caxis([0.5 nb+0.5]);
labs=cell(1,numel(lv)); for k=1:numel(lv); labs{k}=sprintf('%.1f',lv(k)); end
cb=colorbar('Direction','reverse'); set(cb,'Ticks',0.5:1:nb+0.5,'TickLabels',labs); ylabel(cb,[lab ' [mm]']);
plot(xm,ym,'-','Color',[0.45 0.45 0.45],'LineWidth',0.3);   % malla T6
plot(xi,yi,'--k','LineWidth',1.6);                          % interfaz suelo 1 / suelo 2
plot(xb,yb,'-k','LineWidth',1.3);                           % contorno del talud
% etiquetas de los suelos en el centroide de cada region
for mm=1:2
  cxs=[]; cys=[];
  for e=1:ne; if EMAT(e)==mm; cxs=[cxs mean(X(ELE(e,1:3)))]; cys=[cys mean(Y(ELE(e,1:3)))]; end; end
  yoff=0; if mm==2; yoff=-2.5; end
  text(mean(cxs),mean(cys)+yoff,sprintf('SUELO %d\n\\phi=%.1f%s  c=%.0f kPa\n\\gamma=%.0f kN/m^3',mm,MAT(mm,3),char(176),MAT(mm,4),MAT(mm,5)),...
       'HorizontalAlignment','center','VerticalAlignment','middle','FontSize',8,'BackgroundColor','w','EdgeColor','k');
end
% cargas de la etapa
if si>=2      % sobrecarga: flechas hacia abajo en los nodos cargados de la corona
  idx=find(Fs(2:2:end)<-1e-9);
  quiver(X(idx),Y(idx)+1.6,zeros(size(idx)),-1.6*ones(size(idx)),0,'k','LineWidth',0.9,'MaxHeadSize',0.6);
  text(mean(X(idx)),Y(idx(1))+2.0,'q = 35 kPa','HorizontalAlignment','center','VerticalAlignment','bottom','FontSize',8);
end
if si>=3      % ancla: fuerza puntual en su nodo, flecha en la direccion de la fuerza
  [~,ia]=max(abs(Fa(1:2:end))+abs(Fa(2:2:end)));
  fx=sum(Fa(1:2:end)); fy=sum(Fa(2:2:end)); L=4.0/sqrt(fx^2+fy^2);
  quiver(X(ia),Y(ia),fx*L,fy*L,0,'k','LineWidth',1.6,'MaxHeadSize',0.8);
  text(X(ia)+fx*L+0.3,Y(ia)+fy*L-0.3,'ancla 72 kN','HorizontalAlignment','left','VerticalAlignment','top','FontSize',8);
end
axis equal; xlim([xmin-1 xmax+1]); ylim([zmin-1 zmax+3]); box on;
xlabel('x [m]'); ylabel('z [m]');
title(sprintf('Talud Demo04 %s - %s [mm]  FS=%.4f  (GEO5 %.2f)',name,lab,fs,gfs));
hold off;
try; print('-dpng','-r110',fn); fprintf('PNG: %s\n',fn); catch err; fprintf('print fallo: %s\n',err.message); end
end

function st=geo5_step(vmin,vmax)
% paso de banda de GEO5, MEDIDO en 6 barras de su GUI (d_x y d_z, 3 etapas, 3-sep): paso=(max-min)/11 con la
% MANTISA redondeada al 0.5 mas cercano: 18.0/11->1.5, 110.7/11->10, 5.4/11->0.5, 5.9/11=0.536->0.55
rng=vmax-vmin; if rng<=0; st=1; return; end
raw=rng/11; p=10^floor(log10(raw)); m=raw/p; st=round(m*2)/2*p;
end
function lv=geo5_levels(vmin,vmax)
% bordes de banda de GEO5: [min, multiplos del paso dentro de (min,max), max] (min y max a 0.1 mm); un multiplo a
% menos de 0.1 paso del min se omite (etapa 2 d_x: -0.4, 10, 20...), uno a menos de 0.05 paso del max se funde con el
st=geo5_step(vmin,vmax); vmax=round(vmax*10)/10; vmin=round(vmin*10)/10;
lv=vmin; k=floor(vmin/st)+1;
while k*st<vmax-1e-9
  v=k*st; if v-vmin>=0.1*st; lv(end+1)=v; end
  k=k+1;
end
if vmax>lv(end)+0.05*st; lv(end+1)=vmax; else; lv(end)=vmax; end
end
