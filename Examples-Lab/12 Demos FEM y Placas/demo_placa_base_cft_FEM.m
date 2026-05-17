% =========================================================================
% Placa base CFT — FEM KIRCHHOFF PURO (sin PDE Toolbox, MATLAB R2017a)
% -------------------------------------------------------------------------
% Elemento  : Q4-BFS (4 nodos rectangulares, 16 GDL/elem)
% GDL/nodo  : w, theta_x = dw/dx, theta_y = dw/dy, psi = d2w/(dx dy)
% Shape fns : Hermitianas cubicas (producto tensorial)
% Cuadratura: Gauss-Legendre 4x4
% BC        : apoyos puntuales en los 8 pernos perimetrales (w = 0 con penalty)
% Carga     : presion uniforme q_HSS = P/A_HSS sobre la huella del HSS
% =========================================================================
close all; clc;
mkdir('demo_fem_figs');

t_total = tic;

%% ---------- Datos ----------
a    = 0.5;       % lado placa X [m]
b    = 0.5;       % lado placa Y [m]
t_pl = 0.030;     % espesor [m]
E    = 200e6;     % E acero [kN/m^2]  (200 GPa)
nu   = 0.3;

P_axial = 1500;       % [kN] carga axial CFT
b_col   = 0.30;       % HSS lado [m]
A_HSS   = b_col*b_col;
q_HSS   = P_axial / A_HSS;   % [kN/m^2]
fprintf('Carga distribuida bajo HSS: q = %.0f kN/m2\n', q_HSS);

edge = 0.05;          % distancia perno -> borde

%% ---------- Mesh ----------
n_a = 10;             % elementos en X (a_1 = 50 mm)
n_b = 10;             % elementos en Y
n_e = n_a*n_b;
n_j = (n_a+1)*(n_b+1);
a_1 = a/n_a;
b_1 = b/n_b;
n_dof = 4;
n_k   = n_dof*4;      % 16

% Nodos (column-major: y inner, x outer) — igual que Rectangular_Slab_FEA.m
x_j = zeros(n_j, 1);
y_j = zeros(n_j, 1);
x = 0; y = 0;
for j = 1:n_j
    x_j(j) = x;
    y_j(j) = y;
    y = y + b_1;
    if y > b + 1e-9
        y = 0;
        x = x + a_1;
    end
end

% Conectividad Q4 (CCW desde nodo inferior-izq)
e_j = zeros(n_e, 4);
for ia = 1:n_a
    for ib = 1:n_b
        e = ib + n_b*(ia-1);
        j_corner = e + ia - 1;
        e_j(e, 1) = j_corner;
        e_j(e, 2) = j_corner + n_b + 1;
        e_j(e, 3) = j_corner + n_b + 2;
        e_j(e, 4) = j_corner + 1;
    end
end

%% ---------- Pernos perimetrales (8) ----------
xp = [edge, a/2, a-edge, edge, a-edge, edge, a/2, a-edge];
yp = [edge, edge, edge, b/2, b/2, b-edge, b-edge, b-edge];
nPernos = numel(xp);

% Nodo mas cercano para cada perno (snap a la malla)
nodo_perno = zeros(nPernos, 1);
for k = 1:nPernos
    d2 = (x_j - xp(k)).^2 + (y_j - yp(k)).^2;
    [~, idx] = min(d2);
    nodo_perno(k) = idx;
end
fprintf('Pernos -> nodos: %s\n', mat2str(nodo_perno'));

%% ---------- Matriz constitutiva D (Kirchhoff bending) ----------
D = E*t_pl^3/(12*(1-nu^2)) * [1, nu, 0; nu, 1, 0; 0, 0, (1-nu)/2];

%% ---------- Gauss-Legendre 4x4 en [0,1] ----------
gp = [0.0694318442029737, 0.3300094782075719, 0.6699905217924281, 0.9305681557970263];
gw = [0.1739274225687269, 0.3260725774312731, 0.3260725774312731, 0.1739274225687269];

%% ---------- K_e (16x16) ----------
K_e = zeros(n_k, n_k);
for ig = 1:4
    for jg = 1:4
        xi = gp(ig); eta = gp(jg); wgt = gw(ig)*gw(jg);
        % --- Hermite en xi (longitud a_1) ---
        P1a = 1 - xi^2*(3-2*xi);   P2a = xi*a_1*(1-xi*(2-xi));
        P3a = xi^2*(3-2*xi);       P4a = xi^2*a_1*(xi-1);
        Pdd1a = -(6/a_1^2)*(1-2*xi);  Pdd2a = -(2/a_1)*(2-3*xi);
        Pdd3a =  (6/a_1^2)*(1-2*xi);  Pdd4a = -(2/a_1)*(1-3*xi);
        Pd1a = -6*(xi/a_1)*(1-xi);    Pd2a = 1 - xi*(4-3*xi);
        Pd3a =  6*(xi/a_1)*(1-xi);    Pd4a = -xi*(2-3*xi);
        % --- Hermite en eta (longitud b_1) ---
        P1b = 1 - eta^2*(3-2*eta);  P2b = eta*b_1*(1-eta*(2-eta));
        P3b = eta^2*(3-2*eta);      P4b = eta^2*b_1*(eta-1);
        Pdd1b = -(6/b_1^2)*(1-2*eta); Pdd2b = -(2/b_1)*(2-3*eta);
        Pdd3b =  (6/b_1^2)*(1-2*eta); Pdd4b = -(2/b_1)*(1-3*eta);
        Pd1b = -6*(eta/b_1)*(1-eta);  Pd2b = 1 - eta*(4-3*eta);
        Pd3b =  6*(eta/b_1)*(1-eta);  Pd4b = -eta*(2-3*eta);

        % B (3 x 16): [kappa_x; kappa_y; 2*kappa_xy]
        B1 = [Pdd1a*P1b, Pdd2a*P1b, Pdd1a*P2b, Pdd2a*P2b, ...
              Pdd3a*P1b, Pdd4a*P1b, Pdd3a*P2b, Pdd4a*P2b, ...
              Pdd3a*P3b, Pdd4a*P3b, Pdd3a*P4b, Pdd4a*P4b, ...
              Pdd1a*P3b, Pdd2a*P3b, Pdd1a*P4b, Pdd2a*P4b];
        B2 = [P1a*Pdd1b, P2a*Pdd1b, P1a*Pdd2b, P2a*Pdd2b, ...
              P3a*Pdd1b, P4a*Pdd1b, P3a*Pdd2b, P4a*Pdd2b, ...
              P3a*Pdd3b, P4a*Pdd3b, P3a*Pdd4b, P4a*Pdd4b, ...
              P1a*Pdd3b, P2a*Pdd3b, P1a*Pdd4b, P2a*Pdd4b];
        B3 = 2*[Pd1a*Pd1b, Pd2a*Pd1b, Pd1a*Pd2b, Pd2a*Pd2b, ...
                Pd3a*Pd1b, Pd4a*Pd1b, Pd3a*Pd2b, Pd4a*Pd2b, ...
                Pd3a*Pd3b, Pd4a*Pd3b, Pd3a*Pd4b, Pd4a*Pd4b, ...
                Pd1a*Pd3b, Pd2a*Pd3b, Pd1a*Pd4b, Pd2a*Pd4b];
        B = [B1; B2; B3];
        K_e = K_e + (B')*D*B * a_1*b_1*wgt;
    end
end

%% ---------- Vector elemental F_e (uniforme dentro del elem.) ----------
% Lo recalculamos solo para elementos cuyo centro cae bajo el HSS.
F_e_unit = zeros(n_k, 1);
for ig = 1:4
    for jg = 1:4
        xi = gp(ig); eta = gp(jg); wgt = gw(ig)*gw(jg);
        P1a = 1 - xi^2*(3-2*xi);   P2a = xi*a_1*(1-xi*(2-xi));
        P3a = xi^2*(3-2*xi);       P4a = xi^2*a_1*(xi-1);
        P1b = 1 - eta^2*(3-2*eta); P2b = eta*b_1*(1-eta*(2-eta));
        P3b = eta^2*(3-2*eta);     P4b = eta^2*b_1*(eta-1);
        N = [P1a*P1b, P2a*P1b, P1a*P2b, P2a*P2b, ...
             P3a*P1b, P4a*P1b, P3a*P2b, P4a*P2b, ...
             P3a*P3b, P4a*P3b, P3a*P4b, P4a*P4b, ...
             P1a*P3b, P2a*P3b, P1a*P4b, P2a*P4b];
        F_e_unit = F_e_unit + (N')*a_1*b_1*wgt;
    end
end

% Mascara: elementos bajo HSS (huella 300x300 centrada)
HSS_x0 = (a - b_col)/2;  HSS_y0 = (b - b_col)/2;
HSS_x1 = HSS_x0 + b_col; HSS_y1 = HSS_y0 + b_col;
elem_bajo_HSS = false(n_e, 1);
for e = 1:n_e
    xc_e = mean(x_j(e_j(e,:)));
    yc_e = mean(y_j(e_j(e,:)));
    elem_bajo_HSS(e) = (xc_e > HSS_x0) && (xc_e < HSS_x1) && ...
                       (yc_e > HSS_y0) && (yc_e < HSS_y1);
end
fprintf('Elementos bajo huella HSS: %d / %d\n', sum(elem_bajo_HSS), n_e);

%% ---------- Ensamblaje global ----------
n_g = n_dof*n_j;
K = zeros(n_g, n_g);
F = zeros(n_g, 1);
for e = 1:n_e
    q_e = q_HSS * double(elem_bajo_HSS(e));   % presion solo bajo HSS
    F_e = q_e * F_e_unit;
    gdl_e = zeros(n_k, 1);
    for i = 1:4
        gi = e_j(e, i);
        for ii = 1:n_dof
            gdl_e(n_dof*(i-1)+ii) = n_dof*(gi-1) + ii;
        end
    end
    K(gdl_e, gdl_e) = K(gdl_e, gdl_e) + K_e;
    F(gdl_e)        = F(gdl_e) + F_e;
end

%% ---------- BC: apoyos puntuales en pernos (w = 0) ----------
k_pen = 1e20;
for k = 1:nPernos
    g = n_dof*(nodo_perno(k)-1) + 1;   % DOF w del nodo del perno
    K(g, g) = K(g, g) + k_pen;
end

%% ---------- Solve ----------
fprintf('Resolviendo K (%dx%d) ...\n', n_g, n_g);
Z = K \ F;
fprintf('Listo. Tiempo total hasta aqui: %.2f s\n', toc(t_total));

% Deflexion en cada nodo
w_nodo = Z(1:n_dof:end);   % vector de w en cada nodo (mm cuando *1000)
W_mat = zeros(n_b+1, n_a+1);
X_grid = zeros(n_b+1, n_a+1);
Y_grid = zeros(n_b+1, n_a+1);
for i = 1:n_a+1
    for j = 1:n_b+1
        node = (i-1)*(n_b+1) + j;
        X_grid(j, i) = x_j(node);
        Y_grid(j, i) = y_j(node);
        W_mat(j, i)  = w_nodo(node) * 1000;   % mm
    end
end

fprintf('w_max (centro placa) = %.4f mm\n', W_mat(round((n_b+1)/2), round((n_a+1)/2)));
fprintf('w_min (mas negativa) = %.4f mm\n', min(min(W_mat)));
fprintf('w_max (mas positiva) = %.4f mm\n', max(max(W_mat)));

%% =========================================================================
%  VISUALIZACION DEL MALLADO (3 formas distintas)
%% =========================================================================
% Colormap SAP2000 (azul -> verde -> amarillo -> rojo)
sap2000_cmap = [0,0,1; 0,0.5,1; 0,1,1; 0,1,0.5; 0,1,0; ...
                0.5,1,0; 1,1,0; 1,0.5,0; 1,0,0];

%% --- FIG 1: triplot (Q4 splitado en 2 T3) ---
T = zeros(2*n_e, 3);
for e = 1:n_e
    T(2*e-1, :) = [e_j(e,1), e_j(e,2), e_j(e,3)];
    T(2*e,   :) = [e_j(e,1), e_j(e,3), e_j(e,4)];
end
figure('Position',[80 80 800 750]); hold on;
triplot(T, x_j*1000, y_j*1000, 'Color',[.2 .4 .7], 'LineWidth',0.8);
% Marcar pernos
for k = 1:nPernos
    plot(xp(k)*1000, yp(k)*1000, 'ro', 'MarkerSize',12, ...
         'MarkerFaceColor','r', 'LineWidth',1.2);
    text(xp(k)*1000, yp(k)*1000-18, sprintf('B%d', k), ...
         'HorizontalAlignment','center', 'FontSize',9, ...
         'FontWeight','bold', 'Color','r');
end
% Sombra HSS
patch([HSS_x0 HSS_x1 HSS_x1 HSS_x0]*1000, ...
      [HSS_y0 HSS_y0 HSS_y1 HSS_y1]*1000, ...
      [.95 .9 .5], 'FaceAlpha',0.35, 'EdgeColor',[.6 .5 0], 'LineWidth',1.5);
text(a/2*1000, b/2*1000, sprintf('HSS\n%d\\times%d', b_col*1000, b_col*1000), ...
     'HorizontalAlignment','center', 'FontSize',11, 'FontWeight','bold');
title(sprintf('Mallado (triplot): %d Q4 -> %d T3, %d nodos, %d GDL', ...
              n_e, size(T,1), n_j, n_g), 'FontSize',12);
xlabel('x [mm]'); ylabel('y [mm]'); axis equal tight; grid on; box on;
saveas(gcf, 'demo_fem_figs/01_mesh_triplot.png');

%% --- FIG 2: Q4 reales con patch (los cuadrilateros) ---
figure('Position',[80 80 800 750]); hold on;
for e = 1:n_e
    nd = e_j(e, :);
    patch(x_j(nd)*1000, y_j(nd)*1000, 'w', ...
          'FaceColor','none', 'EdgeColor',[.1 .3 .6], 'LineWidth',1.0);
end
% Centro de cada elemento + numeracion (solo si malla pequeña)
if n_e <= 200
    for e = 1:n_e
        xc_e = mean(x_j(e_j(e,:)))*1000;
        yc_e = mean(y_j(e_j(e,:)))*1000;
        if elem_bajo_HSS(e)
            text(xc_e, yc_e, sprintf('%d', e), 'HorizontalAlignment','center', ...
                 'FontSize',7, 'Color',[.5 .3 0]);
        else
            text(xc_e, yc_e, sprintf('%d', e), 'HorizontalAlignment','center', ...
                 'FontSize',7, 'Color',[.5 .5 .5]);
        end
    end
end
% Huella HSS y pernos
patch([HSS_x0 HSS_x1 HSS_x1 HSS_x0]*1000, ...
      [HSS_y0 HSS_y0 HSS_y1 HSS_y1]*1000, ...
      [.95 .9 .5], 'FaceAlpha',0.25, 'EdgeColor',[.6 .5 0], 'LineWidth',2);
for k = 1:nPernos
    plot(xp(k)*1000, yp(k)*1000, 'ro', 'MarkerSize',12, ...
         'MarkerFaceColor','r', 'LineWidth',1.2);
end
title(sprintf('Mallado Q4 real (BFS conformante): %d elementos rectangulares', n_e), ...
      'FontSize',12);
xlabel('x [mm]'); ylabel('y [mm]'); axis equal tight; grid on; box on;
saveas(gcf, 'demo_fem_figs/02_mesh_Q4.png');

%% --- FIG 3: Surf 3D de la deflexion ---
figure('Position',[80 80 950 700]);
colormap(sap2000_cmap);
surf(X_grid*1000, Y_grid*1000, W_mat, 'EdgeColor',[.3 .3 .3], 'LineWidth',0.4);
title('Deflexion w(x,y) [mm] — Kirchhoff Q4-BFS', 'FontSize',12);
xlabel('x [mm]'); ylabel('y [mm]'); zlabel('w [mm]');
colorbar; view(35, 30); grid on;
saveas(gcf, 'demo_fem_figs/03_deflexion_surf.png');

%% --- FIG 4: Contour de la deflexion ---
figure('Position',[80 80 850 700]); hold on;
colormap(sap2000_cmap);
contourf(X_grid*1000, Y_grid*1000, W_mat, 20, 'LineColor','none');
cb = colorbar; cb.Label.String = 'w [mm]';
% Pernos
for k = 1:nPernos
    plot(xp(k)*1000, yp(k)*1000, 'ko', 'MarkerSize',10, ...
         'MarkerFaceColor','k');
end
% Huella HSS
plot([HSS_x0 HSS_x1 HSS_x1 HSS_x0 HSS_x0]*1000, ...
     [HSS_y0 HSS_y0 HSS_y1 HSS_y1 HSS_y0]*1000, ...
     'k--', 'LineWidth',1.5);
title('Contour deflexion w [mm]', 'FontSize',12);
xlabel('x [mm]'); ylabel('y [mm]'); axis equal tight; grid on; box on;
saveas(gcf, 'demo_fem_figs/04_deflexion_contour.png');

%% --- Momentos en nodos (promediados desde centros de elemento) ---
M_x_n = zeros(n_j, 1);
M_y_n = zeros(n_j, 1);
M_xy_n = zeros(n_j, 1);
cnt = zeros(n_j, 1);
for e = 1:n_e
    Z_e = zeros(16, 1);
    for i = 1:4
        gi = e_j(e, i);
        for ii = 1:4
            Z_e(4*(i-1)+ii) = Z(4*(gi-1)+ii);
        end
    end
    xi = 0.5; eta = 0.5;
    P1a = 1 - xi^2*(3-2*xi);  P2a = xi*a_1*(1-xi*(2-xi));
    P3a = xi^2*(3-2*xi);      P4a = xi^2*a_1*(xi-1);
    Pdd1a = -(6/a_1^2)*(1-2*xi); Pdd2a = -(2/a_1)*(2-3*xi);
    Pdd3a =  (6/a_1^2)*(1-2*xi); Pdd4a = -(2/a_1)*(1-3*xi);
    Pd1a = -6*(xi/a_1)*(1-xi);   Pd2a = 1 - xi*(4-3*xi);
    Pd3a =  6*(xi/a_1)*(1-xi);   Pd4a = -xi*(2-3*xi);
    P1b = 1 - eta^2*(3-2*eta); P2b = eta*b_1*(1-eta*(2-eta));
    P3b = eta^2*(3-2*eta);     P4b = eta^2*b_1*(eta-1);
    Pdd1b = -(6/b_1^2)*(1-2*eta); Pdd2b = -(2/b_1)*(2-3*eta);
    Pdd3b =  (6/b_1^2)*(1-2*eta); Pdd4b = -(2/b_1)*(1-3*eta);
    Pd1b = -6*(eta/b_1)*(1-eta); Pd2b = 1 - eta*(4-3*eta);
    Pd3b =  6*(eta/b_1)*(1-eta); Pd4b = -eta*(2-3*eta);
    B1 = [Pdd1a*P1b, Pdd2a*P1b, Pdd1a*P2b, Pdd2a*P2b, Pdd3a*P1b, Pdd4a*P1b, Pdd3a*P2b, Pdd4a*P2b, Pdd3a*P3b, Pdd4a*P3b, Pdd3a*P4b, Pdd4a*P4b, Pdd1a*P3b, Pdd2a*P3b, Pdd1a*P4b, Pdd2a*P4b];
    B2 = [P1a*Pdd1b, P2a*Pdd1b, P1a*Pdd2b, P2a*Pdd2b, P3a*Pdd1b, P4a*Pdd1b, P3a*Pdd2b, P4a*Pdd2b, P3a*Pdd3b, P4a*Pdd3b, P3a*Pdd4b, P4a*Pdd4b, P1a*Pdd3b, P2a*Pdd3b, P1a*Pdd4b, P2a*Pdd4b];
    B3 = 2*[Pd1a*Pd1b, Pd2a*Pd1b, Pd1a*Pd2b, Pd2a*Pd2b, Pd3a*Pd1b, Pd4a*Pd1b, Pd3a*Pd2b, Pd4a*Pd2b, Pd3a*Pd3b, Pd4a*Pd3b, Pd3a*Pd4b, Pd4a*Pd4b, Pd1a*Pd3b, Pd2a*Pd3b, Pd1a*Pd4b, Pd2a*Pd4b];
    B_c = [B1; B2; B3];
    M_e = -D*B_c*Z_e;
    for i = 1:4
        gi = e_j(e, i);
        M_x_n(gi)  = M_x_n(gi)  + M_e(1);
        M_y_n(gi)  = M_y_n(gi)  + M_e(2);
        M_xy_n(gi) = M_xy_n(gi) + M_e(3);
        cnt(gi) = cnt(gi) + 1;
    end
end
for j = 1:n_j
    if cnt(j) > 0
        M_x_n(j)  = M_x_n(j)  / cnt(j);
        M_y_n(j)  = M_y_n(j)  / cnt(j);
        M_xy_n(j) = M_xy_n(j) / cnt(j);
    end
end
Mx_mat  = zeros(n_b+1, n_a+1);
My_mat  = zeros(n_b+1, n_a+1);
Mxy_mat = zeros(n_b+1, n_a+1);
for i = 1:n_a+1
    for j = 1:n_b+1
        node = (i-1)*(n_b+1) + j;
        Mx_mat(j,i)  = M_x_n(node);
        My_mat(j,i)  = M_y_n(node);
        Mxy_mat(j,i) = M_xy_n(node);
    end
end

%% --- FIG 5: Mx contour ---
figure('Position',[80 80 850 700]); hold on;
colormap(sap2000_cmap);
contourf(X_grid*1000, Y_grid*1000, Mx_mat, 20, 'LineColor','none');
cb = colorbar; cb.Label.String = 'M_x [kN m/m]';
for k = 1:nPernos, plot(xp(k)*1000, yp(k)*1000, 'ko', 'MarkerSize',8, 'MarkerFaceColor','k'); end
plot([HSS_x0 HSS_x1 HSS_x1 HSS_x0 HSS_x0]*1000, ...
     [HSS_y0 HSS_y0 HSS_y1 HSS_y1 HSS_y0]*1000, 'k--', 'LineWidth',1.5);
title('Momento flector M_x  [kN m/m]', 'FontSize',12);
xlabel('x [mm]'); ylabel('y [mm]'); axis equal tight; grid on; box on;
saveas(gcf, 'demo_fem_figs/05_Mx.png');

%% --- FIG 6: My contour ---
figure('Position',[80 80 850 700]); hold on;
colormap(sap2000_cmap);
contourf(X_grid*1000, Y_grid*1000, My_mat, 20, 'LineColor','none');
cb = colorbar; cb.Label.String = 'M_y [kN m/m]';
for k = 1:nPernos, plot(xp(k)*1000, yp(k)*1000, 'ko', 'MarkerSize',8, 'MarkerFaceColor','k'); end
plot([HSS_x0 HSS_x1 HSS_x1 HSS_x0 HSS_x0]*1000, ...
     [HSS_y0 HSS_y0 HSS_y1 HSS_y1 HSS_y0]*1000, 'k--', 'LineWidth',1.5);
title('Momento flector M_y  [kN m/m]', 'FontSize',12);
xlabel('x [mm]'); ylabel('y [mm]'); axis equal tight; grid on; box on;
saveas(gcf, 'demo_fem_figs/06_My.png');

%% --- FIG 7: Mxy contour ---
figure('Position',[80 80 850 700]); hold on;
colormap(sap2000_cmap);
contourf(X_grid*1000, Y_grid*1000, Mxy_mat, 20, 'LineColor','none');
cb = colorbar; cb.Label.String = 'M_{xy} [kN m/m]';
for k = 1:nPernos, plot(xp(k)*1000, yp(k)*1000, 'ko', 'MarkerSize',8, 'MarkerFaceColor','k'); end
plot([HSS_x0 HSS_x1 HSS_x1 HSS_x0 HSS_x0]*1000, ...
     [HSS_y0 HSS_y0 HSS_y1 HSS_y1 HSS_y0]*1000, 'k--', 'LineWidth',1.5);
title('Momento torsor M_{xy}  [kN m/m]', 'FontSize',12);
xlabel('x [mm]'); ylabel('y [mm]'); axis equal tight; grid on; box on;
saveas(gcf, 'demo_fem_figs/07_Mxy.png');

%% ---------- Reporte ----------
fprintf('\n========================================================\n');
fprintf('  RESULTADOS FEM KIRCHHOFF Q4-BFS\n');
fprintf('========================================================\n');
fprintf('  Placa            : %.0f x %.0f x %.1f mm\n', a*1000, b*1000, t_pl*1000);
fprintf('  Material         : E = %.0f GPa, nu = %.2f\n', E/1e6, nu);
fprintf('  Malla            : %d x %d elementos (%d nodos, %d GDL)\n', n_a, n_b, n_j, n_g);
fprintf('  Carga total      : %.0f kN sobre %.0f x %.0f mm\n', P_axial, b_col*1000, b_col*1000);
fprintf('  Pernos           : %d apoyos puntuales (w = 0)\n', nPernos);
fprintf('  -------------------------------------------\n');
fprintf('  w_min (placa)    : %+.4f mm\n', min(min(W_mat)));
fprintf('  w_max (placa)    : %+.4f mm\n', max(max(W_mat)));
fprintf('  |Mx|_max         : %.3f kN m/m\n', max(max(abs(Mx_mat))));
fprintf('  |My|_max         : %.3f kN m/m\n', max(max(abs(My_mat))));
fprintf('  |Mxy|_max        : %.3f kN m/m\n', max(max(abs(Mxy_mat))));
fprintf('  -------------------------------------------\n');
fprintf('  Tiempo total     : %.2f s\n', toc(t_total));
fprintf('========================================================\n\n');

disp('Done.');
