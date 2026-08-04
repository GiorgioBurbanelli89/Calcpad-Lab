%% Demo: Visualización para los 5 tipos de elementos FEM
%% Todos usan `patch` — sólo cambia la malla y la variable graficada
close all
mkdir('demo_fem_elementos');

%% Mall arbitraria T3 (Delaunay) en una placa con hueco circular
% Esta misma malla la reutilizamos para membrana, plate, shell
[xg, yg] = meshgrid(linspace(0,4,15), linspace(0,3,12));
xn = xg(:); yn = yg(:);
% Quitar puntos dentro del hueco circular
mask = (xn-2).^2 + (yn-1.5).^2 > 0.6^2;
xn = xn(mask); yn = yn(mask);
tri = delaunay(xn, yn);
% Filtrar triángulos cuyo centroide cae dentro del hueco
% (centroide por columnas: indexar un vector por una columna preserva la forma)
cx = (xn(tri(:,1)) + xn(tri(:,2)) + xn(tri(:,3))) / 3;
cy = (yn(tri(:,1)) + yn(tri(:,2)) + yn(tri(:,3))) / 3;
tri = tri((cx-2).^2 + (cy-1.5).^2 > 0.6^2, :);

%% =====================================================================
%% 1. MEMBRANA — plane stress, tensión en plano
%% Variable: σ_vm (von Mises) por NODO (color interpolado)
%% =====================================================================
% σ_vm simulada: concentración alrededor del hueco
r2 = (xn-2).^2 + (yn-1.5).^2;
sigma_vm = 100e6 * (1 ./ (r2 + 0.5)) / 2;   % Pa

figure('Position',[100 100 900 500]);
patch('Faces', tri, 'Vertices', [xn yn], ...
      'FaceVertexCData', sigma_vm/1e6, ...
      'FaceColor', 'interp', 'EdgeColor', [.3 .3 .3], 'LineWidth', 0.5);
colormap(jet); cb = colorbar; cb.Label.String = '\sigma_{vm} (MPa)';
title('1. MEMBRANA — \sigma_{vm} (von Mises) nodal, plane stress');
xlabel('x (m)'); ylabel('y (m)'); axis equal tight;
saveas(gcf,'demo_fem_elementos/01_membrana.png');

%% =====================================================================
%% 2. PLATE THIN (Kirchhoff) — flexión fuera del plano
%% Variable: w (flecha) — plot 2D planta + plot 3D deformada
%% =====================================================================
% Flecha w simulada (mayor en el centro lejos del hueco)
w = 5*sin(pi*xn/4).*sin(pi*yn/3);    % mm
% Bajar la zona cerca del hueco
w = w .* (1 - exp(-r2*2));

figure('Position',[100 100 1200 500]);
% (a) Planta con color = w
subplot(1,2,1);
patch('Faces', tri, 'Vertices', [xn yn], ...
      'FaceVertexCData', w, 'FaceColor','interp', 'EdgeColor','none');
colormap(jet); cb = colorbar; cb.Label.String = 'w (mm)';
title('2a. PLATE THIN — flecha w (planta)');
xlabel('x'); ylabel('y'); axis equal tight;
% (b) Deformada 3D con color = M_xx (simulado curvatura local)
% M_xx ≈ -D · ∂²w/∂x²  (curvatura) — aproximación por gradiente
M_xx = -50 * w;   % proxy: flecha negativa donde M_xx es máximo
subplot(1,2,2);
patch('Faces', tri, 'Vertices', [xn yn w], ...
      'FaceVertexCData', M_xx, 'FaceColor','interp', 'EdgeColor','none');
colormap(jet); cb = colorbar; cb.Label.String = 'M_{xx} proxy';
title('2b. PLATE THIN — deformada 3D coloreada por M_{xx}');
xlabel('x'); ylabel('y'); zlabel('w (mm)'); view(40,30);
saveas(gcf,'demo_fem_elementos/02_plate_thin.png');

%% =====================================================================
%% 3. PLATE THICK (Mindlin) — incluye cortante transversal
%% Variable adicional: V_x, V_y por elemento (no nodal en Mindlin clásico)
%% =====================================================================
% Cortante V_x por elemento (centroide)
ex = (xn(tri(:,1)) + xn(tri(:,2)) + xn(tri(:,3))) / 3;  % centroide elemental
ey = (yn(tri(:,1)) + yn(tri(:,2)) + yn(tri(:,3))) / 3;
V_x_elem = 100 * cos(pi*ex/4) .* sin(pi*ey/3);  % kN/m, simulado

figure('Position',[100 100 900 500]);
patch('Faces', tri, 'Vertices', [xn yn], ...
      'FaceVertexCData', V_x_elem, 'FaceColor','flat', ...   % flat = un color por elemento
      'EdgeColor', [.3 .3 .3], 'LineWidth', 0.4);
colormap(jet); cb = colorbar; cb.Label.String = 'V_x (kN/m)';
title('3. PLATE THICK (Mindlin) — V_x por ELEMENTO (FaceColor=flat)');
xlabel('x (m)'); ylabel('y (m)'); axis equal tight;
saveas(gcf,'demo_fem_elementos/03_plate_thick.png');

%% =====================================================================
%% 4. SHELL THIN — cáscara curva 3D (membrana + plate combinado)
%% Variable: σ_vm sobre superficie curva
%% =====================================================================
% Generamos una cáscara cilíndrica con malla triangular
nU = 30; nV = 15;
U = linspace(0, pi, nU);
V_ = linspace(0, 4, nV);
[Ug, Vg] = meshgrid(U, V_);
R_cyl = 2;
Xs = R_cyl*cos(Ug);
Ys = Vg;
Zs = R_cyl*sin(Ug);
Xs_v = Xs(:); Ys_v = Ys(:); Zs_v = Zs(:);
% Conectividad: cada cuadrángulo → 2 triángulos
tri_s = [];
for j = 1:nV-1
    for i = 1:nU-1
        n1 = (j-1)*nU + i;
        n2 = n1 + 1;
        n3 = n1 + nU + 1;
        n4 = n1 + nU;
        tri_s = [tri_s; n1 n2 n3; n1 n3 n4];
    end
end
% σ_vm sobre la cáscara (máximo en el ápice z=R, y=L/2)
dist_apice = (Xs_v - 0).^2 + (Ys_v - 2).^2 + (Zs_v - R_cyl).^2;
sigma_shell = 80e6 * exp(-dist_apice/3);

figure('Position',[100 100 900 600]);
patch('Faces', tri_s, 'Vertices', [Xs_v Ys_v Zs_v], ...
      'FaceVertexCData', sigma_shell/1e6, 'FaceColor','interp', ...
      'EdgeColor', [.3 .3 .3], 'LineWidth', 0.2);
colormap(jet); cb = colorbar; cb.Label.String = '\sigma_{vm} (MPa)';
title('4. SHELL THIN — cáscara cilíndrica curva, \sigma_{vm}');
xlabel('x'); ylabel('y'); zlabel('z'); view(35,25);
axis equal; light; lighting gouraud;
saveas(gcf,'demo_fem_elementos/04_shell_thin.png');

%% =====================================================================
%% 5. SHELL THICK — igual que thin pero con cortantes transversales
%% Visualización idéntica + segundo plot del cortante V_xz
%% =====================================================================
% V_xz por elemento (centroide)
ex_s = (Xs_v(tri_s(:,1)) + Xs_v(tri_s(:,2)) + Xs_v(tri_s(:,3))) / 3;
ey_s = (Ys_v(tri_s(:,1)) + Ys_v(tri_s(:,2)) + Ys_v(tri_s(:,3))) / 3;
ez_s = (Zs_v(tri_s(:,1)) + Zs_v(tri_s(:,2)) + Zs_v(tri_s(:,3))) / 3;
V_xz = 30 * sin(2*pi*ex_s/4) .* cos(pi*ey_s/4);

figure('Position',[100 100 900 600]);
patch('Faces', tri_s, 'Vertices', [Xs_v Ys_v Zs_v], ...
      'FaceVertexCData', V_xz, 'FaceColor','flat', ...
      'EdgeColor', [.4 .4 .4], 'LineWidth', 0.2);
colormap(jet); cb = colorbar; cb.Label.String = 'V_{xz} (kN/m)';
title('5. SHELL THICK (Mindlin shell) — V_{xz} por elemento');
xlabel('x'); ylabel('y'); zlabel('z'); view(35,25);
axis equal; light; lighting gouraud;
saveas(gcf,'demo_fem_elementos/05_shell_thick.png');

%% =====================================================================
%% 6. LAYERED SHELL (composite) — 3 capas separadas verticalmente
%% Una vista que apila las 3 capas en z (separadas para visualizar)
%% =====================================================================
figure('Position',[100 100 1100 600]); hold on;

% Capa 1 (fibra a 0°): σ_xx alta
layer1_z = 0;
sigma_L1 = 150e6 * sin(pi*Xs_v/4) .* cos(pi*Ys_v/4);
patch('Faces', tri_s, 'Vertices', [Xs_v Ys_v Zs_v+layer1_z], ...
      'FaceVertexCData', sigma_L1/1e6, 'FaceColor','interp', ...
      'EdgeColor','none', 'FaceAlpha', 1);

% Capa 2 (fibra a 90°): rotada, σ_yy alta
layer2_z = 0.8;
sigma_L2 = 100e6 * cos(pi*Xs_v/4) .* sin(pi*Ys_v/4);
patch('Faces', tri_s, 'Vertices', [Xs_v Ys_v Zs_v+layer2_z], ...
      'FaceVertexCData', sigma_L2/1e6, 'FaceColor','interp', ...
      'EdgeColor','none', 'FaceAlpha', 1);

% Capa 3 (fibra a 45°): mezcla
layer3_z = 1.6;
sigma_L3 = 80e6 * (sin(pi*Xs_v/4) + cos(pi*Ys_v/4))/2;
patch('Faces', tri_s, 'Vertices', [Xs_v Ys_v Zs_v+layer3_z], ...
      'FaceVertexCData', sigma_L3/1e6, 'FaceColor','interp', ...
      'EdgeColor','none', 'FaceAlpha', 1);

text(-2.5, 0, R_cyl, 'Capa 1 (0°)', 'FontSize',11, 'Color','k');
text(-2.5, 0, R_cyl+0.8, 'Capa 2 (90°)', 'FontSize',11, 'Color','k');
text(-2.5, 0, R_cyl+1.6, 'Capa 3 (45°)', 'FontSize',11, 'Color','k');

colormap(jet); cb = colorbar; cb.Label.String = '\sigma (MPa)';
title('6. LAYERED SHELL — 3 capas separadas, σ por capa');
xlabel('x'); ylabel('y'); zlabel('z'); view(35,20);
axis equal; light; lighting gouraud;
saveas(gcf,'demo_fem_elementos/06_layered_shell.png');

disp('Done');
exit;
