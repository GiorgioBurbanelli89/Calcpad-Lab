%% Demo: Visualización FEM realista — placas con flexión y tensiones
close all
mkdir('demo_fem_figs');

%% ============================================================
%% 1. Placa cuadrada simplemente apoyada — flexión (Navier)
%% w(x,y) = Σ (16q/π⁶D) sin(mπx/a)sin(nπy/b)/(mn((m/a)²+(n/b)²)²)
%% ============================================================
a = 4; b = 4;                     % dimensiones placa (m)
q = 10000;                        % carga (N/m²)
E = 30e9; nu = 0.2; t = 0.2;      % hormigón
D = E*t^3/(12*(1-nu^2));          % rigidez flexional

[X,Y] = meshgrid(linspace(0,a,50), linspace(0,b,50));
W = zeros(size(X));
for m = 1:2:7  % impares
    for n = 1:2:7
        coef = 16*q/(pi^6*D) / (m*n*((m/a)^2+(n/b)^2)^2);
        W = W + coef*sin(m*pi*X/a).*sin(n*pi*Y/b);
    end
end

figure('Position',[100 100 800 600]);
surf(X, Y, -W*1000, 'EdgeColor','none');     % en mm, invertida (sentido físico)
shading interp; colorbar;
title('1. Placa SS con carga uniforme — flecha (mm)');
xlabel('x (m)'); ylabel('y (m)'); zlabel('w (mm)');
view(35,30); colormap(jet);
saveas(gcf,'demo_fem_figs/01_placa_flexion.png');

%% ============================================================
%% 2. Misma placa con HUECO circular central — NaN trick
%% ============================================================
Wh = -W*1000;
mask = ((X-a/2).^2 + (Y-b/2).^2) < 0.6^2;
Wh(mask) = NaN;
figure('Position',[100 100 800 600]);
surf(X, Y, Wh, 'EdgeColor','none');
shading interp; colorbar;
title('2. Placa con hueco circular — deformada (mm)');
xlabel('x (m)'); ylabel('y (m)'); zlabel('w (mm)');
view(35,30); colormap(jet);
saveas(gcf,'demo_fem_figs/02_placa_hueco.png');

%% ============================================================
%% 3. Losa en L con malla triangular FEM (trisurf)
%% ============================================================
[xg, yg] = meshgrid(0:0.2:4, 0:0.2:4);
inside = (xg<=2 | yg<=2);
xn = xg(inside); yn = yg(inside);
% deflexión simulada (mayor hacia la esquina interior)
zn = sin(pi*xn/4).*sin(pi*yn/4) .* (1 - exp(-((xn-2).^2+(yn-2).^2)/2));
tri = delaunay(xn, yn);
% Filtro triángulos cuyo centroide cae fuera de la L
cx = mean(xn(tri),2); cy = mean(yn(tri),2);
keep = (cx<=2 | cy<=2);
tri = tri(keep,:);

figure('Position',[100 100 800 600]);
trisurf(tri, xn, yn, zn*10, 'EdgeColor',[.3 .3 .3], 'LineWidth',0.3);
shading interp; colorbar;
title('3. Losa en L — malla FEM (T3) con deformada');
xlabel('x (m)'); ylabel('y (m)'); zlabel('w (mm)');
view(40,30); colormap(parula);
saveas(gcf,'demo_fem_figs/03_losa_L.png');

%% ============================================================
%% 4. Placa circular — coordenadas polares (axisimétrica)
%% w(r) = qR⁴/(64D) · (1 - (r/R)²)²  (simply supported edge)
%% ============================================================
R = 2;
[rho, theta] = meshgrid(linspace(0,R,40), linspace(0,2*pi,60));
Xc = rho.*cos(theta); Yc = rho.*sin(theta);
qq = 8000;
DD = E*0.15^3/(12*(1-nu^2));
Wc = qq*R^4/(64*DD) * (1 - (rho/R).^2).^2 * 1000;  % mm

figure('Position',[100 100 800 600]);
surf(Xc, Yc, -Wc, 'EdgeColor','none');
shading interp; colorbar;
title('4. Placa circular — flexión axisimétrica');
xlabel('x (m)'); ylabel('y (m)'); zlabel('w (mm)');
view(35,30); colormap(jet);
saveas(gcf,'demo_fem_figs/04_placa_circular.png');

%% ============================================================
%% 5. PATCH Q4 — malla de cuadriláteros con tensión von Mises
%% (cada elemento coloreado por su σ_vm — discontinuo, FEM clásico)
%% ============================================================
% Generar malla Q4 5×5
nx = 8; ny = 8;
xv = linspace(0, 4, nx+1);
yv = linspace(0, 4, ny+1);
[Xq, Yq] = meshgrid(xv, yv);

% Tensión von Mises simulada (cuadrático con máximo en centro)
sigma_vm = zeros(ny, nx);
for j = 1:ny
    for i = 1:nx
        xc = (Xq(j,i)+Xq(j,i+1))/2;
        yc = (Yq(j,i)+Yq(j+1,i))/2;
        sigma_vm(j,i) = 50e6 * (1 - ((xc-2).^2 + (yc-2).^2)/8);  % Pa
    end
end

figure('Position',[100 100 800 600]); hold on;
% Cada elemento Q4 como un patch con su color
for j = 1:ny
    for i = 1:nx
        xq = [Xq(j,i) Xq(j,i+1) Xq(j+1,i+1) Xq(j+1,i)];
        yq = [Yq(j,i) Yq(j,i+1) Yq(j+1,i+1) Yq(j+1,i)];
        patch(xq, yq, sigma_vm(j,i)/1e6, 'EdgeColor','k', 'LineWidth',0.5);
    end
end
colormap(jet); cb = colorbar; cb.Label.String = '\sigma_{vm} (MPa)';
title('5. Patch Q4 — tensión von Mises por elemento (FEM)');
xlabel('x (m)'); ylabel('y (m)'); axis equal tight;
saveas(gcf,'demo_fem_figs/05_patch_Q4_vm.png');

%% ============================================================
%% 6. Deformada + mapa de momento M_xx (color sobre superficie 3D)
%% ============================================================
% M_xx = -D (∂²w/∂x² + ν ∂²w/∂y²)
% Aproximo numéricamente con diferencias finitas
[dx, dy] = gradient(W, X(1,2)-X(1,1), Y(2,1)-Y(1,1));
[d2x_x, ~] = gradient(dx, X(1,2)-X(1,1), Y(2,1)-Y(1,1));
[~, d2y_y] = gradient(dy, X(1,2)-X(1,1), Y(2,1)-Y(1,1));
Mxx = -D*(d2x_x + nu*d2y_y);

figure('Position',[100 100 800 600]);
surf(X, Y, -W*1000, Mxx/1000, 'EdgeColor','none');   % color = Mxx (kN·m/m)
shading interp; cb = colorbar; cb.Label.String = 'M_{xx} (kN·m/m)';
title('6. Deformada coloreada por M_{xx}');
xlabel('x (m)'); ylabel('y (m)'); zlabel('w (mm)');
view(40,35); colormap(jet);
saveas(gcf,'demo_fem_figs/06_deformada_Mxx.png');

%% ============================================================
%% 7. Multi-vista: planta + alzado + iso — subplot
%% ============================================================
figure('Position',[100 100 1200 400]);
subplot(1,3,1);
surf(X, Y, -W*1000, 'EdgeColor','none'); shading interp;
view(0,90); colorbar; title('Planta (top)'); axis equal tight;
xlabel('x'); ylabel('y');

subplot(1,3,2);
surf(X, Y, -W*1000, 'EdgeColor','none'); shading interp;
view(0,0); colorbar; title('Alzado x-z (front)');
xlabel('x'); zlabel('w');

subplot(1,3,3);
surf(X, Y, -W*1000, 'EdgeColor','none'); shading interp;
view(40,30); colorbar; title('Isométrica');
xlabel('x'); ylabel('y'); zlabel('w');

colormap(jet);
saveas(gcf,'demo_fem_figs/07_multivista.png');

%% ============================================================
%% 8. Patch — geometría irregular custom (sección compuesta)
%% ============================================================
figure('Position',[100 100 700 700]); hold on;
% Hormigón (rectángulo exterior)
patch([0 4 4 0], [0 0 5 5], [.85 .85 .85], 'EdgeColor','k', 'LineWidth',1.5);
% Hueco rectangular
patch([1 3 3 1], [2 2 4 4], 'w', 'EdgeColor','k', 'LineWidth',1.5);
% Armadura inferior (puntos)
xs = linspace(0.3, 3.7, 6); plot(xs, 0.3*ones(size(xs)), 'ko', 'MarkerFaceColor','k', 'MarkerSize',10);
% Armadura superior
plot(xs, 4.7*ones(size(xs)), 'ko', 'MarkerFaceColor','k', 'MarkerSize',8);
% Cotas
text(2, -0.3, 'b = 4 m', 'HorizontalAlignment','center', 'FontSize',12);
text(-0.3, 2.5, 'h = 5 m', 'Rotation',90, 'HorizontalAlignment','center', 'FontSize',12);
axis equal; axis([-0.5 4.5 -0.7 5.5]); axis off;
title('8. Patch — sección compuesta con cotas');
saveas(gcf,'demo_fem_figs/08_seccion_compuesta.png');

disp('Done');
exit;
