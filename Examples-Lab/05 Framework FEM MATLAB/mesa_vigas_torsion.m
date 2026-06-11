% =============================================================================
%  MESA A TORSION - foco: TORSION EN LAS VIGAS PERIMETRALES + mapa Mxy
%  Script self-contained (reusa helpers kbend_mzc/kmembrane_q4/kframe_local/tframe/
%  shell_centroid del mismo dir). Corre en MATLAB R2017a.
% =============================================================================

%% Parametros (igual que mesa_torsion_matlab.m)
Lx = 6.0; Ly = 6.0; H = 4.0;
t_slab = 0.10; bC = 0.40; hC = 0.40; bV = 0.30; hV = 0.50;
N = 5; nPS = N + 1; dx = Lx/N; dy = Ly/N;
E = 24.85e6; nu = 0.20; G = E/(2*(1+nu));
q_load = 9.80665;
sv_J = @(b,h) (1/3 - 0.21*min(b,h)/max(b,h)*(1-(min(b,h)/max(b,h))^4/12))*max(b,h)*min(b,h)^3;
Jc = 0.141*bC^4; Jv = sv_J(bV,hV);

%% Malla
nodes = [0,0,0; Lx,0,0; Lx,Ly,0; 0,Ly,0];
for j=0:N, for i=0:N, nodes=[nodes; i*dx, j*dy, H]; end, end
n_nodes = size(nodes,1);
ix = @(i,j) 4 + 1 + j*nPS + i;

elements = {}; etype = {}; eprop = {};
for j=0:N-1, for i=0:N-1
    elements{end+1}=[ix(i,j),ix(i+1,j),ix(i+1,j+1),ix(i,j+1)];
    etype{end+1}='shell'; eprop{end+1}=struct('E',E,'nu',nu,'t',t_slab);
end, end
shell_count = numel(elements);
colp = struct('E',E,'nu',nu,'A',bC*hC,'Iy',bC*hC^3/12,'Iz',hC*bC^3/12,'J',Jc);
cpairs = [1,ix(0,0); 2,ix(N,0); 3,ix(N,N); 4,ix(0,N)];
for k=1:4, elements{end+1}=cpairs(k,:); etype{end+1}='frame'; eprop{end+1}=colp; end
vp = struct('E',E,'nu',nu,'A',bV*hV,'Iy',bV*hV^3/12,'Iz',hV*bV^3/12,'J',Jv);
beam_nodes = {};   % para ubicar cada viga
for i=0:N-1, elements{end+1}=[ix(i,0),ix(i+1,0)]; etype{end+1}='frame'; eprop{end+1}=vp; end
for j=0:N-1, elements{end+1}=[ix(N,j),ix(N,j+1)]; etype{end+1}='frame'; eprop{end+1}=vp; end
for i=0:N-1, elements{end+1}=[ix(i,N),ix(i+1,N)]; etype{end+1}='frame'; eprop{end+1}=vp; end
for j=0:N-1, elements{end+1}=[ix(0,j),ix(0,j+1)]; etype{end+1}='frame'; eprop{end+1}=vp; end
n_el = numel(elements);

%% Ensamble K
ndof = 6*n_nodes; Il=[]; Jl=[]; Vl=[];
for e=1:n_el
    elm=elements{e}; p=eprop{e};
    if strcmp(etype{e},'shell')
        x_e=nodes(elm,1); y_e=nodes(elm,2);
        Kb=kbend_mzc(x_e,y_e,p.E,p.nu,p.t); Km=kmembrane_q4(x_e,y_e,p.E,p.nu,p.t);
        kd=1e-3*max(abs(diag(Kb)));
        g=zeros(1,24); for k=1:4, n=elm(k); g(6*k-5:6*k)=(6*n-5):(6*n); end
        K24=zeros(24,24);
        for a=1:4, for b=1:4
            K24(6*(a-1)+1,6*(b-1)+1)=Km(2*(a-1)+1,2*(b-1)+1);
            K24(6*(a-1)+1,6*(b-1)+2)=Km(2*(a-1)+1,2*(b-1)+2);
            K24(6*(a-1)+2,6*(b-1)+1)=Km(2*(a-1)+2,2*(b-1)+1);
            K24(6*(a-1)+2,6*(b-1)+2)=Km(2*(a-1)+2,2*(b-1)+2);
            for di=1:3, for dj=1:3
                K24(6*(a-1)+2+di,6*(b-1)+2+dj)=Kb(3*(a-1)+di,3*(b-1)+dj);
            end, end
        end, end
        for a=1:4, K24(6*a,6*a)=K24(6*a,6*a)+kd; end
        [Ii,Jj]=ndgrid(g,g); Il=[Il;Ii(:)]; Jl=[Jl;Jj(:)]; Vl=[Vl;K24(:)];
    else
        n0=nodes(elm(1),:); n1=nodes(elm(2),:);
        Kg=tframe(n0,n1)'*kframe_local(n0,n1,p)*tframe(n0,n1);
        g=[(6*elm(1)-5):(6*elm(1)), (6*elm(2)-5):(6*elm(2))];
        [Ii,Jj]=ndgrid(g,g); Il=[Il;Ii(:)]; Jl=[Jl;Jj(:)]; Vl=[Vl;Kg(:)];
    end
end
K=sparse(Il,Jl,Vl,ndof,ndof);

%% Cargas + BCs + solve
fixed=1:24; free=setdiff(1:ndof,fixed);
F=zeros(ndof,1);
for j=0:N, for i=0:N
    xE=(i==0||i==N); yE=(j==0||j==N); f=q_load*dx*dy;
    if xE&&yE, f=f*0.25; elseif xE||yE, f=f*0.5; end
    n=ix(i,j); F(6*n-3)=F(6*n-3)-f;
end, end
u=zeros(ndof,1); u(free)=K(free,free)\F(free);

%% Mxy en centroides (mapa)
cx=zeros(shell_count,1); cy=cx; Mxy=cx;
for e=1:shell_count
    elm=elements{e}; x_e=nodes(elm,1); y_e=nodes(elm,2);
    [~,~,mxy]=shell_centroid(elm,u,x_e,y_e,E,nu,t_slab);
    cx(e)=mean(x_e); cy(e)=mean(y_e); Mxy(e)=mxy;
end

%% TORSION en las VIGAS perimetrales
beam_ids = (shell_count+5):n_el;       % frames 5..n son las vigas
nb = numel(beam_ids); Tb=zeros(nb,1); bx=zeros(nb,1); by=zeros(nb,1);
for q=1:nb
    e=beam_ids(q); elm=elements{e}; p=eprop{e};
    n0=nodes(elm(1),:); n1=nodes(elm(2),:);
    uG=[u(6*elm(1)-5:6*elm(1)); u(6*elm(2)-5:6*elm(2))];
    fL=kframe_local(n0,n1,p)*(tframe(n0,n1)*uG);
    Tb(q)=fL(4);                        % momento torsor local (T)
    bx(q)=mean([n0(1),n1(1)]); by(q)=mean([n0(2),n1(2)]);
end
fprintf('T torsor en vigas: max |T| = %.3f kN*m\n', max(abs(Tb)));

%% PLOT: mapa Mxy + torsion en vigas
fig=figure('visible','off','position',[80 80 1100 480]);
subplot(1,2,1);
scatter(cx,cy,260,Mxy,'filled','s'); colorbar; axis equal; colormap(jet);
xlabel('x [m]'); ylabel('y [m]'); title('M_{xy} losa [kN m/m]');

subplot(1,2,2);
hold on; axis equal;
% planta de las vigas coloreadas por |T|
for q=1:nb
    e=beam_ids(q); elm=elements{e};
    n0=nodes(elm(1),:); n1=nodes(elm(2),:);
    c=abs(Tb(q));
    plot([n0(1) n1(1)],[n0(2) n1(2)],'-','LineWidth',8, ...
         'Color',torsion_color(c,max(abs(Tb))));
end
scatter(bx,by,40,abs(Tb),'filled'); colorbar; colormap(jet);
xlabel('x [m]'); ylabel('y [m]');
title(sprintf('Torsion T en vigas [kN m]  (max=%.1f)', max(abs(Tb))));
saveas(fig,'mesa_vigas_torsion.png');
fprintf('Plot guardado: mesa_vigas_torsion.png\n');

function c = torsion_color(v, vmax)
    if vmax<=0, t=0; else, t=min(1,v/vmax); end
    c=[t, 0.2, 1-t];   % azul -> rojo
end
