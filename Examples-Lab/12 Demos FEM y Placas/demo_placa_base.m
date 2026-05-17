%% Placa base de columna metálica con N×M orificios para pernos
close all
mkdir('demo_placa_base_figs');

%% ===== PARÁMETROS =====
Lx = 500;          % largo placa (mm)
Ly = 400;          % ancho placa (mm)
t = 30;            % espesor (mm)
nx = 4;            % nº pernos en dirección x
ny = 3;            % nº pernos en dirección y
d_perno = 20;      % diámetro perno M20
d_orif = 22;       % diámetro orificio (perno + holgura)
edge = 50;         % distancia al borde (mm)
% Columna HSS 300×300×8 (tubo cuadrado hueco) en el centro
b_col = 300; h_col = 300; t_col = 8;   % HSS 300×300×8

%% ===== POSICIONES DE PERNOS (solo perimetrales, NO en el centro) =====
xb = linspace(edge, Lx-edge, nx);
yb = linspace(edge, Ly-edge, ny);
[XB, YB] = meshgrid(xb, yb);
xp_all = XB(:); yp_all = YB(:);
% Filtro: solo pernos en el perímetro (primera/última fila O primera/última columna)
is_perim = (XB == xb(1)) | (XB == xb(end)) | (YB == yb(1)) | (YB == yb(end));
xp = xp_all(is_perim(:));
yp = yp_all(is_perim(:));
nPernos = numel(xp);
fprintf('Total pernos perimetrales: %d (de %d×%d=%d totales)\n', nPernos, nx, ny, nx*ny);

%% =====================================================================
%% FIGURA 1 — Vista en planta con cotas
%% =====================================================================
figure('Position',[100 100 1000 700]); hold on; axis equal;

% Placa base (rectángulo gris)
patch([0 Lx Lx 0], [0 0 Ly Ly], [.78 .78 .82], 'EdgeColor','k', 'LineWidth',1.5);

% Columna HSS 300×300×8 (tubo cuadrado hueco) en el centro
xc = Lx/2 - b_col/2; yc = Ly/2 - h_col/2;
% Sección exterior
patch([xc xc+b_col xc+b_col xc], [yc yc yc+h_col yc+h_col], [.5 .6 .9], ...
      'EdgeColor','b', 'LineWidth',2, 'FaceAlpha',0.6);
% Hueco interior (tubo)
xi = xc + t_col; yi = yc + t_col;
bi = b_col - 2*t_col; hi = h_col - 2*t_col;
patch([xi xi+bi xi+bi xi], [yi yi yi+hi yi+hi], 'w', ...
      'EdgeColor','b', 'LineWidth',1.5);
text(Lx/2, Ly/2, sprintf('HSS\n%d×%d×%d', b_col, h_col, t_col), ...
     'HorizontalAlignment','center', 'FontSize',10, ...
     'FontWeight','bold', 'Color','b');

% Orificios (círculos blancos)
theta = linspace(0, 2*pi, 30);
for k = 1:nPernos
    rorif = d_orif/2;
    xx = xp(k) + rorif*cos(theta);
    yy = yp(k) + rorif*sin(theta);
    patch(xx, yy, 'w', 'EdgeColor','k', 'LineWidth',1);
    % Cruz indicadora de centro
    line([xp(k)-rorif*1.3 xp(k)+rorif*1.3], [yp(k) yp(k)], 'Color','k', 'LineWidth',0.5);
    line([xp(k) xp(k)], [yp(k)-rorif*1.3 yp(k)+rorif*1.3], 'Color','k', 'LineWidth',0.5);
end

% Numeración pernos
for k = 1:nPernos
    text(xp(k), yp(k)+18, sprintf('B%d', k), 'HorizontalAlignment','center', ...
         'FontSize',9, 'Color','r', 'FontWeight','bold');
end

% Cotas exteriores
% Cota Lx (abajo)
line([0 Lx], [-30 -30], 'Color','k');
line([0 0], [-25 -35], 'Color','k');
line([Lx Lx], [-25 -35], 'Color','k');
text(Lx/2, -45, sprintf('L_x = %d mm', Lx), 'HorizontalAlignment','center', 'FontSize',11);
% Cota Ly (izquierda)
line([-30 -30], [0 Ly], 'Color','k');
line([-25 -35], [0 0], 'Color','k');
line([-25 -35], [Ly Ly], 'Color','k');
text(-50, Ly/2, sprintf('L_y = %d mm', Ly), 'HorizontalAlignment','center', ...
     'Rotation',90, 'FontSize',11);
% Cota separación pernos en x
if nx >= 2
    sx = xb(2) - xb(1);
    line([xb(1) xb(2)], [Ly+30 Ly+30], 'Color','b');
    line([xb(1) xb(1)], [Ly+25 Ly+35], 'Color','b');
    line([xb(2) xb(2)], [Ly+25 Ly+35], 'Color','b');
    text((xb(1)+xb(2))/2, Ly+45, sprintf('s_x = %.0f mm', sx), ...
         'HorizontalAlignment','center', 'FontSize',10, 'Color','b');
end
% Cota separación pernos en y
if ny >= 2
    sy = yb(2) - yb(1);
    line([Lx+30 Lx+30], [yb(1) yb(2)], 'Color','b');
    line([Lx+25 Lx+35], [yb(1) yb(1)], 'Color','b');
    line([Lx+25 Lx+35], [yb(2) yb(2)], 'Color','b');
    text(Lx+50, (yb(1)+yb(2))/2, sprintf('s_y = %.0f mm', sy), ...
         'HorizontalAlignment','center', 'Rotation',90, 'FontSize',10, 'Color','b');
end

% ===== COTAS DEL HSS =====
% Lado HSS (cota superior interior)
line([xc xc+b_col], [yc+h_col+15 yc+h_col+15], 'Color',[0 .5 0]);
line([xc xc], [yc+h_col+10 yc+h_col+20], 'Color',[0 .5 0]);
line([xc+b_col xc+b_col], [yc+h_col+10 yc+h_col+20], 'Color',[0 .5 0]);
text(xc+b_col/2, yc+h_col+27, sprintf('%d mm', b_col), ...
     'HorizontalAlignment','center', 'FontSize',10, 'Color',[0 .5 0]);

% Espesor t_col (detalle ampliado con flecha)
xtt = xc + b_col + 5;
line([xtt xtt+15], [yc+h_col/2 yc+h_col/2-25], 'Color','m');
text(xtt+18, yc+h_col/2-30, sprintf('t = %d mm', t_col), ...
     'FontSize',9, 'Color','m');

% Distancia HSS ↔ perno más cercano (cota "g")
% Tomamos perno B1 (esquina inferior-izquierda): xp(1), yp(1)
% Y la esquina del HSS más cercana: (xc, yc)
g_x = xc - xp(1);   % distancia horizontal centro-perno → cara HSS
g_y = yc - yp(1);
% Cota horizontal g_x (desde perno B1 hasta cara izquierda del HSS)
y_cota_g = yp(1) - 20;
line([xp(1) xc], [y_cota_g y_cota_g], 'Color',[.8 .3 0], 'LineWidth',1.2);
line([xp(1) xp(1)], [y_cota_g-3 y_cota_g+3], 'Color',[.8 .3 0]);
line([xc xc], [y_cota_g-3 y_cota_g+3], 'Color',[.8 .3 0]);
text((xp(1)+xc)/2, y_cota_g-12, sprintf('g_x = %.0f mm', g_x), ...
     'HorizontalAlignment','center', 'FontSize',9, 'Color',[.8 .3 0]);
% Cota vertical g_y
x_cota_g = xp(1) - 20;
line([x_cota_g x_cota_g], [yp(1) yc], 'Color',[.8 .3 0], 'LineWidth',1.2);
line([x_cota_g-3 x_cota_g+3], [yp(1) yp(1)], 'Color',[.8 .3 0]);
line([x_cota_g-3 x_cota_g+3], [yc yc], 'Color',[.8 .3 0]);
text(x_cota_g-15, (yp(1)+yc)/2, sprintf('g_y = %.0f mm', g_y), ...
     'HorizontalAlignment','center', 'Rotation',90, 'FontSize',9, 'Color',[.8 .3 0]);

title(sprintf('Placa base %d × %d mm — %d pernos M%d + HSS %d×%d×%d', ...
              Lx, Ly, nPernos, d_perno, b_col, h_col, t_col));
xlabel('x (mm)'); ylabel('y (mm)');
xlim([-80 Lx+90]); ylim([-80 Ly+80]);
grid on; box on;
saveas(gcf,'demo_placa_base_figs/01_planta.png');

%% =====================================================================
%% FIGURA 2 — Vista 3D con espesor (extruida)
%% =====================================================================
% Genero malla T3 que respeta los orificios
[xg, yg] = meshgrid(linspace(0,Lx,40), linspace(0,Ly,30));
xn = xg(:); yn = yg(:);
% Quitar puntos dentro de los orificios
keep = true(size(xn));
for k = 1:nPernos
    in_orif = (xn-xp(k)).^2 + (yn-yp(k)).^2 < (d_orif/2)^2;
    keep = keep & ~in_orif;
end
xn = xn(keep); yn = yn(keep);
% Agregar puntos sobre el borde de cada orificio para malla limpia
n_arc = 16;
for k = 1:nPernos
    th = linspace(0, 2*pi, n_arc+1); th(end) = [];
    xn = [xn; xp(k) + d_orif/2 * cos(th)'];
    yn = [yn; yp(k) + d_orif/2 * sin(th)'];
end
tri = delaunay(xn, yn);
% Filtrar triángulos dentro de los orificios
cx = mean(xn(tri),2); cy = mean(yn(tri),2);
keep_tri = true(size(cx));
for k = 1:nPernos
    in_orif = (cx-xp(k)).^2 + (cy-yp(k)).^2 < (d_orif/2 * 0.95)^2;
    keep_tri = keep_tri & ~in_orif;
end
tri = tri(keep_tri,:);
nNodes = numel(xn);
nTri = size(tri,1);

figure('Position',[100 100 1100 700]); hold on;
% Cara superior de la placa
z_top = t * ones(nNodes,1);
patch('Faces', tri, 'Vertices', [xn yn z_top], ...
      'FaceColor',[.78 .78 .82], 'EdgeColor',[.4 .4 .4], 'LineWidth',0.3);
% Cara inferior
z_bot = zeros(nNodes,1);
patch('Faces', tri, 'Vertices', [xn yn z_bot], ...
      'FaceColor',[.78 .78 .82], 'EdgeColor','none');
% Bordes laterales (4 lados del rectángulo)
% lado y=0
sx_pts = linspace(0, Lx, 30)';
zs_lat = [zeros(size(sx_pts)); t*ones(size(sx_pts))];
xs_lat = [sx_pts; sx_pts];
ys_lat = zeros(size(xs_lat));
% Hago el lateral como rectángulos verticales
for side = 1:4
    switch side
        case 1, p1 = [0 0]; p2 = [Lx 0];
        case 2, p1 = [Lx 0]; p2 = [Lx Ly];
        case 3, p1 = [Lx Ly]; p2 = [0 Ly];
        case 4, p1 = [0 Ly]; p2 = [0 0];
    end
    patch([p1(1) p2(1) p2(1) p1(1)], [p1(2) p2(2) p2(2) p1(2)], [0 0 t t], ...
          [.65 .65 .70], 'EdgeColor','k', 'LineWidth',1);
end
% Cilindros (pernos) — uno por orificio
for k = 1:nPernos
    th_c = linspace(0, 2*pi, 30);
    rp = d_perno/2;
    xcyl = xp(k) + rp*cos(th_c);
    ycyl = yp(k) + rp*sin(th_c);
    % Anclaje hacia abajo (longitud = 1.5·t)
    L_anc = 3*t;
    % Cuerpo del perno
    for j = 1:length(th_c)-1
        patch([xcyl(j) xcyl(j+1) xcyl(j+1) xcyl(j)], ...
              [ycyl(j) ycyl(j+1) ycyl(j+1) ycyl(j)], ...
              [-L_anc -L_anc t+15 t+15], ...
              [.3 .3 .3], 'EdgeColor','none');
    end
    % Tuerca arriba
    nut_r = d_perno*0.8;
    nut_pts = linspace(0, 2*pi, 7); nut_pts(end) = [];
    patch(xp(k)+nut_r*cos(nut_pts), yp(k)+nut_r*sin(nut_pts), ...
          (t+15)*ones(1,6), [.5 .5 .55], 'EdgeColor','k', 'LineWidth',0.5);
end

% Columna HSS 300×300×8 (tubo cuadrado hueco)
hcol_show = 180;   % altura visible (mm)
z0 = t;            % base de la columna = top de la placa
z1 = t + hcol_show;
% Coordenadas: 4 esquinas exteriores + 4 esquinas interiores
xo = [xc xc+b_col xc+b_col xc];                  % exterior
yo = [yc yc yc+h_col yc+h_col];
xi_v = [xc+t_col xc+b_col-t_col xc+b_col-t_col xc+t_col];   % interior
yi_v = [yc+t_col yc+t_col yc+h_col-t_col yc+h_col-t_col];

% Tapa SUPERIOR del tubo (anillo cuadrado) — usando 4 patches en lugar de polígono con hueco
for k = 1:4
    kn = mod(k,4)+1;
    patch([xo(k) xo(kn) xi_v(kn) xi_v(k)], ...
          [yo(k) yo(kn) yi_v(kn) yi_v(k)], ...
          [z1 z1 z1 z1], [.5 .6 .9], 'EdgeColor','b', 'LineWidth',1);
end
% Tapa INFERIOR del tubo (anillo en z=z0) — opcional, sólo borde para que se vea hueco
for k = 1:4
    kn = mod(k,4)+1;
    patch([xo(k) xo(kn) xi_v(kn) xi_v(k)], ...
          [yo(k) yo(kn) yi_v(kn) yi_v(k)], ...
          [z0 z0 z0 z0], [.55 .65 .95], 'EdgeColor','b', 'LineWidth',1);
end
% Caras exteriores del tubo (4 paredes verticales exteriores)
for k = 1:4
    kn = mod(k,4)+1;
    patch([xo(k) xo(kn) xo(kn) xo(k)], ...
          [yo(k) yo(kn) yo(kn) yo(k)], ...
          [z0 z0 z1 z1], [.5 .6 .95], 'EdgeColor','b', 'LineWidth',1.2);
end
% Caras interiores (paredes del hueco — apenas visibles desde fuera, pero válidas)
for k = 1:4
    kn = mod(k,4)+1;
    patch([xi_v(k) xi_v(kn) xi_v(kn) xi_v(k)], ...
          [yi_v(k) yi_v(kn) yi_v(kn) yi_v(k)], ...
          [z0 z0 z1 z1], [.7 .75 .98], 'EdgeColor','b', 'LineWidth',0.8);
end

title(sprintf('Placa base 3D — %d pernos M%d + HSS %d×%d×%d', ...
              nPernos, d_perno, b_col, h_col, t_col));
xlabel('x (mm)'); ylabel('y (mm)'); zlabel('z (mm)');
view(35, 30); axis equal;
light('Position',[1 1 1]); lighting gouraud; material dull;
grid on;
saveas(gcf,'demo_placa_base_figs/02_3D.png');

%% =====================================================================
%% FIGURA 3 — Mapa de tensiones σ_vm sobre la placa (FEM simulado)
%% =====================================================================
% Simulamos σ_vm: máximo cerca de cada perno + concentración bajo la columna
sigma_vm = zeros(size(xn));
% Carga axial 500 kN sobre columna → distribución por anclajes
P_total = 500e3;       % N
P_perno = P_total/nPernos;
for k = 1:nPernos
    r2 = (xn-xp(k)).^2 + (yn-yp(k)).^2;
    sigma_vm = sigma_vm + P_perno ./ (r2/100 + 50);  % Pa
end
% Base por la columna (compresión bajo el perfil)
in_col = (xn >= xc) & (xn <= xc+b_col) & (yn >= yc) & (yn <= yc+h_col);
sigma_vm = sigma_vm + 20e6 * in_col;  % +20 MPa bajo columna

figure('Position',[100 100 1000 700]); hold on;
patch('Faces', tri, 'Vertices', [xn yn], ...
      'FaceVertexCData', sigma_vm/1e6, 'FaceColor','interp', ...
      'EdgeColor','none');
% Contorno de orificios visible
for k = 1:nPernos
    th = linspace(0, 2*pi, 30);
    plot(xp(k) + d_orif/2*cos(th), yp(k) + d_orif/2*sin(th), 'k-', 'LineWidth',1);
end
% Contorno de columna
plot([xc xc+b_col xc+b_col xc xc], [yc yc yc+h_col yc+h_col yc], 'b-', 'LineWidth',2);

colormap(jet); cb = colorbar; cb.Label.String = '\sigma_{vm} (MPa)';
title('Mapa \sigma_{vm} sobre placa base (simulado FEM)');
xlabel('x (mm)'); ylabel('y (mm)'); axis equal tight;
grid on; box on;
saveas(gcf,'demo_placa_base_figs/03_sigma_vm.png');

disp('Done');
exit;
