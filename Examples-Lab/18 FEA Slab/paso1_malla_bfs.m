%% PASO 1 — Malla del elemento BFS (Bogner-Fox-Schmit)
%-- Solo geometria: coordenadas de joints, conectividad y apoyos.
%-- Equivale a los loops 1-4 del rectangular_slab_bfs.m completo.
%-- Referencia: Reddy, "Theory and Analysis of Elastic Plates and Shells",
%-- Cap. 12, Fig. 12.2.5 (elemento rectangular conforme, 16 GDL).

clear; clc; close all;

%% Datos de entrada
a = 6     % Dimension en x [m]
b = 4     % Dimension en y [m]

%% Malla: n_a x n_b elementos
n_a = 6   % elementos en a
n_b = 4   % elementos en b
n_e = n_a*n_b           % total elementos
n_j = (n_a+1)*(n_b+1)   % total joints
a_1 = a/n_a             % ancho elemento [m]
b_1 = b/n_b             % alto elemento [m]

%% LOOP 2 — Coordenadas de los joints (numeracion column-major)
%-- y sube primero; al pasar el borde, y se reinicia y x avanza.
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

%% LOOP 3 — Conectividad e_j(e, 1..4): nodos de cada elemento (CCW)
%-- Nodo 1=(0,0), 2=(1,0), 3=(1,1), 4=(0,1) en coords locales.
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

%% LOOP 4 — Joints apoyados (bordes, simply supported)
n_s = 2*(n_a + n_b);
s_j = zeros(n_s, 1);
i_s = 0;
for i = 1:n_a + 1               % borde inferior y=0
    i_s = i_s + 1;
    s_j(i_s) = (n_b + 1)*i - n_b;
end
for i = 1:n_a + 1               % borde superior y=b
    i_s = i_s + 1;
    s_j(i_s) = (n_b + 1)*i;
end
for i = 2:n_b                   % borde izquierdo x=0 (interior)
    i_s = i_s + 1;
    s_j(i_s) = i;
end
for i = 2:n_b                   % borde derecho x=a (interior)
    i_s = i_s + 1;
    s_j(i_s) = n_a*(n_b + 1) + i;
end

fprintf('Malla: %d elementos (%d x %d), %d joints, %d apoyos\n', ...
        n_e, n_a, n_b, n_j, n_s);

%% Verificacion — como matrices y vectores (limpio, no texto suelto)
% Tabla joint -> coordenadas: columnas [joint, x, y]
Coords_jxy = [(1:n_j)', x_j, y_j]
% Cuadricula fisica de coordenadas: fila = nivel y, columna = columna x
X_grid = reshape(x_j, n_b+1, n_a+1)
Y_grid = reshape(y_j, n_b+1, n_a+1)
% Conectividad: columnas [elem, nodo1, nodo2, nodo3, nodo4]
Conectividad = [(1:n_e)', e_j]
% Joints apoyados (vector columna)
Apoyos = s_j

%% Dibujo de la malla
figure('Position',[100 100 760 560]); hold on;
% Elementos como patches
for e = 1:n_e
    nodes = e_j(e, :);
    xq = x_j(nodes);
    yq = y_j(nodes);
    patch(xq, yq, [0.80 1.0 0.80], 'EdgeColor',[0.18 0.55 0.34], 'LineWidth',1.2);
    % Numero de elemento en su centro
    text(mean(xq), mean(yq), num2str(e), ...
         'HorizontalAlignment','center', 'Color',[0.18 0.55 0.34], 'FontSize',9);
end
% Nodos (circulos naranjas) + numeros — todo vectorizado, sin for
plot(x_j, y_j, 'o', 'MarkerFaceColor',[1 0.55 0], 'MarkerEdgeColor','k', 'MarkerSize',16);
text(x_j, y_j, (1:n_j)', 'Color','k', 'FontSize',8);
% Apoyos (triangulos rojos) — una sola llamada vectorizada
plot(x_j(s_j), y_j(s_j), '^', 'MarkerFaceColor','r', 'MarkerEdgeColor','r', 'MarkerSize',9);
axis equal; axis([-0.5 a+0.5 -0.5 b+0.5]);
xlabel('x [m]'); ylabel('y [m]');
title('Paso 1 — Malla BFS 6x4: joints (naranja), elementos (verde), apoyos (rojo)');
grid on;
saveas(gcf, 'paso1_malla_bfs.png');

fprintf('\n=== FIN Paso 1: malla generada y dibujada ===\n');
