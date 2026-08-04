%% Finite Element Analysis of Rectangular Slab
%-- Replica del "Rectangular Slab FEA.cpd" de Calcpad, en MATLAB (Calcpad Lab).
%-- Placa Kirchhoff, elemento rectangular BFS de 16 GDL (w, theta_x, theta_y, psi).
%-- Mismo codigo corre en MATLAB 2017a y Calcpad Lab; las directivas Calcpad van
%-- escondidas en comentarios (% #noc / % #val) -> MATLAB las ignora, Lab typografia.

%% Input data
a  = 6;      % Slab dimension x [m]
b  = 4;      % Slab dimension y [m]
t  = 0.1;    % Thickness [m]
q  = 10;     % Load [kN/m^2]
E  = 35000;  % Modulus of elasticity [MPa]
nu = 0.15;   % Poisson's ratio
E_si = E*1000;   % [kN/m^2]

%% Finite element mesh
n_a = 6;                 % elements along a
n_b = 4;                 % elements along b
n_e = n_a*n_b;           % total elements
n_j = (n_a+1)*(n_b+1);   % total joints
a_1 = a/n_a;             % element size x [m]
b_1 = b/n_b;             % element size y [m]
n_s = 2*(n_a+n_b);       % supported joints
n_dof = 4;               % DOFs per joint
n_g = n_dof*n_j;         % total DOFs
fprintf('Mesh: %d elements (%dx%d), %d joints, %d supports\n', n_e, n_a, n_b, n_j, n_s);

%-- Joint coordinates
x_j = zeros(n_j,1); y_j = zeros(n_j,1);
xv = 0; yv = 0;
for j = 1:n_j
    x_j(j) = xv; y_j(j) = yv;
    yv = yv + b_1;
    if yv > b + 1e-9
        yv = 0; xv = xv + a_1;
    end
end

%-- Element connectivity: e_j(e,k) = joint k of element e
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

%-- Supported joints (all 4 edges)
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

%% Constitutive matrix (stress-strain)
%-- OJO: el contenido de #noc es sintaxis CALCPAD (matriz: ; = columna, | = fila),
%-- no MATLAB. La matriz MATLAB de abajo usa , y ; (distinto).
% #noc D = E*t^3/(12*(1 - nu^2))*[1; nu; 0|nu; 1; 0|0; 0; (1 - nu)/2]
D = E_si*t^3/(12*(1-nu^2)) * [1, nu, 0; nu, 1, 0; 0, 0, (1-nu)/2];

%% Shape functions (Hermite cubics, BFS)
% #noc Phi_1(x) = 1 - x^2*(3 - 2*x)
% #noc Phi_2(x) = x*l*(1 - x*(2 - x))
% #noc Phi_3(x) = x^2*(3 - 2*x)
% #noc Phi_4(x) = x^2*l*(-1 + x)

%% Element stiffness matrix
% Se calcula por doble integracion de Gauss (formula simbolica):
% #noc K_e = a_1*b_1*$Area{$Area{B_i^T*D*B_j @ xi = 0 : 1} @ eta = 0 : 1}
% Element load vector:
% #noc F_e = a_1*b_1*$Area{$Area{N_i^T*q @ xi = 0 : 1} @ eta = 0 : 1}

%-- Gauss-Legendre 4x4 mapped to [0,1]
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

%% Global assembly
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

%% Supports (simply supported: w=0 + tangential rotation=0)
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

%% Solution
Z = K \ F;

%% Results - joint displacements
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
fprintf('Central deflection w(a/2,b/2) = %.4f mm\n', w_center*1000);

%% Bending moments recovery (M = -D*B*Z_e, averaged at joints)
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
Mx = zeros(n_a+1, n_b+1); My = zeros(n_a+1, n_b+1); Mxy = zeros(n_a+1, n_b+1);
for i = 1:n_a+1
    for k = 1:n_b+1
        j = (i-1)*(n_b+1)+k;
        Mx(i,k) = Mnode(1,j); My(i,k) = Mnode(2,j); Mxy(i,k) = Mnode(3,j);
    end
end
fprintf('M_x(a/2; b/2) = %.4f kNm/m\n', Mx(center_col,center_row));
fprintf('M_y(a/2; b/2) = %.4f kNm/m\n', My(center_col,center_row));
fprintf('M_xy(0; 0)    = %.4f kNm/m\n', Mxy(1,1));

%% Contour maps
xg = 0:a_1:a; yg = 0:b_1:b;
[Xg, Yg] = meshgrid(xg, yg);

figure; contourf(Xg, Yg, -W_z'*1000, 20); colorbar;
title('Deflection w [mm]'); xlabel('x [m]'); ylabel('y [m]');

figure; contourf(Xg, Yg, Mx', 20); colorbar;
title('Bending moment M_x [kNm/m]'); xlabel('x [m]'); ylabel('y [m]');

figure; contourf(Xg, Yg, My', 20); colorbar;
title('Bending moment M_y [kNm/m]'); xlabel('x [m]'); ylabel('y [m]');

figure; contourf(Xg, Yg, Mxy', 20); colorbar;
title('Twisting moment M_{xy} [kNm/m]'); xlabel('x [m]'); ylabel('y [m]');

fprintf('=== FIN ===\n');

%% ---- Helper functions ----
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
    % B = curvaturas [d2w/dx2; d2w/dy2; 2*d2w/dxdy] (convencion .cpd: Phi'' > 0).
    % El signo no afecta K = B'*D*B; SI fija el signo de M = -D*B*Z.
    Bm = zeros(3,16);
    for j = 1:16
        [ix, iy] = bfs_ix(j);
        Bm(1,j) = phi_dd(ix,u,a1)*phi(iy,v,b1);
        Bm(2,j) = phi(ix,u,a1)*phi_dd(iy,v,b1);
        Bm(3,j) = 2*phi_d(ix,u,a1)*phi_d(iy,v,b1);
    end
end
