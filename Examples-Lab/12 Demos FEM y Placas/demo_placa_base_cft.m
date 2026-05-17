%% Placa base con columna CFT (HSS + concreto) sobre pedestal de concreto
%% — Vista en planta, 3D completa, 3D en sección, mapa de tensiones
close all
mkdir('demo_cft_figs');

%% Cronómetro global y por sección
t_total = tic;
times = struct();

%% ==================================================================
%% PARÁMETROS GEOMÉTRICOS (todo en mm)
%% ==================================================================
% Placa de acero
Lx = 500;          % largo placa
Ly = 500;          % ancho placa
tp = 30;           % espesor placa
edge = 50;         % distancia perno → borde placa
nx = 3;            % nº pernos en x
ny = 3;            % nº pernos en y
% Pernos M20 — geometría completa para verificación de constructibilidad
d_perno = 20;      % M20 (diámetro nominal)
d_orif = 22;       % φ orificio (perno + holgura)
d_tuerca = 37;     % tuerca hexagonal M20: across-corners ≈ 37 mm
d_herram = 60;     % φ herramienta de impacto: 60 mm (llave neumática)
%                    (alternativas: llave de tubo 45, llave fija 50)
L_anc = 350;       % longitud anclaje hacia abajo (gancho recto)
% Columna CFT (HSS exterior + concreto interior, sección cuadrada)
b_col = 300;       % lado HSS
h_col = 300;
t_col = 8;         % espesor pared HSS
H_col = 400;       % altura visible de columna
% Pedestal de concreto (mismo footprint que la placa)
b_ped = 500;       % largo pedestal
h_ped = 500;       % ancho pedestal
H_ped = 500;       % altura pedestal
% Colores
col_acero  = [.35 .42 .55];
col_concH  = [.78 .72 .60];    % concreto del CFT
col_concP  = [.82 .80 .72];    % concreto del pedestal
col_perno  = [.18 .18 .18];

%% Posiciones pernos (perimetrales)
t_section = tic;
xb = linspace(edge, Lx-edge, nx);
yb = linspace(edge, Ly-edge, ny);
[XB, YB] = meshgrid(xb, yb);
is_perim = (XB == xb(1)) | (XB == xb(end)) | (YB == yb(1)) | (YB == yb(end));
xp = XB(is_perim); yp = YB(is_perim);
nPernos = numel(xp);

% Centro HSS
xc = Lx/2 - b_col/2;
yc = Ly/2 - h_col/2;
% Holgura HSS↔perno
g_x = xc - xp(1);
g_y = yc - yp(1);
fprintf('Pernos perimetrales: %d  |  g_x = %.0f mm, g_y = %.0f mm\n', nPernos, g_x, g_y);

%% ==================================================================
%% DETECCIÓN DE CHOQUES (constructibilidad)
%% Para cada perno, verificar que:
%%   (a) la tuerca + espacio de herramienta no choquen con el borde
%%   (b) la tuerca + espacio de herramienta no choquen con el HSS
%%   (c) la tuerca + herramienta no choquen con otros pernos
%% ==================================================================
r_h = d_herram/2;    % radio del espacio libre para herramienta
choques = repmat({''}, nPernos, 1);
ok = true(nPernos, 1);
for k = 1:nPernos
    msgs = {};
    % (a) Borde de placa
    if xp(k) - r_h < 0
        msgs{end+1} = sprintf('borde izq (%.0f<%.0f)', xp(k), r_h);
    end
    if xp(k) + r_h > Lx
        msgs{end+1} = sprintf('borde der (%.0f>%.0f)', xp(k)+r_h, Lx);
    end
    if yp(k) - r_h < 0
        msgs{end+1} = sprintf('borde inf');
    end
    if yp(k) + r_h > Ly
        msgs{end+1} = sprintf('borde sup');
    end
    % (b) Cara del HSS (rectángulo [xc, xc+b_col] × [yc, yc+h_col])
    % Distancia mínima del centro perno al rectángulo HSS
    dx_HSS = max(max(xc - xp(k), xp(k) - (xc + b_col)), 0);
    dy_HSS = max(max(yc - yp(k), yp(k) - (yc + h_col)), 0);
    d_to_HSS = sqrt(dx_HSS^2 + dy_HSS^2);
    if d_to_HSS < r_h
        msgs{end+1} = sprintf('HSS (d=%.0f<%.0f)', d_to_HSS, r_h);
    end
    % (c) Otros pernos
    for j = 1:nPernos
        if j == k, continue; end
        d_jk = sqrt((xp(k)-xp(j))^2 + (yp(k)-yp(j))^2);
        if d_jk < d_herram     % herramientas se interfieren
            msgs{end+1} = sprintf('perno B%d (d=%.0f<%.0f)', j, d_jk, d_herram);
        end
    end
    if ~isempty(msgs)
        choques{k} = strjoin(msgs, '; ');
        ok(k) = false;
    end
end
% Reporte
fprintf('\n══ Verificación de constructibilidad (φ herramienta = %d mm) ══\n', d_herram);
nOk = sum(ok); nFail = sum(~ok);
fprintf('  Pernos OK:   %d / %d\n', nOk, nPernos);
fprintf('  Conflictos:  %d / %d\n', nFail, nPernos);
if nFail > 0
    for k = 1:nPernos
        if ~ok(k)
            fprintf('   ⚠ B%d (x=%.0f, y=%.0f): %s\n', k, xp(k), yp(k), choques{k});
        end
    end
end
fprintf('\n');
times.geometria_y_checks = toc(t_section);

%% ==================================================================
%% FIGURA 1 — Planta (sin choques) con CFT
%% ==================================================================
t_section = tic;
figure('Position',[100 100 1000 950]); hold on; axis equal;

% Placa base
patch([0 Lx Lx 0], [0 0 Ly Ly], col_acero, 'EdgeColor','k', 'LineWidth',1.5, 'FaceAlpha',0.85);

% CFT: tubo exterior + concreto interior
% Exterior (anillo de acero)
patch([xc xc+b_col xc+b_col xc], [yc yc yc+h_col yc+h_col], col_acero*0.7, ...
      'EdgeColor','b', 'LineWidth',2);
% Interior (concreto del CFT)
xi = xc + t_col; yi = yc + t_col;
bi = b_col - 2*t_col; hi = h_col - 2*t_col;
patch([xi xi+bi xi+bi xi], [yi yi yi+hi yi+hi], col_concH, ...
      'EdgeColor','b', 'LineWidth',1.5);
% Hatching diagonal sobre el concreto para indicar material
nH = 20;
for k = 0:nH
    xH = [xi + k*bi/nH, xi + (k+1)*bi/nH];
    yH = [yi, yi+hi];
    if xH(2) > xi+bi, xH(2) = xi+bi; yH(2) = yi + (xi+bi - xH(1))*(hi/(bi/nH)); end
    plot(xH, yH, '-', 'Color',[.5 .4 .3], 'LineWidth',0.3);
end
% Etiqueta CFT
text(Lx/2, Ly/2, sprintf('CFT\nHSS %d×%d×%d\n+ concreto', b_col, b_col, t_col), ...
     'HorizontalAlignment','center', 'FontSize',10, 'FontWeight','bold', 'Color','k');

% Capas concéntricas por cada perno:
%   1. círculo herramienta (amarillo o rojo si choca)
%   2. tuerca hexagonal
%   3. orificio
%   4. cruz de centro + etiqueta
theta = linspace(0, 2*pi, 40);
for k = 1:nPernos
    % (1) Espacio para herramienta — círculo grande
    col_h = ok(k) * [0.95 0.90 0.55] + ~ok(k) * [1 0.45 0.45];   % verde claro vs rojo
    edge_h = ok(k) * [0.6 0.6 0.2] + ~ok(k) * [.8 0 0];
    patch(xp(k) + r_h*cos(theta), yp(k) + r_h*sin(theta), col_h, ...
          'EdgeColor', edge_h, 'LineStyle','--', 'LineWidth', 1, 'FaceAlpha', 0.45);
    % (2) Tuerca hexagonal (across-corners = d_tuerca)
    rt = d_tuerca/2;
    th_h = (0:5)*pi/3 + pi/6;
    patch(xp(k) + rt*cos(th_h), yp(k) + rt*sin(th_h), [.55 .55 .58], ...
          'EdgeColor','k', 'LineWidth',1);
    % (3) Orificio (blanco)
    rorif = d_orif/2;
    patch(xp(k) + rorif*cos(theta), yp(k) + rorif*sin(theta), 'w', ...
          'EdgeColor','k', 'LineWidth',1);
    % (4) Cruz centro
    line([xp(k)-rorif*1.3 xp(k)+rorif*1.3], [yp(k) yp(k)], 'Color','k', 'LineWidth',0.5);
    line([xp(k) xp(k)], [yp(k)-rorif*1.3 yp(k)+rorif*1.3], 'Color','k', 'LineWidth',0.5);
end
% Etiquetas (después para que queden encima)
for k = 1:nPernos
    if ok(k)
        text(xp(k), yp(k)+r_h+8, sprintf('B%d', k), ...
             'HorizontalAlignment','center', 'FontSize',9, ...
             'Color','k', 'FontWeight','bold');
    else
        text(xp(k), yp(k)+r_h+8, sprintf('B%d ⚠', k), ...
             'HorizontalAlignment','center', 'FontSize',9, ...
             'Color','r', 'FontWeight','bold');
    end
end

% (Leyenda se crea al final para que no agarre cotas/líneas dibujadas después)

% Cotas exteriores
line([0 Lx], [-30 -30], 'Color','k');
line([0 0], [-25 -35], 'Color','k'); line([Lx Lx], [-25 -35], 'Color','k');
text(Lx/2, -45, sprintf('L_x = %d mm', Lx), 'HorizontalAlignment','center', 'FontSize',11);
line([-30 -30], [0 Ly], 'Color','k');
line([-25 -35], [0 0], 'Color','k'); line([-25 -35], [Ly Ly], 'Color','k');
text(-55, Ly/2, sprintf('L_y = %d mm', Ly), 'HorizontalAlignment','center', ...
     'Rotation',90, 'FontSize',11);

% Cota del HSS (verde)
line([xc xc+b_col], [yc+h_col+18 yc+h_col+18], 'Color',[0 .5 0], 'LineWidth',1.2);
line([xc xc], [yc+h_col+13 yc+h_col+23], 'Color',[0 .5 0]);
line([xc+b_col xc+b_col], [yc+h_col+13 yc+h_col+23], 'Color',[0 .5 0]);
text(xc+b_col/2, yc+h_col+32, sprintf('HSS = %d mm', b_col), ...
     'HorizontalAlignment','center', 'FontSize',10, 'Color',[0 .5 0]);

% Holgura g_x (naranja) — claro y bien posicionado
y_cota_g = yp(1) - 25;
line([xp(1) xc], [y_cota_g y_cota_g], 'Color',[.85 .3 0], 'LineWidth',1.4);
line([xp(1) xp(1)], [y_cota_g-4 y_cota_g+4], 'Color',[.85 .3 0]);
line([xc xc], [y_cota_g-4 y_cota_g+4], 'Color',[.85 .3 0]);
text((xp(1)+xc)/2, y_cota_g-14, sprintf('g_x = %.0f mm', g_x), ...
     'HorizontalAlignment','center', 'FontSize',10, 'Color',[.85 .3 0], 'FontWeight','bold');

% Holgura g_y (naranja vertical)
x_cota_g = xp(1) - 25;
line([x_cota_g x_cota_g], [yp(1) yc], 'Color',[.85 .3 0], 'LineWidth',1.4);
line([x_cota_g-4 x_cota_g+4], [yp(1) yp(1)], 'Color',[.85 .3 0]);
line([x_cota_g-4 x_cota_g+4], [yc yc], 'Color',[.85 .3 0]);
text(x_cota_g-15, (yp(1)+yc)/2, sprintf('g_y = %.0f mm', g_y), ...
     'HorizontalAlignment','center', 'Rotation',90, 'FontSize',10, ...
     'Color',[.85 .3 0], 'FontWeight','bold');

% Cota espesor HSS (magenta, detalle)
xtt = xc + b_col + 8;
line([xtt xtt+15], [yc+h_col/2 yc+h_col/2-25], 'Color','m', 'LineWidth',1);
text(xtt+18, yc+h_col/2-30, sprintf('t_{HSS} = %d mm', t_col), ...
     'FontSize',9, 'Color','m');

title(sprintf('Planta — Placa %d×%d mm | CFT %d×%d×%d | %d pernos M%d', ...
              Lx, Ly, b_col, b_col, t_col, nPernos, d_perno), 'FontSize',12);
xlabel('x (mm)'); ylabel('y (mm)');
xlim([-90 Lx+260]); ylim([-90 Ly+90]);   % +260 a la derecha para hacer espacio a la leyenda
grid on; box on;

% Leyenda al FINAL (con handles explícitos para evitar capturar cotas)
hL1 = patch(NaN, NaN, [0.95 0.90 0.55], 'EdgeColor',[0.6 0.6 0.2], ...
            'LineStyle','--', 'FaceAlpha',0.45);
hL2 = patch(NaN, NaN, [.55 .55 .58], 'EdgeColor','k');
hL3 = patch(NaN, NaN, 'w', 'EdgeColor','k');
hL4 = patch(NaN, NaN, [1 0.45 0.45], 'EdgeColor',[.8 0 0], ...
            'LineStyle','--', 'FaceAlpha',0.45);
leg = legend([hL1 hL2 hL3 hL4], ...
       {sprintf('Espacio herram. φ%d mm', d_herram), ...
        sprintf('Tuerca M%d (φ%d)', d_perno, d_tuerca), ...
        sprintf('Orificio φ%d', d_orif), ...
        'CHOQUE detectado'}, ...
       'Location','northeast', 'FontSize',10);

saveas(gcf,'demo_cft_figs/01_planta.png');
times.fig1_planta = toc(t_section);

%% ==================================================================
%% FIGURA 2 — 3D VISTA COMPLETA del ensamble
%% ==================================================================
t_section = tic;
t_mesh = tic;
% Genero malla T3 que respeta los orificios (para la placa)
[xg, yg] = meshgrid(linspace(0,Lx,40), linspace(0,Ly,40));
xn = xg(:); yn = yg(:);
keep = true(size(xn));
for k = 1:nPernos
    keep = keep & ((xn-xp(k)).^2 + (yn-yp(k)).^2 >= (d_orif/2)^2);
end
xn = xn(keep); yn = yn(keep);
n_arc = 16;
for k = 1:nPernos
    th = linspace(0, 2*pi, n_arc+1); th(end) = [];
    xn = [xn; xp(k) + d_orif/2 * cos(th)'];
    yn = [yn; yp(k) + d_orif/2 * sin(th)'];
end
tri = delaunay(xn, yn);
cx = mean(xn(tri),2); cy = mean(yn(tri),2);
keep_tri = true(size(cx));
for k = 1:nPernos
    keep_tri = keep_tri & ((cx-xp(k)).^2 + (cy-yp(k)).^2 >= (d_orif/2 * 0.95)^2);
end
tri = tri(keep_tri,:);
times.mallado_T3 = toc(t_mesh);

figure('Position',[100 100 1100 800]); hold on;
% --- Pedestal de concreto ---
xp_ped = Lx/2 - b_ped/2; yp_ped = Ly/2 - h_ped/2;
zP_top = 0; zP_bot = -H_ped;
% 6 caras del paralelepípedo pedestal
draw_box(xp_ped, yp_ped, zP_bot, b_ped, h_ped, H_ped, col_concP, [0.6 0.5 0.4], 0.9);
text(xp_ped+b_ped*0.85, yp_ped, zP_bot+10, 'Pedestal HA', ...
     'FontSize',10, 'Color',[.5 .35 .15]);
% --- Placa de acero ---
z0 = 0; z1 = tp;
patch('Faces', tri, 'Vertices', [xn yn z1*ones(numel(xn),1)], ...
      'FaceColor',col_acero, 'EdgeColor',[.2 .2 .3], 'LineWidth',0.2);
patch('Faces', tri, 'Vertices', [xn yn z0*ones(numel(xn),1)], ...
      'FaceColor',col_acero*0.85, 'EdgeColor','none');
% Bordes laterales placa
for side = 1:4
    switch side
        case 1, p1 = [0 0]; p2 = [Lx 0];
        case 2, p1 = [Lx 0]; p2 = [Lx Ly];
        case 3, p1 = [Lx Ly]; p2 = [0 Ly];
        case 4, p1 = [0 Ly]; p2 = [0 0];
    end
    patch([p1(1) p2(1) p2(1) p1(1)], [p1(2) p2(2) p2(2) p1(2)], [z0 z0 z1 z1], ...
          col_acero, 'EdgeColor','k', 'LineWidth',0.8);
end
% --- Pernos de anclaje (cuerpo cilíndrico) ---
for k = 1:nPernos
    z_perno_top = z1 + 25;       % cabeza sobresale
    z_perno_bot = -L_anc;        % gancho dentro del pedestal
    draw_cylinder(xp(k), yp(k), z_perno_bot, z_perno_top, d_perno/2, col_perno, 16);
    % Tuerca hexagonal arriba
    nut_r = d_perno*0.85;
    th_n = linspace(0, 2*pi, 7); th_n(end) = [];
    patch(xp(k)+nut_r*cos(th_n), yp(k)+nut_r*sin(th_n), ...
          (z_perno_top)*ones(1,6), [.5 .5 .55], 'EdgeColor','k', 'LineWidth',0.5);
    % Placa de anclaje al final del perno
    p_anc = 60;
    patch(xp(k)+[-p_anc/2 p_anc/2 p_anc/2 -p_anc/2], ...
          yp(k)+[-p_anc/2 -p_anc/2 p_anc/2 p_anc/2], ...
          (z_perno_bot)*ones(1,4), [.25 .25 .3], 'EdgeColor','k', 'LineWidth',0.5);
end
% --- Columna CFT ---
% HSS exterior (4 caras + tapa hueca)
zC_bot = z1; zC_top = z1 + H_col;
draw_hollow_box(xc, yc, zC_bot, b_col, h_col, H_col, t_col, col_acero, col_concH);

% Cotas/etiquetas
text(Lx/2, yp_ped-30, zP_bot, 'PEDESTAL HORMIGÓN', ...
     'HorizontalAlignment','center', 'FontSize',10, 'Color',[.4 .3 .2], 'FontWeight','bold');

title(sprintf('Conjunto 3D — CFT %d×%d×%d sobre placa %d×%d sobre pedestal HA', ...
              b_col, b_col, t_col, Lx, Ly), 'FontSize',12);
xlabel('x (mm)'); ylabel('y (mm)'); zlabel('z (mm)');
view(35, 25); axis equal;
camlight('headlight'); lighting gouraud; material dull;
grid on;
saveas(gcf,'demo_cft_figs/02_3D_completa.png');
times.fig2_3D_completa = toc(t_section);

%% ==================================================================
%% FIGURA 3 — 3D EN SECCIÓN (cortada por y=Ly/2) — muestra el interior
%% ==================================================================
t_section = tic;
figure('Position',[100 100 1200 800]); hold on;

% Plano de corte: y = Ly/2 → dibujo solo y > Ly/2 (mitad trasera) para ver el corte
yCut = Ly/2;

% --- Pedestal cortado ---
draw_box(xp_ped, yCut, zP_bot, b_ped, h_ped/2, H_ped, col_concP, [0.6 0.5 0.4], 0.9);
% Cara de corte (frontal) del pedestal
patch([xp_ped xp_ped+b_ped xp_ped+b_ped xp_ped], ...
      [yCut yCut yCut yCut], ...
      [zP_bot zP_bot zP_top zP_top], ...
      col_concP*0.85, 'EdgeColor','k', 'LineWidth',1.5);
% Hatching sobre la cara de corte (textura concreto)
for k = 1:15
    yk = yCut + 0.01;   % apenas adelante para visibilidad
    xH = xp_ped + (k/15)*b_ped;
    plot3([xH xH-30], [yk yk], [zP_bot zP_top-(k*30)], 'Color',[.5 .4 .3], 'LineWidth',0.4);
end

% --- Placa de acero (mitad) ---
keep_y = yn >= yCut;
% Para placa cortada: tomamos los nodos del lado y>=yCut, malla y patches
trip = tri;   % usamos malla completa pero filtramos por mediana del triángulo
mid_y_tri = mean(yn(trip),2);
trip = trip(mid_y_tri >= yCut, :);
patch('Faces', trip, 'Vertices', [xn yn z1*ones(numel(xn),1)], ...
      'FaceColor',col_acero, 'EdgeColor',[.2 .2 .3], 'LineWidth',0.2);
patch('Faces', trip, 'Vertices', [xn yn z0*ones(numel(xn),1)], ...
      'FaceColor',col_acero*0.85, 'EdgeColor','none');
% Caras laterales solo las que dan al lado y>=yCut
for side = 1:4
    switch side
        case 1, p1 = [0 0]; p2 = [Lx 0];
        case 2, p1 = [Lx 0]; p2 = [Lx Ly];
        case 3, p1 = [Lx Ly]; p2 = [0 Ly];
        case 4, p1 = [0 Ly]; p2 = [0 0];
    end
    if min(p1(2),p2(2)) >= yCut - 0.1
        patch([p1(1) p2(1) p2(1) p1(1)], [p1(2) p2(2) p2(2) p1(2)], [z0 z0 z1 z1], ...
              col_acero, 'EdgeColor','k', 'LineWidth',0.8);
    end
end
% Cara de corte de la placa
patch([0 Lx Lx 0], [yCut yCut yCut yCut], [z0 z0 z1 z1], ...
      col_acero*0.6, 'EdgeColor','k', 'LineWidth',1.5);

% --- Pernos de anclaje (todos visibles para mostrar el detalle) ---
for k = 1:nPernos
    z_perno_top = z1 + 25;
    z_perno_bot = -L_anc;
    if yp(k) >= yCut - 50   % muestra todos los del lado visible y los justo en el corte
        draw_cylinder(xp(k), yp(k), z_perno_bot, z_perno_top, d_perno/2, col_perno, 16);
        nut_r = d_perno*0.85;
        th_n = linspace(0, 2*pi, 7); th_n(end) = [];
        patch(xp(k)+nut_r*cos(th_n), yp(k)+nut_r*sin(th_n), ...
              (z_perno_top)*ones(1,6), [.5 .5 .55], 'EdgeColor','k', 'LineWidth',0.5);
        patch(xp(k)+[-30 30 30 -30], yp(k)+[-30 -30 30 30], ...
              (z_perno_bot)*ones(1,4), [.25 .25 .3], 'EdgeColor','k');
    end
end

% --- Columna CFT (mitad, mostrando relleno) ---
% HSS exterior — solo cara trasera y dos laterales
% Cara trasera del HSS
patch([xc xc+b_col xc+b_col xc], [yc+h_col yc+h_col yc+h_col yc+h_col], ...
      [zC_bot zC_bot zC_top zC_top], col_acero, 'EdgeColor','b', 'LineWidth',1);
% Caras laterales
patch([xc xc xc xc], [yCut yc+h_col yc+h_col yCut], ...
      [zC_bot zC_bot zC_top zC_top], col_acero, 'EdgeColor','b', 'LineWidth',1);
patch([xc+b_col xc+b_col xc+b_col xc+b_col], [yCut yc+h_col yc+h_col yCut], ...
      [zC_bot zC_bot zC_top zC_top], col_acero, 'EdgeColor','b', 'LineWidth',1);
% Tapa superior del HSS (anillo trasero)
patch([xc xc+b_col xc+b_col xc], [yc+h_col yc+h_col yCut yCut], ...
      zC_top*ones(1,4), col_acero, 'EdgeColor','b', 'LineWidth',0.8);

% Cara CORTADA del HSS (cara frontal, mostrando espesor del acero)
% Acero anillo
patch([xc xc+t_col xc+t_col xc], [yCut yCut yCut yCut], ...
      [zC_bot zC_bot zC_top zC_top], col_acero*0.6, 'EdgeColor','k', 'LineWidth',1.2);  % pared izq
patch([xc+b_col-t_col xc+b_col xc+b_col xc+b_col-t_col], [yCut yCut yCut yCut], ...
      [zC_bot zC_bot zC_top zC_top], col_acero*0.6, 'EdgeColor','k', 'LineWidth',1.2);  % pared der
patch([xc xc+b_col xc+b_col xc], [yCut yCut yCut yCut], ...
      [zC_top-t_col zC_top-t_col zC_top zC_top], col_acero*0.6, 'EdgeColor','k', 'LineWidth',1.2);  % top
% Concreto del CFT (interior cortado) — capa "layered"
xi_ = xc + t_col; bi_ = b_col - 2*t_col;
patch([xi_ xi_+bi_ xi_+bi_ xi_], [yCut yCut yCut yCut], ...
      [zC_bot zC_bot zC_top-t_col zC_top-t_col], col_concH, 'EdgeColor',[.5 .4 .3], 'LineWidth',1);
% Hatching del concreto del CFT
for k = 1:12
    yk = yCut + 0.02;
    xH = xi_ + (k/12)*bi_;
    plot3([xH xH-25], [yk yk], [zC_bot zC_top-t_col-(k*25)], 'Color',[.5 .4 .3], 'LineWidth',0.4);
end

% Etiquetas claras
text(xc+b_col/2, yCut, zC_top+40, 'CFT', 'HorizontalAlignment','center', ...
     'FontSize',13, 'FontWeight','bold', 'Color','k');
text(xc-15, yCut, zC_top*0.6, 'HSS', 'HorizontalAlignment','right', ...
     'FontSize',10, 'Color',[.2 .3 .5], 'FontWeight','bold');
text(xc+b_col/2, yCut, zC_top*0.5, 'Hormigón\nrelleno', ...
     'HorizontalAlignment','center', 'FontSize',9, 'Color',[.4 .3 .15]);
text(xp_ped+b_ped*0.85, yCut, -H_ped/2, 'Pedestal\nHA', ...
     'HorizontalAlignment','right', 'FontSize',11, 'Color',[.4 .3 .2], 'FontWeight','bold');
text(xp(1)+50, yCut, -L_anc+30, 'Anclajes\nembebidos', ...
     'HorizontalAlignment','left', 'FontSize',9, 'Color',[.2 .2 .2]);
% Línea indicando arranque hormigón-hormigón
plot3([xi_ xi_+bi_], [yCut yCut], [zC_bot zC_bot], 'r--', 'LineWidth',2);
text(xi_+bi_/2, yCut, zC_bot-20, 'arranque HA-HA', ...
     'HorizontalAlignment','center', 'FontSize',9, 'Color','r', 'FontWeight','bold');

title('Vista en SECCIÓN (y = L_y/2) — CFT + placa + anclajes + pedestal', 'FontSize',12);
xlabel('x (mm)'); ylabel('y (mm)'); zlabel('z (mm)');
view(0, 5); axis equal;
camlight('headlight'); lighting gouraud;
grid on;
saveas(gcf,'demo_cft_figs/03_3D_seccion.png');
times.fig3_3D_seccion = toc(t_section);

%% ==================================================================
%% FIGURA 4 — Mapa σ_vm con concentraciones EN cada perno + bajo el HSS
%% ==================================================================
t_section = tic;
t_calc = tic;
% Carga axial CFT sobre placa = 1500 kN, momento My = 80 kN·m
P_axial = 1500e3;
My = 80e3;
% Distribución bajo HSS: presión de contacto uniforme + componente lineal por momento
A_HSS_int = bi*hi;
sigma_contact_promedio = P_axial / A_HSS_int;
% Reacción en cada perno por tracción si momento existe
% Modelo simplificado: pernos del lado tracción reciben T_perno
T_perno_max = My / (Lx * 0.4);

sigma_vm = zeros(size(xn));
% (1) Presión bajo el HSS (compresión)
in_HSS = (xn >= xi_) & (xn <= xi_+bi_) & (yn >= yi) & (yn <= yi+hi);
sigma_vm = sigma_vm + sigma_contact_promedio/1e6 * in_HSS;
% Gradiente lineal por momento (más compresión en x grande)
sigma_vm = sigma_vm + 15 * (xn - Lx/2)/Lx .* in_HSS;
% (2) Concentración alrededor de cada perno (tracción → halo radial)
for k = 1:nPernos
    r = sqrt((xn-xp(k)).^2 + (yn-yp(k)).^2);
    intensidad = 100 / nPernos;  % MPa proporcional a la fuerza por perno
    if xp(k) > Lx/2, intensidad = intensidad * 1.5; end  % lado tracción si momento
    sigma_local = intensidad * exp(-(r/40).^2);
    sigma_vm = sigma_vm + sigma_local;
end
times.calculo_sigma_vm = toc(t_calc);

figure('Position',[100 100 1100 750]); hold on;
patch('Faces', tri, 'Vertices', [xn yn], ...
      'FaceVertexCData', sigma_vm, 'FaceColor','interp', 'EdgeColor','none');
% Contornos
for k = 1:nPernos
    plot(xp(k) + d_orif/2*cos(theta), yp(k) + d_orif/2*sin(theta), 'k-', 'LineWidth',1.2);
    text(xp(k), yp(k)-22, sprintf('B%d', k), 'HorizontalAlignment','center', ...
         'FontSize',8, 'Color','w', 'FontWeight','bold');
end
% Contorno HSS exterior e interior
plot([xc xc+b_col xc+b_col xc xc], [yc yc yc+h_col yc+h_col yc], ...
     'k-', 'LineWidth',2);
plot([xi_ xi_+bi xi_+bi xi_ xi_], [yi yi yi+hi yi+hi yi], ...
     'k--', 'LineWidth',1.2);

colormap(jet); cb = colorbar; cb.Label.String = '\sigma_{vm} (MPa)';
title(sprintf('Mapa σ_{vm} — Carga axial %.0f kN + momento %.0f kN·m sobre CFT', ...
              P_axial/1e3, My/1e3), 'FontSize',12);
xlabel('x (mm)'); ylabel('y (mm)'); axis equal tight;
grid on; box on;
saveas(gcf,'demo_cft_figs/04_sigma_vm.png');
times.fig4_sigma_vm = toc(t_section);

%% ==================================================================
%% REPORTE DE TIEMPOS
%% ==================================================================
times.TOTAL = toc(t_total);
fprintf('\n══════════════════════════════════════════════════════════\n');
fprintf('  REPORTE DE TIEMPOS DE CÁLCULO\n');
fprintf('══════════════════════════════════════════════════════════\n');
fprintf('  Geometría + check choques     : %7.3f s\n', times.geometria_y_checks);
fprintf('  Fig 1 — Planta acotada        : %7.3f s\n', times.fig1_planta);
fprintf('    └ Mallado T3 (Delaunay)     : %7.3f s\n', times.mallado_T3);
fprintf('  Fig 2 — 3D ensamble completo  : %7.3f s\n', times.fig2_3D_completa);
fprintf('  Fig 3 — 3D vista en sección   : %7.3f s\n', times.fig3_3D_seccion);
fprintf('    └ Cálculo σ_vm              : %7.3f s\n', times.calculo_sigma_vm);
fprintf('  Fig 4 — Mapa σ_vm             : %7.3f s\n', times.fig4_sigma_vm);
fprintf('──────────────────────────────────────────────────────────\n');
fprintf('  TOTAL                         : %7.3f s\n', times.TOTAL);
fprintf('══════════════════════════════════════════════════════════\n');
% Datos de modelo para contexto
fprintf('  Modelo: %d nodos, %d triángulos, %d pernos\n', ...
        numel(xn), size(tri,1), nPernos);
fprintf('══════════════════════════════════════════════════════════\n\n');

disp('Done');
exit;

%% ==================================================================
%% Helper functions
%% ==================================================================
function draw_box(x0, y0, z0, bx, by, bz, col_face, col_edge, alpha)
    % Caja sólida 6 caras
    X = [x0 x0+bx x0+bx x0; x0 x0+bx x0+bx x0];
    Y = [y0 y0 y0+by y0+by; y0 y0 y0+by y0+by];
    Z = [z0 z0 z0 z0; z0+bz z0+bz z0+bz z0+bz];
    % bottom + top
    patch('XData',[x0 x0+bx x0+bx x0]','YData',[y0 y0 y0+by y0+by]', ...
          'ZData',[z0 z0 z0 z0]','FaceColor',col_face*0.8,'EdgeColor',col_edge,'FaceAlpha',alpha);
    patch('XData',[x0 x0+bx x0+bx x0]','YData',[y0 y0 y0+by y0+by]', ...
          'ZData',[z0+bz z0+bz z0+bz z0+bz]','FaceColor',col_face,'EdgeColor',col_edge,'FaceAlpha',alpha);
    % 4 lados
    sides = {[1 2; 1 2], [2 3; 2 3], [3 4; 3 4], [4 1; 4 1]};
    pts = [x0 y0; x0+bx y0; x0+bx y0+by; x0 y0+by];
    for s = 1:4
        i1 = s; i2 = mod(s,4)+1;
        patch([pts(i1,1) pts(i2,1) pts(i2,1) pts(i1,1)], ...
              [pts(i1,2) pts(i2,2) pts(i2,2) pts(i1,2)], ...
              [z0 z0 z0+bz z0+bz], col_face, 'EdgeColor',col_edge, ...
              'LineWidth',0.8, 'FaceAlpha',alpha);
    end
end

function draw_cylinder(x0, y0, z0, z1, r, col, n)
    th = linspace(0, 2*pi, n+1);
    for k = 1:n
        xc = [x0+r*cos(th(k)) x0+r*cos(th(k+1)) x0+r*cos(th(k+1)) x0+r*cos(th(k))];
        yc = [y0+r*sin(th(k)) y0+r*sin(th(k+1)) y0+r*sin(th(k+1)) y0+r*sin(th(k))];
        zc = [z0 z0 z1 z1];
        patch(xc, yc, zc, col, 'EdgeColor','none');
    end
end

function draw_hollow_box(x0, y0, z0, bx, by, bz, t, col_steel, col_core)
    xi = x0 + t; yi = y0 + t;
    bxi = bx - 2*t; byi = by - 2*t;
    % Núcleo de concreto (cubo macizo interior)
    draw_box(xi, yi, z0, bxi, byi, bz, col_core, [.5 .4 .3], 1);
    % Paredes acero exteriores
    pts_o = [x0 y0; x0+bx y0; x0+bx y0+by; x0 y0+by];
    for s = 1:4
        i1 = s; i2 = mod(s,4)+1;
        patch([pts_o(i1,1) pts_o(i2,1) pts_o(i2,1) pts_o(i1,1)], ...
              [pts_o(i1,2) pts_o(i2,2) pts_o(i2,2) pts_o(i1,2)], ...
              [z0 z0 z0+bz z0+bz], col_steel, 'EdgeColor','b', 'LineWidth',1);
    end
    % Tapa superior (anillo)
    pts_i = [xi yi; xi+bxi yi; xi+bxi yi+byi; xi yi+byi];
    for s = 1:4
        i1 = s; i2 = mod(s,4)+1;
        patch([pts_o(i1,1) pts_o(i2,1) pts_i(i2,1) pts_i(i1,1)], ...
              [pts_o(i1,2) pts_o(i2,2) pts_i(i2,2) pts_i(i1,2)], ...
              (z0+bz)*ones(1,4), col_steel*0.9, 'EdgeColor','b', 'LineWidth',0.8);
    end
end
