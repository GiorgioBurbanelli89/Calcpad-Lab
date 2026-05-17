% =========================================================================
% Placa base CFT — von Mises CALIDAD FEM (Kirchhoff Q4-BFS)
% Igual al FEM_placa pero con malla finer + 3 figuras von Mises de calidad
% =========================================================================
close all; clc;
mkdir('demo_vm_figs');

t_total = tic;

%% ---------- Datos ----------
a    = 0.5;
b    = 0.5;
t_pl = 0.030;
E    = 200e6;
nu   = 0.3;
P_axial = 1500;
b_col   = 0.30;
A_HSS   = b_col*b_col;
q_HSS   = P_axial / A_HSS;
edge = 0.05;

%% ---------- Mesh (malla FINER 20x20) ----------
n_a = 20;
n_b = 20;
n_e = n_a*n_b;
n_j = (n_a+1)*(n_b+1);
a_1 = a/n_a;
b_1 = b/n_b;
n_dof = 4;
n_k   = n_dof*4;

x_j = zeros(n_j, 1);
y_j = zeros(n_j, 1);
x = 0; y = 0;
for j = 1:n_j
    x_j(j) = x; y_j(j) = y;
    y = y + b_1;
    if y > b + 1e-9, y = 0; x = x + a_1; end
end

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
nodo_perno = zeros(nPernos, 1);
for k = 1:nPernos
    d2 = (x_j - xp(k)).^2 + (y_j - yp(k)).^2;
    [~, idx] = min(d2);
    nodo_perno(k) = idx;
end

%% ---------- D + cuadratura ----------
D = E*t_pl^3/(12*(1-nu^2)) * [1, nu, 0; nu, 1, 0; 0, 0, (1-nu)/2];
gp = [0.0694318442029737, 0.3300094782075719, 0.6699905217924281, 0.9305681557970263];
gw = [0.1739274225687269, 0.3260725774312731, 0.3260725774312731, 0.1739274225687269];

%% ---------- K_e + F_e ----------
K_e = zeros(n_k, n_k);
F_e_unit = zeros(n_k, 1);
for ig = 1:4
    for jg = 1:4
        xi = gp(ig); eta = gp(jg); wgt = gw(ig)*gw(jg);
        P1a = 1 - xi^2*(3-2*xi);   P2a = xi*a_1*(1-xi*(2-xi));
        P3a = xi^2*(3-2*xi);       P4a = xi^2*a_1*(xi-1);
        Pdd1a = -(6/a_1^2)*(1-2*xi);  Pdd2a = -(2/a_1)*(2-3*xi);
        Pdd3a =  (6/a_1^2)*(1-2*xi);  Pdd4a = -(2/a_1)*(1-3*xi);
        Pd1a = -6*(xi/a_1)*(1-xi);    Pd2a = 1 - xi*(4-3*xi);
        Pd3a =  6*(xi/a_1)*(1-xi);    Pd4a = -xi*(2-3*xi);
        P1b = 1 - eta^2*(3-2*eta);  P2b = eta*b_1*(1-eta*(2-eta));
        P3b = eta^2*(3-2*eta);      P4b = eta^2*b_1*(eta-1);
        Pdd1b = -(6/b_1^2)*(1-2*eta); Pdd2b = -(2/b_1)*(2-3*eta);
        Pdd3b =  (6/b_1^2)*(1-2*eta); Pdd4b = -(2/b_1)*(1-3*eta);
        Pd1b = -6*(eta/b_1)*(1-eta);  Pd2b = 1 - eta*(4-3*eta);
        Pd3b =  6*(eta/b_1)*(1-eta);  Pd4b = -eta*(2-3*eta);
        B1 = [Pdd1a*P1b, Pdd2a*P1b, Pdd1a*P2b, Pdd2a*P2b, Pdd3a*P1b, Pdd4a*P1b, Pdd3a*P2b, Pdd4a*P2b, Pdd3a*P3b, Pdd4a*P3b, Pdd3a*P4b, Pdd4a*P4b, Pdd1a*P3b, Pdd2a*P3b, Pdd1a*P4b, Pdd2a*P4b];
        B2 = [P1a*Pdd1b, P2a*Pdd1b, P1a*Pdd2b, P2a*Pdd2b, P3a*Pdd1b, P4a*Pdd1b, P3a*Pdd2b, P4a*Pdd2b, P3a*Pdd3b, P4a*Pdd3b, P3a*Pdd4b, P4a*Pdd4b, P1a*Pdd3b, P2a*Pdd3b, P1a*Pdd4b, P2a*Pdd4b];
        B3 = 2*[Pd1a*Pd1b, Pd2a*Pd1b, Pd1a*Pd2b, Pd2a*Pd2b, Pd3a*Pd1b, Pd4a*Pd1b, Pd3a*Pd2b, Pd4a*Pd2b, Pd3a*Pd3b, Pd4a*Pd3b, Pd3a*Pd4b, Pd4a*Pd4b, Pd1a*Pd3b, Pd2a*Pd3b, Pd1a*Pd4b, Pd2a*Pd4b];
        B = [B1; B2; B3];
        K_e = K_e + (B')*D*B * a_1*b_1*wgt;
        N = [P1a*P1b, P2a*P1b, P1a*P2b, P2a*P2b, P3a*P1b, P4a*P1b, P3a*P2b, P4a*P2b, P3a*P3b, P4a*P3b, P3a*P4b, P4a*P4b, P1a*P3b, P2a*P3b, P1a*P4b, P2a*P4b];
        F_e_unit = F_e_unit + (N')*a_1*b_1*wgt;
    end
end

%% Mascara HSS
HSS_x0 = (a - b_col)/2;  HSS_y0 = (b - b_col)/2;
HSS_x1 = HSS_x0 + b_col; HSS_y1 = HSS_y0 + b_col;
elem_bajo_HSS = false(n_e, 1);
for e = 1:n_e
    xc_e = mean(x_j(e_j(e,:)));
    yc_e = mean(y_j(e_j(e,:)));
    elem_bajo_HSS(e) = (xc_e > HSS_x0) && (xc_e < HSS_x1) && ...
                       (yc_e > HSS_y0) && (yc_e < HSS_y1);
end

%% Ensamblaje
n_g = n_dof*n_j;
K = zeros(n_g, n_g);
F = zeros(n_g, 1);
for e = 1:n_e
    q_e = q_HSS * double(elem_bajo_HSS(e));
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

% BC pernos
k_pen = 1e20;
for k = 1:nPernos
    g = n_dof*(nodo_perno(k)-1) + 1;
    K(g, g) = K(g, g) + k_pen;
end

%% Solve
t_solve = tic;
Z = K \ F;
t_solve_val = toc(t_solve);
fprintf('Solve K (%dx%d): %.3f s\n', n_g, n_g, t_solve_val);

w_nodo = Z(1:n_dof:end);
W_mat = zeros(n_b+1, n_a+1);
X_grid = zeros(n_b+1, n_a+1);
Y_grid = zeros(n_b+1, n_a+1);
for i = 1:n_a+1
    for j = 1:n_b+1
        node = (i-1)*(n_b+1) + j;
        X_grid(j, i) = x_j(node);
        Y_grid(j, i) = y_j(node);
        W_mat(j, i)  = w_nodo(node) * 1000;
    end
end

%% Momentos en nodos (promediados desde centro de elementos)
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
        M_x_n(j) = M_x_n(j)/cnt(j);
        M_y_n(j) = M_y_n(j)/cnt(j);
        M_xy_n(j) = M_xy_n(j)/cnt(j);
    end
end

%% ========================================================================
%  CÁLCULO σ_vm REAL en la fibra extrema (z = -t/2)
%  Para Kirchhoff: σ_xx = -6 Mx / t², σ_yy = -6 My / t², τ_xy = -6 Mxy / t²
%  σ_vm = √(σ_xx² - σ_xx σ_yy + σ_yy² + 3 τ_xy²)
%% ========================================================================
sigma_xx = zeros(n_j, 1);
sigma_yy = zeros(n_j, 1);
tau_xy   = zeros(n_j, 1);
for j = 1:n_j
    sigma_xx(j) = -6 * M_x_n(j)  / t_pl^2;   % kN/m²
    sigma_yy(j) = -6 * M_y_n(j)  / t_pl^2;
    tau_xy(j)   = -6 * M_xy_n(j) / t_pl^2;
end
% Convertir a MPa: 1 kN/m² = 0.001 MPa
sigma_xx_MPa = sigma_xx / 1000;
sigma_yy_MPa = sigma_yy / 1000;
tau_xy_MPa   = tau_xy   / 1000;
% von Mises (plane stress)
sigma_vm = sqrt(sigma_xx_MPa.^2 - sigma_xx_MPa.*sigma_yy_MPa + sigma_yy_MPa.^2 ...
                + 3*tau_xy_MPa.^2);

% Reshape a matriz para contour
VM_mat = zeros(n_b+1, n_a+1);
SXX_mat = zeros(n_b+1, n_a+1);
SYY_mat = zeros(n_b+1, n_a+1);
TXY_mat = zeros(n_b+1, n_a+1);
for i = 1:n_a+1
    for j = 1:n_b+1
        node = (i-1)*(n_b+1) + j;
        VM_mat(j,i)  = sigma_vm(node);
        SXX_mat(j,i) = sigma_xx_MPa(node);
        SYY_mat(j,i) = sigma_yy_MPa(node);
        TXY_mat(j,i) = tau_xy_MPa(node);
    end
end

%% Colormap SAP2000 — calidad ingeniería
sap2000 = [0,0,0.5;       % azul oscuro
           0,0,1;         % azul
           0,0.5,1;       % azul claro
           0,1,1;         % cyan
           0.2,1,0.6;     % verde-cyan
           0.5,1,0.3;     % verde claro
           1,1,0;         % amarillo
           1,0.7,0;       % naranja
           1,0.3,0;       % rojo-naranja
           1,0,0;         % rojo
           0.7,0,0];      % rojo oscuro

%% =========================================================================
%  FIG 1: σ_vm contour LLENADO (calidad SAP2000)
%% =========================================================================
figure('Position',[80 80 1100 850]); hold on;
colormap(sap2000);
contourf(X_grid*1000, Y_grid*1000, VM_mat, 30, 'LineColor','none');
cb = colorbar; cb.Label.String = '\sigma_{vm}  [MPa]';
cb.FontSize = 12;
% Isolíneas etiquetadas cada 10 MPa
sigma_max = max(max(VM_mat));
levels_iso = 10:10:floor(sigma_max/10)*10;
if numel(levels_iso) >= 2
    [C, h] = contour(X_grid*1000, Y_grid*1000, VM_mat, levels_iso, ...
                     'LineColor','k', 'LineWidth',0.6);
    clabel(C, h, 'FontSize',8, 'Color','k', 'LabelSpacing',300);
end
% Huella HSS
plot([HSS_x0 HSS_x1 HSS_x1 HSS_x0 HSS_x0]*1000, ...
     [HSS_y0 HSS_y0 HSS_y1 HSS_y1 HSS_y0]*1000, ...
     'k--', 'LineWidth',2);
text(a/2*1000, (HSS_y0 + 0.02)*1000, 'huella HSS', ...
     'HorizontalAlignment','center', 'FontSize',10, ...
     'FontWeight','bold', 'Color',[.3 .3 .3]);
% Pernos numerados
for k = 1:nPernos
    plot(xp(k)*1000, yp(k)*1000, 'ko', 'MarkerSize',13, ...
         'MarkerFaceColor',[1 1 1], 'LineWidth',1.5);
    text(xp(k)*1000, yp(k)*1000, sprintf('B%d', k), ...
         'HorizontalAlignment','center', 'FontSize',8, ...
         'FontWeight','bold', 'Color','k');
end
title(sprintf('\\sigma_{vm}  fibra extrema (z = -t/2)  —  %d×%d Q4-BFS  —  max = %.1f MPa', ...
              n_a, n_b, sigma_max), 'FontSize',12);
xlabel('x  [mm]'); ylabel('y  [mm]'); axis equal tight;
grid on; box on; set(gca,'Layer','top','FontSize',11);
saveas(gcf, 'demo_vm_figs/01_vm_contour.png');

%% =========================================================================
%  FIG 2: σ_vm 3D SURFACE (con colormap SAP2000)
%% =========================================================================
figure('Position',[80 80 1100 850]);
colormap(sap2000);
surf(X_grid*1000, Y_grid*1000, VM_mat, ...
     'EdgeColor',[.2 .2 .2], 'LineWidth',0.3);
shading interp;
cb = colorbar; cb.Label.String = '\sigma_{vm}  [MPa]'; cb.FontSize = 12;
title('\sigma_{vm}  superficie 3D — Kirchhoff Q4-BFS', 'FontSize',12);
xlabel('x  [mm]'); ylabel('y  [mm]'); zlabel('\sigma_{vm}  [MPa]');
view(35, 30); grid on;
saveas(gcf, 'demo_vm_figs/02_vm_surf3D.png');

%% =========================================================================
%  FIG 3: Componentes σ_xx, σ_yy, τ_xy en 2×2 subplot
%% =========================================================================
figure('Position',[60 60 1300 1000]);
colormap(sap2000);

subplot(2,2,1);
contourf(X_grid*1000, Y_grid*1000, SXX_mat, 25, 'LineColor','none');
hold on;
plot([HSS_x0 HSS_x1 HSS_x1 HSS_x0 HSS_x0]*1000, ...
     [HSS_y0 HSS_y0 HSS_y1 HSS_y1 HSS_y0]*1000, 'k--', 'LineWidth',1.5);
for k = 1:nPernos, plot(xp(k)*1000, yp(k)*1000, 'ko', 'MarkerSize',7, 'MarkerFaceColor','w'); end
cb = colorbar; cb.Label.String = '\sigma_{xx}  [MPa]';
title(sprintf('\\sigma_{xx}  (max = %.1f MPa)', max(max(abs(SXX_mat)))), 'FontSize',11);
xlabel('x [mm]'); ylabel('y [mm]'); axis equal tight; grid on;

subplot(2,2,2);
contourf(X_grid*1000, Y_grid*1000, SYY_mat, 25, 'LineColor','none');
hold on;
plot([HSS_x0 HSS_x1 HSS_x1 HSS_x0 HSS_x0]*1000, ...
     [HSS_y0 HSS_y0 HSS_y1 HSS_y1 HSS_y0]*1000, 'k--', 'LineWidth',1.5);
for k = 1:nPernos, plot(xp(k)*1000, yp(k)*1000, 'ko', 'MarkerSize',7, 'MarkerFaceColor','w'); end
cb = colorbar; cb.Label.String = '\sigma_{yy}  [MPa]';
title(sprintf('\\sigma_{yy}  (max = %.1f MPa)', max(max(abs(SYY_mat)))), 'FontSize',11);
xlabel('x [mm]'); ylabel('y [mm]'); axis equal tight; grid on;

subplot(2,2,3);
contourf(X_grid*1000, Y_grid*1000, TXY_mat, 25, 'LineColor','none');
hold on;
plot([HSS_x0 HSS_x1 HSS_x1 HSS_x0 HSS_x0]*1000, ...
     [HSS_y0 HSS_y0 HSS_y1 HSS_y1 HSS_y0]*1000, 'k--', 'LineWidth',1.5);
for k = 1:nPernos, plot(xp(k)*1000, yp(k)*1000, 'ko', 'MarkerSize',7, 'MarkerFaceColor','w'); end
cb = colorbar; cb.Label.String = '\tau_{xy}  [MPa]';
title(sprintf('\\tau_{xy}  (max = %.1f MPa)', max(max(abs(TXY_mat)))), 'FontSize',11);
xlabel('x [mm]'); ylabel('y [mm]'); axis equal tight; grid on;

subplot(2,2,4);
contourf(X_grid*1000, Y_grid*1000, VM_mat, 25, 'LineColor','none');
hold on;
plot([HSS_x0 HSS_x1 HSS_x1 HSS_x0 HSS_x0]*1000, ...
     [HSS_y0 HSS_y0 HSS_y1 HSS_y1 HSS_y0]*1000, 'k--', 'LineWidth',1.5);
for k = 1:nPernos, plot(xp(k)*1000, yp(k)*1000, 'ko', 'MarkerSize',7, 'MarkerFaceColor','w'); end
cb = colorbar; cb.Label.String = '\sigma_{vm}  [MPa]';
title(sprintf('\\sigma_{vm}  (max = %.1f MPa)', sigma_max), 'FontSize',11);
xlabel('x [mm]'); ylabel('y [mm]'); axis equal tight; grid on;

saveas(gcf, 'demo_vm_figs/03_componentes.png');

%% =========================================================================
%  Reporte
%% =========================================================================
fprintf('\n══════════════════════════════════════════════════════════\n');
fprintf('  RESULTADOS σ_vm — FEM Kirchhoff Q4-BFS\n');
fprintf('══════════════════════════════════════════════════════════\n');
fprintf('  Placa            : %.0f × %.0f × %.1f mm\n', a*1000, b*1000, t_pl*1000);
fprintf('  Malla            : %d × %d Q4 (%d nodos, %d GDL)\n', n_a, n_b, n_j, n_g);
fprintf('  Pernos           : %d apoyos puntuales\n', nPernos);
fprintf('  Carga            : %.0f kN sobre %.0f × %.0f mm (q = %.0f kN/m²)\n', P_axial, b_col*1000, b_col*1000, q_HSS);
fprintf('  ──────────────────────────────────────────────────────────\n');
fprintf('  w_min            : %+.3f mm\n', min(min(W_mat)));
fprintf('  w_max            : %+.3f mm\n', max(max(W_mat)));
fprintf('  |σ_xx|_max       : %.2f MPa\n', max(max(abs(SXX_mat))));
fprintf('  |σ_yy|_max       : %.2f MPa\n', max(max(abs(SYY_mat))));
fprintf('  |τ_xy|_max       : %.2f MPa\n', max(max(abs(TXY_mat))));
fprintf('  σ_vm_max         : %.2f MPa\n', sigma_max);
fprintf('  Acero S355 fy    : 355 MPa   → ratio σ_vm/fy = %.2f\n', sigma_max/355);
fprintf('  ──────────────────────────────────────────────────────────\n');
fprintf('  Tiempo solve     : %.3f s\n', t_solve_val);
fprintf('  Tiempo total     : %.2f s\n', toc(t_total));
fprintf('══════════════════════════════════════════════════════════\n\n');

disp('Done');
exit;
