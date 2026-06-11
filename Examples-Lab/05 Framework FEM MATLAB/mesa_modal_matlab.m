%% MESA A TORSION - MODAL (vibracion) - objetivo ETABS: f1=f2=2.912, f3=3.478 Hz
% Reusa helpers kbend_mzc/kmembrane_q4/kframe_local/tframe. K densa + masa lumped + eig(K,M).
Lx=6; Ly=6; H=4; t_slab=0.10; bC=0.40; hC=0.40; bV=0.30; hV=0.50;
N=5; nPS=N+1; dx=Lx/N; dy=Ly/N;
E=24.85e6; nu=0.20;                      % kPa
gamma=24.0277; g=9.80665;                % peso especifico kN/m3 (ETABS 2.40277 tonf/m3)
rho=gamma/g;                             % densidad de masa (kN s2/m)/m3
rb=min(bV,hV)/max(bV,hV); Jv=(1/3-0.21*rb*(1-rb^4/12))*max(bV,hV)*min(bV,hV)^3; Jc=0.141*bC^4;

% --- nodos ---
nodes=[0,0,0; Lx,0,0; Lx,Ly,0; 0,Ly,0];
for j=0:N, for i=0:N, nodes=[nodes; i*dx,j*dy,H]; end, end
n_nodes=size(nodes,1); ndof=6*n_nodes;

% --- conectividad ---
nsh=N*N; shell=zeros(nsh,4); e=0;
for j=0:N-1, for i=0:N-1, e=e+1; n1=4+1+j*nPS+i; shell(e,:)=[n1,n1+1,n1+1+nPS,n1+nPS]; end, end
fr=[1,5,1; 2,5+N,1; 3,5+N+N*nPS,1; 4,5+N*nPS,1];
for i=0:N-1, n1=4+1+i;        fr=[fr; n1,n1+1,2]; end
for j=0:N-1, n1=4+1+j*nPS+N;  fr=[fr; n1,n1+nPS,2]; end
for i=0:N-1, n1=4+1+N*nPS+i;  fr=[fr; n1,n1+1,2]; end
for j=0:N-1, n1=4+1+j*nPS;    fr=[fr; n1,n1+nPS,2]; end
nfr=size(fr,1);

% --- K densa ---
K=zeros(ndof,ndof);
for s=1:nsh
  elm=shell(s,:); x_e=nodes(elm,1); y_e=nodes(elm,2);
  Kb=kbend_mzc(x_e,y_e,E,nu,t_slab); Km=kmembrane_q4(x_e,y_e,E,nu,t_slab); kd=1e-3*max(abs(diag(Kb)));
  K24=zeros(24,24);
  for a=1:4, for b=1:4
    K24(6*(a-1)+1,6*(b-1)+1)=Km(2*(a-1)+1,2*(b-1)+1); K24(6*(a-1)+1,6*(b-1)+2)=Km(2*(a-1)+1,2*(b-1)+2);
    K24(6*(a-1)+2,6*(b-1)+1)=Km(2*(a-1)+2,2*(b-1)+1); K24(6*(a-1)+2,6*(b-1)+2)=Km(2*(a-1)+2,2*(b-1)+2);
    for di=1:3, for dj=1:3, K24(6*(a-1)+2+di,6*(b-1)+2+dj)=Kb(3*(a-1)+di,3*(b-1)+dj); end, end
  end, end
  for a=1:4, K24(6*a,6*a)=K24(6*a,6*a)+kd; end
  gg=zeros(1,24); for k=1:4, n=elm(k); gg(6*k-5:6*k)=(6*n-5):(6*n); end
  K(gg,gg)=K(gg,gg)+K24;
end
for f=1:nfr
  ni=fr(f,1); nj=fr(f,2);
  if fr(f,3)==1, p=struct('E',E,'nu',nu,'A',bC*hC,'Iy',bC*hC^3/12,'Iz',hC*bC^3/12,'J',Jc);
  else, p=struct('E',E,'nu',nu,'A',bV*hV,'Iy',bV*hV^3/12,'Iz',hV*bV^3/12,'J',Jv); end
  n0=nodes(ni,:); n1=nodes(nj,:); Tt=tframe(n0,n1); Kg=Tt'*kframe_local(n0,n1,p)*Tt;
  gg=[(6*ni-5):(6*ni),(6*nj-5):(6*nj)]; K(gg,gg)=K(gg,gg)+Kg;
end

% --- Masa lumped (traslacional) ---
M=zeros(ndof,ndof);
% slab: rho*t*A por elemento, /4 a cada nodo (ux,uy,uz)
for s=1:nsh
  elm=shell(s,:); me=rho*t_slab*dx*dy/4;
  for k=1:4, n=elm(k); M(6*n-5,6*n-5)=M(6*n-5,6*n-5)+me; M(6*n-4,6*n-4)=M(6*n-4,6*n-4)+me; M(6*n-3,6*n-3)=M(6*n-3,6*n-3)+me; end
end
% frames estilo ETABS: masa de VIGA lumpeada en las 2 ESQUINAS sobre la LUZ LIBRE
% (luz - ancho columna); columna: mitad al nudo superior (esquina de losa). CSI §7.13.
c1=4+1+0; c2=4+1+N; c3=4+1+N*nPS+N; c4=4+1+N*nPS;     % 4 esquinas de la losa
corners=[c1 c2 c3 c4];
sides={[c1 c2],[c2 c3],[c3 c4],[c4 c1]};
m_beam_side=rho*(bV*hV)*(Lx-bC);                        % masa de viga por lado (luz libre)
for sdx=1:4
  for n=sides{sdx}
    add=m_beam_side/2;
    M(6*n-5,6*n-5)=M(6*n-5,6*n-5)+add; M(6*n-4,6*n-4)=M(6*n-4,6*n-4)+add; M(6*n-3,6*n-3)=M(6*n-3,6*n-3)+add;
  end
end
m_col=rho*(bC*hC)*H/2;                                  % mitad de columna al nudo superior
for n=corners
  M(6*n-5,6*n-5)=M(6*n-5,6*n-5)+m_col; M(6*n-4,6*n-4)=M(6*n-4,6*n-4)+m_col; M(6*n-3,6*n-3)=M(6*n-3,6*n-3)+m_col;
end
% masa rotacional minima para evitar M singular
mt=max(diag(M)); for d=1:ndof, if M(d,d)==0, M(d,d)=1e-6*mt; end, end

% --- BCs: apoyos ARTICULADOS (ETABS: UX UY UZ en nodos 1..4) ---
fixed=[1 2 3, 7 8 9, 13 14 15, 19 20 21];
free=setdiff(1:ndof,fixed);

% --- eig generalizado (guardando eigenvectores) ---
Kf=K(free,free); Mf=M(free,free);
[Vall,Dall]=eig(Kf,Mf);
w2all=real(diag(Dall));
[w2s,ord]=sort(w2all); Vs=Vall(:,ord);
% quedarse con los modos reales (excluir los espurios de masa rotacional minima ~1e8)
keep=find(w2s>1e-3 & w2s<1e8);
w2=w2s(keep); Vm=Vs(:,keep);
f_hz=sqrt(w2)/(2*pi);

% --- centro de masa (para influencia rotacional) ---
md=diag(Mf); xc=0; yc=0; zc=0; mt=0;
for q=1:numel(free)
  d=free(q); nodo=ceil(d/6); comp=mod(d-1,6)+1;
  if comp<=3
    mt=mt+md(q); xc=xc+md(q)*nodes(nodo,1); yc=yc+md(q)*nodes(nodo,2); zc=zc+md(q)*nodes(nodo,3);
  end
end
xc=xc/mt; yc=yc/mt; zc=zc/mt;

% --- vectores de influencia r_d (free x 6): UX UY UZ RX RY RZ ---
nf=numel(free); R=zeros(nf,6);
for q=1:nf
  d=free(q); nodo=ceil(d/6); comp=mod(d-1,6)+1;
  X=nodes(nodo,1)-xc; Y=nodes(nodo,2)-yc; Z=nodes(nodo,3)-zc;
  if comp==1, R(q,1)=1; R(q,5)=Z;  R(q,6)=-Y; end   % ux: UX, RY(+Z), RZ(-Y)
  if comp==2, R(q,2)=1; R(q,4)=-Z; R(q,6)=X;  end   % uy: UY, RX(-Z), RZ(+X)
  if comp==3, R(q,3)=1; R(q,4)=Y;  R(q,5)=-X; end   % uz: UZ, RX(+Y), RY(-X)
end
Mtot=zeros(1,6); for d=1:6, Mtot(d)=R(:,d)'*Mf*R(:,d); end

% --- participacion de masa por modo ---
nmod=min(6,numel(f_hz));
fprintf('=== MESA MODAL - frecuencias + PARTICIPACION DE MASA (%%) ===\n');
fprintf(' Modo    f[Hz]    T[s]     UX      UY      UZ      RX      RY      RZ\n');
for k=1:nmod
  phi=Vm(:,k); Mn=phi'*Mf*phi;
  part=zeros(1,6);
  for d=1:6
    L=phi'*Mf*R(:,d); part(d)=(L^2/Mn)/Mtot(d)*100;
  end
  fprintf(' %3d  %8.4f %7.4f  %6.1f  %6.1f  %6.1f  %6.1f  %6.1f  %6.1f\n', ...
          k, f_hz(k), 1/f_hz(k), part(1),part(2),part(3),part(4),part(5),part(6));
end
fprintf('\n ETABS: f1=f2=2.912 (lat UX/UY), f3=3.478 (torsional RZ)\n');
fprintf(' Mesa : f1=%.3f f3=%.3f | dif f1=%.1f%% f3=%.1f%%\n', ...
        f_hz(1), f_hz(3), 100*(f_hz(1)/2.912-1), 100*(f_hz(3)/3.478-1));

%% GRAFICAS 3D de los modos (mesa deformada: lateral modos 1-2, torsion modo 3)
tipos={'Lateral Y','Lateral X','TORSIONAL'};
cpairs=[1 c1; 2 c2; 3 c3; 4 c4];
fig=figure('visible','off','position',[40 40 1350 460]);
for kk=1:3
  uf=zeros(ndof,1); uf(free)=Vm(:,kk);
  dm=sqrt(uf(1:6:end).^2+uf(2:6:end).^2+uf(3:6:end).^2);
  sc=1.6/max(dm);                          % escala visual
  defn=nodes;
  for n=1:n_nodes, defn(n,:)=nodes(n,:)+sc*[uf(6*n-5),uf(6*n-4),uf(6*n-3)]; end
  subplot(1,3,kk); hold on; axis equal; view(40,22); grid on;
  % columnas: gris=original, azul=deformada
  for c=1:4
    a=cpairs(c,1); b=cpairs(c,2);
    plot3(nodes([a b],1),nodes([a b],2),nodes([a b],3),'-','Color',[.8 .8 .8]);
    plot3(defn([a b],1),defn([a b],2),defn([a b],3),'b-','LineWidth',2);
  end
  % losa: grilla deformada (lineas en x y en y)
  for j=0:N
    idx=zeros(1,N+1); for i=0:N, idx(i+1)=4+1+j*nPS+i; end
    plot3(defn(idx,1),defn(idx,2),defn(idx,3),'-','Color',[.6 .6 .9]);
  end
  for i=0:N
    idx=zeros(1,N+1); for j=0:N, idx(j+1)=4+1+j*nPS+i; end
    plot3(defn(idx,1),defn(idx,2),defn(idx,3),'-','Color',[.6 .6 .9]);
  end
  title(sprintf('Modo %d: %s  f=%.2f Hz', kk, tipos{kk}, f_hz(kk)));
  xlabel('x'); ylabel('y'); zlabel('z');
end
try, saveas(fig,'mesa_modos_3d.png'); fprintf('\nPlot 3D guardado: mesa_modos_3d.png\n'); catch, end
