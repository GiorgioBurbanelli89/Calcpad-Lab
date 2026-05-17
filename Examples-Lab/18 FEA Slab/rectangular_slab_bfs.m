%% Rectangular Slab FEA — BFS (Bogner-Fox-Schmit, 16 DOF/elem)
%-- Benchmark de placa rectangular simply-supported, carga uniforme q.
%-- Equivalente al Rectangular Slab FEA.cpd de Calcpad.
%-- Compara: deflexion central w(a/2, b/2) — el dato canonico.

clear; clc;

%% Input data
a = 6     % Dimension en x [m]
b = 4     % Dimension en y [m]
t = 0.1   % Espesor [m]
q = 10    % Carga distribuida [kN/m^2]
E = 35000 % Modulo elastico [MPa] = [kN/mm^2*10^3]
nu = 0.15 % Coef de Poisson

% Conversion a unidades consistentes (kN, m): E en kN/m^2 = MPa * 1000
E_si = E*1000 % [kN/m^2]

%% Mesh: n_a x n_b elementos
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

%% Matriz constitutiva D (bending plate)
D11 = E_si*t^3/(12*(1 - nu^2));
D = D11 * [1, nu, 0; nu, 1, 0; 0, 0, (1 - nu)/2];

%% Shape functions Hermite cubicas (Phi_k(xi)) en [0, 1]
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
k_s = 1e20;
for i = 1:n_s
    js = s_j(i);
    g = 4*(js - 1) + 1;
    K(g, g) = K(g, g) + k_s;  % w = 0 en apoyos
    % Bordes paralelos a x (y=0 o y=b): adicionalmente theta_x = 0
    if abs(y_j(js)) < 1e-9 || abs(y_j(js) - b) < 1e-9
        K(g+1, g+1) = K(g+1, g+1) + k_s;
    end
    % Bordes paralelos a y (x=0 o x=a): adicionalmente theta_y = 0
    if abs(x_j(js)) < 1e-9 || abs(x_j(js) - a) < 1e-9
        K(g+2, g+2) = K(g+2, g+2) + k_s;
    end
end

%% Solucion
fprintf('Resolviendo sistema (%d ecs)...\n', n_g);
Z = K \ F;

%% Resultados — deflexion central
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

fprintf('\n=== FIN benchmark BFS slab FEA ===\n');
