%% FEA de placa rectangular CON UNIDADES (symunit) + formulas en % #noc
%-- Corre en MATLAB 2017a y Hekatan Lab. Patron: unidades a la ENTRADA (symunit),
%-- se extraen numeros para el solve numerico (rapido; no simbolico), y las unidades
%-- se re-adjuntan a los RESULTADOS. Las formulas van en % #noc (sintaxis Calcpad).
%-- Placa Kirchhoff, elemento rectangular BFS de 16 GDL.
un = symunit;

%% Datos de entrada (con unidades reales)
a_u  = 6*un.m;             % dimension x
b_u  = 4*un.m;             % dimension y
t_u  = 0.1*un.m;           % espesor
q_u  = 10*un.kN/un.m^2;    % carga distribuida
E_u  = 35000*un.MPa;       % modulo de elasticidad
nu   = 0.15;               % Poisson (adimensional)

%% Numeros para el calculo (MATLAB R2017a: separateUnits + double)
val  = @(x) double(separateUnits(x));
a = val(a_u); b = val(b_u); t = val(t_u); q = val(q_u);
E = val(E_u); E_si = E*1000;      % MPa -> kN/m^2

%% Rigidez flexional de la placa
% #noc D_flex = E*t^3/(12*(1 - nu^2))
D_flex = (E_si*t^3/(12*(1-nu^2))) * un.kN*un.m

%% Malla de elementos finitos
n_a = 6; n_b = 4;
n_e = n_a*n_b; n_j = (n_a+1)*(n_b+1);
a_1 = a/n_a; b_1 = b/n_b;
n_s = 2*(n_a+n_b); n_dof = 4; n_g = n_dof*n_j;
fprintf('Malla: %d elementos (%dx%d), %d nodos, %d apoyos\n', n_e, n_a, n_b, n_j, n_s);

%-- Coordenadas de los nodos
x_j = zeros(n_j,1); y_j = zeros(n_j,1);
xv = 0; yv = 0;
for j = 1:n_j
    x_j(j) = xv; y_j(j) = yv;
    yv = yv + b_1;
    if yv > b + 1e-9
        yv = 0; xv = xv + a_1;
    end
end

%-- Conectividad: e_j(e,k) = nodo k del elemento e
e_j = zeros(n_e,4);
for i_a = 1:n_a
    for i_b = 1:n_b
        e = i_b + n_b*(i_a-1);
        j = e + i_a - 1;
        e_j(e,1) = j;
        e_j(e,2) = j + n_b + 1;
        e_j(e,3) = j + n_b + 2;
        e_j(e,4) = j + 1;
    end
end

%-- Nodos apoyados (4 bordes)
s_j = zeros(n_s,1); i_s = 0;
for i = 1:n_a+1
    i_s = i_s+1; s_j(i_s) = (n_b+1)*i - n_b;
end
for i = 1:n_a+1
    i_s = i_s+1; s_j(i_s) = (n_b+1)*i;
end
for i = 2:n_b
    i_s = i_s+1; s_j(i_s) = i;
end
for i = 2:n_b
    i_s = i_s+1; s_j(i_s) = n_a*(n_b+1) + i;
end

%% Matriz constitutiva (contenido de #noc = sintaxis Calcpad: ; = columna, | = fila)
% #noc D = E*t^3/(12*(1 - nu^2))*[1; nu; 0|nu; 1; 0|0; 0; (1 - nu)/2]
D = E_si*t^3/(12*(1-nu^2)) * [1, nu, 0; nu, 1, 0; 0, 0, (1-nu)/2];

%% Funciones de forma (cubicas de Hermite, BFS)
% #noc phi_1(x) = 1 - x^2*(3 - 2*x)
% #noc phi_2(x) = x*l*(1 - x*(2 - x))
% #noc phi_3(x) = x^2*(3 - 2*x)
% #noc phi_4(x) = x^2*l*(-1 + x)

%% Matriz de rigidez del elemento (doble integracion de Gauss)
% #noc K_e = a_1*b_1*$Area{$Area{B_i^T*D*B_j @ xi = 0 : 1} @ eta = 0 : 1}
% Vector de carga del elemento:
% #noc F_e = a_1*b_1*$Area{$Area{N_i^T*q @ xi = 0 : 1} @ eta = 0 : 1}

gp4 = [-0.861136311594053; -0.339981043584856; 0.339981043584856; 0.861136311594053];
gw4 = [ 0.347854845137454;  0.652145154862546; 0.652145154862546; 0.347854845137454];
gp = (gp4+1)/2; gw = gw4/2; n_gp = 4;

K_e = zeros(16,16);
F_e = zeros(16,1);
for ig = 1:n_gp
    for jg = 1:n_gp
        u = gp(ig); v = gp(jg); wgt = gw(ig)*gw(jg);
        B_e = Bmat(u, v, a_1, b_1);
        K_e = K_e + B_e.'*D*B_e*a_1*b_1*wgt;
        for jj = 1:16
            [ix, iy] = bfs_ix(jj);
            F_e(jj) = F_e(jj) + q*phi(ix,u,a_1)*phi(iy,v,b_1)*a_1*b_1*wgt;
        end
    end
end

%% Ensamblaje global
K = zeros(n_g, n_g);
F = zeros(n_g, 1);
for e = 1:n_e
    for ni = 1:4
        ji = e_j(e,ni);
        for nj = 1:4
            jj = e_j(e,nj);
            for di = 1:4
                for dj = 1:4
                    K(4*(ji-1)+di, 4*(jj-1)+dj) = K(4*(ji-1)+di, 4*(jj-1)+dj) + K_e(4*(ni-1)+di, 4*(nj-1)+dj);
                end
            end
        end
        for di = 1:4
            F(4*(ji-1)+di) = F(4*(ji-1)+di) + F_e(4*(ni-1)+di);
        end
    end
end

%% Apoyos (simplemente apoyada: w=0 + giro tangencial=0)
k_s = 1e20;
for i = 1:n_s
    js = s_j(i); g = 4*(js-1)+1;
    K(g,g) = K(g,g) + k_s;
    if abs(y_j(js)) < 1e-9 || abs(y_j(js)-b) < 1e-9
        K(g+2,g+2) = K(g+2,g+2) + k_s;
    end
    if abs(x_j(js)) < 1e-9 || abs(x_j(js)-a) < 1e-9
        K(g+1,g+1) = K(g+1,g+1) + k_s;
    end
end

%% Solucion (numerica)
Z = K \ F;

%% Deflexiones nodales
W_z = zeros(n_a+1, n_b+1);
for i = 1:n_a+1
    for k = 1:n_b+1
        j = (i-1)*(n_b+1)+k;
        W_z(i,k) = Z(4*(j-1)+1);
    end
end
center_col = n_a/2+1; center_row = n_b/2+1;
cj = (center_col-1)*(n_b+1)+center_row;
w_center = Z(4*(cj-1)+1);

%% Momentos flectores (M = -D*B*Z_e, promediados en nodos)
Mnode = zeros(3, n_j); cnt = zeros(n_j,1);
locuv = [0 0; 1 0; 1 1; 0 1];
for e = 1:n_e
    Ze = zeros(16,1);
    for ni = 1:4
        j = e_j(e,ni);
        for di = 1:4
            Ze(4*(ni-1)+di) = Z(4*(j-1)+di);
        end
    end
    for ni = 1:4
        Bm = Bmat(locuv(ni,1), locuv(ni,2), a_1, b_1);
        Mvec = -D*Bm*Ze;
        j = e_j(e,ni);
        Mnode(:,j) = Mnode(:,j) + Mvec;
        cnt(j) = cnt(j) + 1;
    end
end
for j = 1:n_j
    Mnode(:,j) = Mnode(:,j)/cnt(j);
end
%% Campos de momento en toda la malla Q4 (n_a+1 x n_b+1)
Mxx = zeros(n_a+1, n_b+1); Myy = zeros(n_a+1, n_b+1); Mxy = zeros(n_a+1, n_b+1);
for i = 1:n_a+1
    for k = 1:n_b+1
        j = (i-1)*(n_b+1)+k;
        Mxx(i,k) = Mnode(1,j); Myy(i,k) = Mnode(2,j); Mxy(i,k) = Mnode(3,j);
    end
end

%% Graficas de contorno sobre la malla Q4 (colormap jet invertido)
%-- La malla FEA es gruesa (6x4) -> contourf entre pocos nodos da contornos rectos.
%-- Para curvas SUAVES se interpola a una malla fina con interp2 (cubica).
xg = 0:a_1:a; yg = 0:b_1:b;
[Xg, Yg] = meshgrid(xg, yg);
xf = linspace(0, a, 80); yf = linspace(0, b, 80);
[Xf, Yf] = meshgrid(xf, yf);
Wf   = interp2(Xg, Yg, -W_z'*1000, Xf, Yf, 'cubic');
Mxxf = interp2(Xg, Yg, Mxx',       Xf, Yf, 'cubic');
Myyf = interp2(Xg, Yg, Myy',       Xf, Yf, 'cubic');
Mxyf = interp2(Xg, Yg, Mxy',       Xf, Yf, 'cubic');

figure; contourf(Xf, Yf, Wf, 20, 'LineStyle', 'none'); colorbar; colormap(flipud(jet));
title('Deflexion w [mm]'); xlabel('x [m]'); ylabel('y [m]');

figure; contourf(Xf, Yf, Mxxf, 20, 'LineStyle', 'none'); colorbar; colormap(flipud(jet));
title('Momento Mxx [kNm/m]'); xlabel('x [m]'); ylabel('y [m]');

figure; contourf(Xf, Yf, Myyf, 20, 'LineStyle', 'none'); colorbar; colormap(flipud(jet));
title('Momento Myy [kNm/m]'); xlabel('x [m]'); ylabel('y [m]');

figure; contourf(Xf, Yf, Mxyf, 20, 'LineStyle', 'none'); colorbar; colormap(flipud(jet));
title('Momento Mxy [kNm/m]'); xlabel('x [m]'); ylabel('y [m]');

%% RESULTADOS con unidades (symunit) -- unidades re-adjuntadas a la salida
w_max  = (w_center*1000) * un.mm                        % deflexion central
Mx_max = Mxx(center_col,center_row) * un.kN*un.m/un.m   % momento M_xx en el centro
My_max = Myy(center_col,center_row) * un.kN*un.m/un.m   % momento M_yy en el centro
fprintf('=== FIN ===\n');

%% ---- Funciones auxiliares ----
function v = phi(k, u, L)
    if k==1,     v = 1 - u^2*(3 - 2*u);
    elseif k==2, v = u*L*(1 - u*(2 - u));
    elseif k==3, v = u^2*(3 - 2*u);
    elseif k==4, v = u^2*L*(-1 + u);
    else, v = 0; end
end
function v = phi_d(k, u, L)
    if k==1,     v = -6*u/L + 6*u^2/L;
    elseif k==2, v = 1 - 4*u + 3*u^2;
    elseif k==3, v = 6*u/L - 6*u^2/L;
    elseif k==4, v = -2*u + 3*u^2;
    else, v = 0; end
end
function v = phi_dd(k, u, L)
    if k==1,     v = -6/L^2 + 12*u/L^2;
    elseif k==2, v = (-4 + 6*u)/L;
    elseif k==3, v = 6/L^2 - 12*u/L^2;
    elseif k==4, v = (-2 + 6*u)/L;
    else, v = 0; end
end
function [ix, iy] = bfs_ix(j)
    node = floor((j-1)/4) + 1;
    sub  = mod(j-1, 4) + 1;
    if node==1,     ixw=1; iyw=1;
    elseif node==2, ixw=3; iyw=1;
    elseif node==3, ixw=3; iyw=3;
    else,           ixw=1; iyw=3; end
    if sub==1,     ix=ixw;   iy=iyw;
    elseif sub==2, ix=ixw;   iy=iyw+1;
    elseif sub==3, ix=ixw+1; iy=iyw;
    else,          ix=ixw+1; iy=iyw+1; end
end
function Bm = Bmat(u, v, a1, b1)
    Bm = zeros(3,16);
    for j = 1:16
        [ix, iy] = bfs_ix(j);
        Bm(1,j) = phi_dd(ix,u,a1)*phi(iy,v,b1);
        Bm(2,j) = phi(ix,u,a1)*phi_dd(iy,v,b1);
        Bm(3,j) = 2*phi_d(ix,u,a1)*phi_d(iy,v,b1);
    end
end
