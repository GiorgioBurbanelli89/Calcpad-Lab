%% Finite Element Analysis of Rectangular Slab
% Analisis por elementos finitos de una placa rectangular simplemente apoyada
% bajo carga uniforme, con el elemento rectangular BFS (Bogner-Fox-Schmit) de 16 GDL.
% Motor: Hekatan Lab (MATLAB). Notacion: la que Calcpad VM usa por defecto.

clear; clc;

%% Input data
% Slab dimensions — a = 6 m,  b = 4 m
% Thickness — t = 0.1 m
% Load — q = 10 kN/m²
% Modulus of elasticity — E = 35000 MPa
% Poisson`s ratio — ν = 0.15
a = 6     % Dimension en x [m]
b = 4     % Dimension en y [m]
t = 0.1   % Espesor [m]
q = 10    % Carga distribuida [kN/m^2]
E = 35000 % Modulo elastico [MPa] = [kN/mm^2*10^3]
nu = 0.15 % Coef de Poisson

% Conversion a unidades consistentes (kN, m): E en kN/m^2 = MPa * 1000
E_si = E*1000 % [kN/m^2]

%% Finite element mesh
% Usamos el elemento finito rectangular con n = 16 grados de libertad.
% Number of elements along a and b — n_a = 6,  n_b = 4
% Total number of elements — n_e = n_a·n_b
% Total number of joints — n_j = (n_a + 1)·(n_b + 1)
% Element dimensions — a₁ = a/n_a,  b₁ = b/n_b
% Supported joints count — n_s = 2·(n_a + n_b)
n_a = 6   % elementos en a
n_b = 4   % elementos en b
n_e = n_a*n_b           % total elementos
n_j = (n_a+1)*(n_b+1)   % total joints
a_1 = a/n_a             % ancho elemento
b_1 = b/n_b             % alto elemento
n_dof = 4   % DOFs por joint: w, theta_x, theta_y, psi (twist)
n_ke = 16   % DOFs por elemento (4 nodos x 4 DOFs)
n_g = n_dof*n_j  % DOFs totales globales

%-- Coordenadas de los joints
x_j = zeros(n_j, 1);
y_j = zeros(n_j, 1);
xv = 0; yv = 0;
for j = 1:n_j
    x_j(j) = xv;
    y_j(j) = yv;
    yv = yv + b_1;
    if yv > b + 1e-9
        yv = 0;
        xv = xv + a_1;
    end
end

%-- Connectivity: e_j(e, k) = joint k del elemento e (k=1..4)
e_j = zeros(n_e, 4);
for i_a = 1:n_a
    for i_b = 1:n_b
        e = i_b + n_b*(i_a - 1);
        j = e + i_a - 1;
        e_j(e, 1) = j;
        e_j(e, 2) = j + n_b + 1;
        e_j(e, 3) = j + n_b + 2;
        e_j(e, 4) = j + 1;
    end
end

%-- Joints apoyados (bordes de la placa)
n_s = 2*(n_a + n_b);
s_j = zeros(n_s, 1);
i_s = 0;
for i = 1:n_a + 1
    i_s = i_s + 1;
    s_j(i_s) = (n_b + 1)*i - n_b;
end
for i = 1:n_a + 1
    i_s = i_s + 1;
    s_j(i_s) = (n_b + 1)*i;
end
for i = 2:n_b
    i_s = i_s + 1;
    s_j(i_s) = i;
end
for i = 2:n_b
    i_s = i_s + 1;
    s_j(i_s) = n_a*(n_b + 1) + i;
end

fprintf('Mesh: %d elem (%d x %d), %d joints, %d apoyos\n', n_e, n_a, n_b, n_j, n_s);

%% Constitutive matrix (stress - strain relationship)
% Matriz constitutiva de flexion de placa, la misma que Calcpad VM:
% #noc D = E*t^3/(12*(1 - nu^2))*[1; nu; 0|nu; 1; 0|0; 0; (1 - nu)/2]
D11 = E_si*t^3/(12*(1 - nu^2));
D = D11 * [1, nu, 0; nu, 1, 0; 0, 0, (1 - nu)/2];

%% Finite element formulation — Shape functions (DEDUCCION SIMBOLICA)
%' ### Deduccion simbolica de las funciones de forma de Hermite
%'
%' No las escribo: las DEDUZCO. Cada funcion de forma es la unica cubica que vale
%' 1 en UN grado de libertad de extremo (desplazamiento w o giro theta) y 0 en los
%' otros tres. Se parte de una cubica generica y se imponen las cuatro condiciones.
sym_s = sym('s');
syms As Bs Cs Ds real
w_gen  = As + Bs*sym_s + Cs*sym_s^2 + Ds*sym_s^3;   % cubica generica en s = x/L
dw_gen = diff(w_gen, sym_s);
%' Cada fila de BC = [w(0) w'(0) w(1) w'(1)] activa un grado de libertad:
BC = [1 0 0 0; 0 1 0 0; 0 0 1 0; 0 0 0 1];
Nsym = sym(zeros(1, 4));
for k = 1 : 4
    sol_k = solve([subs(w_gen, sym_s, 0) == BC(k, 1), subs(dw_gen, sym_s, 0) == BC(k, 2), ...
                   subs(w_gen, sym_s, 1) == BC(k, 3), subs(dw_gen, sym_s, 1) == BC(k, 4)], [As Bs Cs Ds]);
    Nsym(k) = simplify(subs(w_gen, {As, Bs, Cs, Ds}, {sol_k.As, sol_k.Bs, sol_k.Cs, sol_k.Ds}));
end
disp('Funciones de forma deducidas  N = [N_1  N_2  N_3  N_4]:')
disp(Nsym)
%' Sus curvaturas (segundas derivadas) son lo que entra a la rigidez de flexion:
ddNsym = diff(Nsym, sym_s, 2);
disp('Curvaturas  N_i'''' :')
disp(ddNsym)
%' La matriz de rigidez de flexion 1D SALE de integrar el producto de curvaturas:
% #noc K_{1D} = $Integral{N_i'' N_j'' @ s = 0 : 1}
K1D_sym = int(ddNsym.' * ddNsym, sym_s, 0, 1);
disp('K_1D = integral de N_i'''' N_j'''' en [0,1]  (matriz de rigidez de viga Euler-Bernoulli):')
disp(K1D_sym)
%' Es exactamente la matriz de rigidez de viga clasica. El elemento de placa BFS
%' es el PRODUCTO TENSORIAL de estas funciones de Hermite en x e y (16 gdl).
%'
%' A continuacion se usan las mismas funciones en forma numerica para ensamblar K.
%-- Las 4 funciones de Hermite cubicas por dimension
syms xi eta
syms aa bb  % parametros a_1 y b_1 simbolicos para diff (sustituidos despues)

Phi1 = 1 - xi^2*(3 - 2*xi);     % phi_1: w en 0
Phi2 = xi*aa*(1 - xi*(2 - xi)); % phi_2: theta en 0 (escalado por longitud aa)
Phi3 = xi^2*(3 - 2*xi);         % phi_3: w en 1
Phi4 = xi^2*aa*(-1 + xi);       % phi_4: theta en 1

%-- Derivadas (con respecto a la coord local 0..1; jacobiano 1/aa para fisica)
dPhi1 = diff(Phi1, xi);   ddPhi1 = diff(Phi1, xi, 2);
dPhi2 = diff(Phi2, xi);   ddPhi2 = diff(Phi2, xi, 2);
dPhi3 = diff(Phi3, xi);   ddPhi3 = diff(Phi3, xi, 2);
dPhi4 = diff(Phi4, xi);   ddPhi4 = diff(Phi4, xi, 2);

%% Cuadratura Gauss 4x4 en [0, 1] (mapeo desde [-1, 1])
%-- Puntos y pesos estandar Gauss-Legendre n=4 en [-1, 1]:
gp4 = [-0.861136311594053; -0.339981043584856;  0.339981043584856;  0.861136311594053];
gw4 = [ 0.347854845137454;  0.652145154862546;  0.652145154862546;  0.347854845137454];
%-- Mapeo a [0, 1]: u = (xi+1)/2, du = dxi/2
gp = (gp4 + 1)/2; gw = gw4/2;
n_gp = 4;

%% Calculo de K_e por elemento (numerico, Gauss 4x4 x 4x4)
fprintf('Computando K_e (16x16) con Gauss 4x4...\n');

%-- Funcion auxiliar: evaluar segundas derivadas de Phi_k en xi=u con aa=L
function v = phi_dd(k, u, L)
    if k == 1
        v = -6/L^2 + 12*u/L^2;
    elseif k == 2
        v = (-4 + 6*u)/L;
    elseif k == 3
        v = 6/L^2 - 12*u/L^2;
    elseif k == 4
        v = (-2 + 6*u)/L;
    else
        v = 0;
    end
end

%-- Funcion auxiliar: evaluar Phi_k en xi=u con aa=L
function v = phi(k, u, L)
    if k == 1
        v = 1 - u^2*(3 - 2*u);
    elseif k == 2
        v = u*L*(1 - u*(2 - u));
    elseif k == 3
        v = u^2*(3 - 2*u);
    elseif k == 4
        v = u^2*L*(-1 + u);
    else
        v = 0;
    end
end

%-- Funcion auxiliar: evaluar primera derivada de Phi_k en xi=u con aa=L
function v = phi_d(k, u, L)
    if k == 1
        v = -6*u/L + 6*u^2/L;
    elseif k == 2
        v = 1 - 4*u + 3*u^2;
    elseif k == 3
        v = 6*u/L - 6*u^2/L;
    elseif k == 4
        v = -2*u + 3*u^2;
    else
        v = 0;
    end
end

%-- Indice DOF en el elemento: dof_idx(node, dof_type) → 1..16
%-- node = 1..4, dof_type = 1 (w), 2 (theta_x), 3 (theta_y), 4 (psi)
function idx = dof_idx(node, dof_type)
    idx = 4*(node - 1) + dof_type;
end

%-- Mapeo: para DOF j (1..16), devuelve (node_k, x_index, y_index, type)
%-- Para BFS estandar: el orden de las 16 funciones por nodo es:
%--   w = Phi1(xi) * Phi1(eta)    (DOF 1)
%--   theta_x = Phi1(xi) * Phi2(eta)  (DOF 2)  -- por dw/dy
%--   theta_y = Phi2(xi) * Phi1(eta)  (DOF 3)  -- por dw/dx
%--   psi = Phi2(xi) * Phi2(eta)      (DOF 4)  -- por d^2w/dxdy
%-- Nodo 1=(0,0), 2=(1,0), 3=(1,1), 4=(0,1)
function [ix, iy, tx, ty] = bfs_indices(j)
    node = floor((j-1)/4) + 1;
    sub = mod(j-1, 4) + 1;
    %-- ix, iy: indice de Phi (1=corner low, 3=corner hi, 2/4=theta)
    %-- Map: por nodo determinar par (ix_w, iy_w) base
    if node == 1
        ixw = 1; iyw = 1;
    elseif node == 2
        ixw = 3; iyw = 1;
    elseif node == 3
        ixw = 3; iyw = 3;
    else
        ixw = 1; iyw = 3;
    end
    %-- Sub-DOF type: 1=w (ixw, iyw), 2=tx (ixw, iyw+1), 3=ty (ixw+1, iyw), 4=psi (ixw+1, iyw+1)
    if sub == 1
        ix = ixw; iy = iyw;
    elseif sub == 2
        ix = ixw; iy = iyw + 1;
    elseif sub == 3
        ix = ixw + 1; iy = iyw;
    else
        ix = ixw + 1; iy = iyw + 1;
    end
    tx = 0; ty = 0; % unused, but MATLAB needs all outputs
end

%% Strain-displacement matrix B  y  Element stiffness matrix
% Matriz deformacion-desplazamiento (curvaturas), 3 filas x 16 columnas.
% Fila 1 = Φ″a(ξ)·Φb(η)   (κ_x),  Fila 2 = Φa(ξ)·Φ″b(η)   (κ_y),
% Fila 3 = 2·Φ′a(ξ)·Φ′b(η)  (κ_xy):
%
%   B(j; ξ; η) = [ B_1(j; ξ; η) ; B_2(j; ξ; η) ; B_3(j; ξ; η) ]
%
% La matriz de rigidez del elemento se obtiene con la MISMA ecuacion de Calcpad VM,
% integrando en el cuadrado unitario (aqui por cuadratura de Gauss 4x4):
% #noc K_e = a_1*b_1*$Area{$Area{B_i^T*D*B_j @ xi = 0 : 1} @ eta = 0 : 1}
% Y el vector de carga consistente del elemento:
% #noc F_e = q*a_1*b_1*$Area{$Area{N_i @ xi = 0 : 1} @ eta = 0 : 1}
%-- B matrix at (u, v): row 1 = -d^2N/dx^2, row 2 = -d^2N/dy^2, row 3 = -2*d^2N/dxdy
%-- Cada columna j (1..16) es la contribucion de la shape function j
B_e = zeros(3, 16);

K_e = zeros(16, 16);
F_e = zeros(16, 1);

for ig = 1:n_gp
    for jg = 1:n_gp
        u = gp(ig); v = gp(jg);
        wgt = gw(ig) * gw(jg);
        %-- Construir B en (u, v)
        for j = 1:16
            [ix, iy, dummy1, dummy2] = bfs_indices(j);
            B_e(1, j) = -phi_dd(ix, u, a_1) * phi(iy, v, b_1);
            B_e(2, j) = -phi(ix, u, a_1) * phi_dd(iy, v, b_1);
            B_e(3, j) = -2 * phi_d(ix, u, a_1) * phi_d(iy, v, b_1) / (a_1*b_1) * a_1*b_1;
        end
        %-- Contribucion: K_e += B^T * D * B * a_1 * b_1 * wgt
        K_e = K_e + B_e.' * D * B_e * a_1 * b_1 * wgt;
        %-- F_e (consistent): contribuye solo a DOFs w (1, 5, 9, 13)
        for j = 1:16
            [ix, iy, dummy1, dummy2] = bfs_indices(j);
            F_e(j) = F_e(j) + q * phi(ix, u, a_1) * phi(iy, v, b_1) * a_1 * b_1 * wgt;
        end
    end
end

fprintf('K_e calculado. Diagonal:\n');
diag_Ke = diag(K_e);
fprintf('  K_e(1,1) = %g  (DOF w nodo 1)\n', K_e(1,1));
fprintf('  K_e(2,2) = %g  (DOF tx nodo 1)\n', K_e(2,2));
fprintf('  K_e(3,3) = %g  (DOF ty nodo 1)\n', K_e(3,3));
fprintf('  K_e(4,4) = %g  (DOF psi nodo 1)\n', K_e(4,4));

%% Ensamblaje de la K global
fprintf('Ensamblando K global (%d x %d)...\n', n_g, n_g);
K = zeros(n_g, n_g);
F = zeros(n_g, 1);

for e = 1:n_e
    for ni = 1:4
        ji = e_j(e, ni);
        for nj = 1:4
            jj = e_j(e, nj);
            for di = 1:4
                gi = 4*(ji-1) + di;
                ei = 4*(ni-1) + di;
                for dj = 1:4
                    gj = 4*(jj-1) + dj;
                    ej = 4*(nj-1) + dj;
                    K(gi, gj) = K(gi, gj) + K_e(ei, ej);
                end
            end
        end
        for di = 1:4
            gi = 4*(ji-1) + di;
            ei = 4*(ni-1) + di;
            F(gi) = F(gi) + F_e(ei);
        end
    end
end

%% Aplicar condiciones de contorno (simply supported)
%-- En cada joint apoyado: penalizar la K en los DOFs apropiados.
%-- Convencion .m: DOF g=w, g+1=theta_x=dw/dy, g+2=theta_y=dw/dx, g+3=psi=d2w/dxdy.
%-- Borde y=0 o y=b (paralelo a x): w=0 (auto) Y dw/dx=0 a lo largo del borde
%--   (porque w=0 a lo largo del borde implica derivada tangencial nula).
%--   dw/dx = theta_y = DOF g+2.
%-- Borde x=0 o x=a (paralelo a y): w=0 Y dw/dy=0 (derivada tangencial = 0).
%--   dw/dy = theta_x = DOF g+1.
k_s = 1e20;
for i = 1:n_s
    js = s_j(i);
    g = 4*(js - 1) + 1;
    K(g, g) = K(g, g) + k_s;  % w = 0 en apoyos
    if abs(y_j(js)) < 1e-9 || abs(y_j(js) - b) < 1e-9
        % Borde paralelo a x: dw/dx = theta_y = 0
        K(g+2, g+2) = K(g+2, g+2) + k_s;
    end
    if abs(x_j(js)) < 1e-9 || abs(x_j(js) - a) < 1e-9
        % Borde paralelo a y: dw/dy = theta_x = 0
        K(g+1, g+1) = K(g+1, g+1) + k_s;
    end
end

%% Solution
% Ensamblada la rigidez global K y el vector de carga global F, e impuestas las
% condiciones de contorno (simply supported en el contorno), se resuelve el sistema:
%
%   Z = K⁻¹ · F        (K·Z = F)
%
% Z contiene los desplazamientos de todos los GDL (w, θₓ, θᵧ, ψ por joint), en m.
fprintf('Resolviendo sistema (%d ecs)...\n', n_g);
Z = K \ F;

%% Results — Joint displacements (deflexion central)
% El dato canonico del benchmark es la deflexion vertical en el centro de la placa,
% w(a/2, b/2), que se reporta en mm (como en Calcpad VM).
%-- El joint central esta en (a/2, b/2). Para mesh 6x4, los joints estan
%-- en intervalos a/6 x b/4. El centro NO necesariamente coincide con un joint.
%-- Para mesh par: centro = joint en columna (n_a/2 + 1), fila (n_b/2 + 1)
center_col = n_a/2 + 1;
center_row = n_b/2 + 1;
center_joint = (center_col - 1)*(n_b + 1) + center_row;
fprintf('Joint central: %d en (%.2f, %.2f) m\n', center_joint, x_j(center_joint), y_j(center_joint));

w_center = Z(4*(center_joint - 1) + 1);
%-- Conversion: Z esta en m porque K esta en kN/m, F en kN. w_center en m.
fprintf('Deflexion central w(a/2, b/2) = %g m = %g mm\n', w_center, w_center*1000);

%% Campos nodales para graficar (deflexion + momentos)
W_z = zeros(n_a+1, n_b+1);
for i = 1:n_a+1
    for k = 1:n_b+1
        jj = (i-1)*(n_b+1)+k;
        W_z(i,k) = Z(4*(jj-1)+1);
    end
end
%-- Recuperacion de momentos M = -D*B*Z_e, promediados en nodos
Mnode = zeros(3, n_j); cnt = zeros(n_j,1);
locuv = [0 0; 1 0; 1 1; 0 1];
for e = 1:n_e
    Ze = zeros(16,1);
    for ni = 1:4
        jn = e_j(e,ni);
        for di = 1:4, Ze(4*(ni-1)+di) = Z(4*(jn-1)+di); end
    end
    for ni = 1:4
        uu = locuv(ni,1); vv = locuv(ni,2);
        Bm = zeros(3,16);
        for j = 1:16
            [ix, iy, d1, d2] = bfs_indices(j);
            Bm(1,j) = -phi_dd(ix,uu,a_1)*phi(iy,vv,b_1);
            Bm(2,j) = -phi(ix,uu,a_1)*phi_dd(iy,vv,b_1);
            Bm(3,j) = -2*phi_d(ix,uu,a_1)*phi_d(iy,vv,b_1);
        end
        Mv = -D*Bm*Ze; jn = e_j(e,ni);
        Mnode(:,jn) = Mnode(:,jn) + Mv; cnt(jn) = cnt(jn)+1;
    end
end
for j = 1:n_j, Mnode(:,j) = Mnode(:,j)/cnt(j); end
Mxx = zeros(n_a+1, n_b+1); Myy = zeros(n_a+1, n_b+1); Mxy = zeros(n_a+1, n_b+1);
for i = 1:n_a+1
    for k = 1:n_b+1
        jj = (i-1)*(n_b+1)+k;
        Mxx(i,k) = Mnode(1,jj); Myy(i,k) = Mnode(2,jj); Mxy(i,k) = Mnode(3,jj);
    end
end
%-- Von Mises (esfuerzo en la fibra extrema: sigma = 6*M/t^2), en MPa
sx = 6*Mxx/t^2; sy = 6*Myy/t^2; sxy = 6*Mxy/t^2;    % [kN/m^2]
Mvm = sqrt(sx.^2 - sx.*sy + sy.^2 + 3*sxy.^2)/1000; % [MPa]

%% Malla Q4 DISCRETIZADA: numeracion de elementos/nodos + apoyos (leyenda por color)
figure; hold on;
cxe = zeros(n_e,1); cye = zeros(n_e,1);
for e = 1:n_e
    nd = e_j(e,:);
    xs = [x_j(nd(1)), x_j(nd(2)), x_j(nd(3)), x_j(nd(4))];
    ys = [y_j(nd(1)), y_j(nd(2)), y_j(nd(3)), y_j(nd(4))];
    patch(xs, ys, [0.93 0.96 1.0], 'EdgeColor', [0 0.25 0.55], 'LineWidth', 1.2);
    cxe(e) = mean(xs); cye(e) = mean(ys);
end
%-- Numero de ELEMENTO (azul, centrado en el centroide)
text(cxe, cye, (1:n_e)', 'Color', [0 0.35 0.75], 'HorizontalAlignment', 'center', 'FontSize', 11);
%-- Nodos: apoyo (rojo) vs libre (azul) => leyenda por color
is_sup = zeros(n_j, 1);
for i = 1:n_s, is_sup(s_j(i)) = 1; end
h_free = plot(x_j(is_sup==0), y_j(is_sup==0), 'o', 'MarkerFaceColor', [0.20 0.45 0.90], 'MarkerEdgeColor', 'k', 'MarkerSize', 7, 'DisplayName', 'Nodo libre');
h_sup  = plot(x_j(is_sup==1), y_j(is_sup==1), 's', 'MarkerFaceColor', [0.90 0.15 0.15], 'MarkerEdgeColor', 'k', 'MarkerSize', 9, 'DisplayName', 'Apoyo (borde)');
%-- Numero de NODO (negro, con leve desplazamiento para no tapar el marcador)
text(x_j + 0.06, y_j + 0.06, (1:n_j)', 'Color', [0.15 0.15 0.15], 'FontSize', 8);
%-- Carga q => fuerzas NODALES equivalentes (F_e = int N^T q dA). En EF la carga
%   entra en los NODOS (vector F), no en el area: anillo hueco = nodo cargado.
h_q = plot(x_j, y_j, 'o', 'MarkerFaceColor', 'none', 'MarkerEdgeColor', [0.15 0.55 0.20], 'MarkerSize', 16, 'LineWidth', 1.3, 'LineStyle', 'none', 'DisplayName', sprintf('Carga nodal equiv. (q=%g kN/m^2)', q));
axis equal; xlim([-0.35 7.7]); ylim([-0.35 4.35]);
legend('show');   % usa los DisplayName => nodos libres/apoyos + carga (celdas patch sin nombre no entran)
title('Malla Q4: numeracion de nodos/elementos + apoyos'); xlabel('x [m]'); ylabel('y [m]');

%% Contornos con interpolacion SPLINE (malla fina) + colormap
xg = 0:a_1:a; yg = 0:b_1:b; [Xg,Yg] = meshgrid(xg, yg);
xf = linspace(0,a,80); yf = linspace(0,b,80); [Xf,Yf] = meshgrid(xf, yf);
Wf   = interp2(Xg, Yg, -W_z'*1000, Xf, Yf, 'spline');
Mxxf = interp2(Xg, Yg, Mxx',       Xf, Yf, 'spline');
Myyf = interp2(Xg, Yg, Myy',       Xf, Yf, 'spline');
Mxyf = interp2(Xg, Yg, Mxy',       Xf, Yf, 'spline');
Mvmf = interp2(Xg, Yg, Mvm',       Xf, Yf, 'spline');

figure; contourf(Xf, Yf, Wf, 20, 'LineStyle', 'none'); colorbar; colormap(flipud(jet));
title('Deflexion w [mm] - interp2 spline'); xlabel('x [m]'); ylabel('y [m]');

figure; contourf(Xf, Yf, Mxxf, 20, 'LineStyle', 'none'); colorbar; colormap(flipud(jet));
title('Momento Mxx [kNm/m] - interp2 spline'); xlabel('x [m]'); ylabel('y [m]');

figure; contourf(Xf, Yf, Myyf, 20, 'LineStyle', 'none'); colorbar; colormap(flipud(jet));
title('Momento Myy [kNm/m] - interp2 spline'); xlabel('x [m]'); ylabel('y [m]');

figure; contourf(Xf, Yf, Mxyf, 20, 'LineStyle', 'none'); colorbar; colormap(flipud(jet));
title('Momento Mxy [kNm/m] - interp2 spline'); xlabel('x [m]'); ylabel('y [m]');

figure; contourf(Xf, Yf, Mvmf, 20, 'LineStyle', 'none'); colorbar; colormap(flipud(jet));
title('Von Mises sigma [MPa] - interp2 spline'); xlabel('x [m]'); ylabel('y [m]');

fprintf('\n=== FIN benchmark BFS slab FEA ===\n');
