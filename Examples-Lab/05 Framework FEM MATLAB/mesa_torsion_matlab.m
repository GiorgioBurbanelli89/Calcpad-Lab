% =============================================================================
%  MESA TORSION - SOLVER FEM COMPLETO EN MATLAB R2017a
% =============================================================================
%  Reproduce el modelo Mesa Torsion de hekatan-fem-py (variant Shell-Thin, SCP).
%  Solver auto-contenido:
%     - Shell Kirchhoff MZC (12x12 bending, Q4 rectangular axis-aligned)
%     - Membrane Q4 plane stress (8x8, 2x2 Gauss)
%     - Drilling weak diagonal (4x4)
%     - Frame Bernoulli-Euler 6DOF (12x12) + transformacion 3D Z-up
%
%  Geometria: 6x6 m losa t=0.10 m, columnas 40x40 cm H=4 m,
%             4 vigas perimetrales V30x50, mesh slab 5x5.
%  Material: E=24.85 GPa, nu=0.20 (concreto fc'=210 kgf/cm2).
%  Carga: q_SCP = 9.80665 kN/m2 (1 tonf/m2) uniforme.
%  Apoyos: 4 bases empotradas.
%
%  Compatible MATLAB R2017a + Octave-CLI (graphics_toolkit gnuplot).
%  Reporta: Mxx, Myy, Mxy, Mmax, Mmin, Fxx, Fyy, V13, V23, Vmax, vonMises
%           + N, V2, V3, T, M2, M3 por frame.
% =============================================================================

function mesa_torsion_matlab()
% Main entry: ejecuta solver FEM completo y reporta resultados.
% Wrapped en function para que las local functions sean visibles en Octave + R2017a.

if exist('OCTAVE_VERSION', 'builtin')
    graphics_toolkit('gnuplot');
end

close all;

%% ─── PARAMETROS ──────────────────────────────────────────────────
Lx = 6.0; Ly = 6.0; H = 4.0;
t_slab = 0.10;
bC = 0.40; hC = 0.40;
bV = 0.30; hV = 0.50;
N_slab = 5;          % mesh slab N_slab x N_slab
E = 24.85e6;         % kPa (1e6 kPa = 1 GPa equivalent en unidades kN, m)
nu = 0.20;
G = E / (2 * (1 + nu));
q_load = 9.80665;    % kN/m2 (SCP)

% Saint-Venant J rectangular (Roark/Young = Timoshenko exacto)
% J = a*b^3*[1/3 - 0.21*r*(1-r^4/12)]; el 1/3 y el 0.21 son terminos SEPARADOS.
sv_J = @(b, h) (1/3 - 0.21 * min(b,h)/max(b,h) * (1 - (min(b,h)/max(b,h))^4 / 12)) * max(b,h) * min(b,h)^3;
Jc = 0.141 * bC^4;
Jv = sv_J(bV, hV);

%% ─── MESH ────────────────────────────────────────────────────────
N = N_slab; nPS = N + 1; dx = Lx/N; dy = Ly/N;

% 4 bases (empotradas) en z=0
nodes = [0,  0,  0;
         Lx, 0,  0;
         Lx, Ly, 0;
         0,  Ly, 0];
% Nodos losa en z=H
for j = 0:N
    for i = 0:N
        nodes = [nodes; i*dx, j*dy, H];
    end
end
n_nodes = size(nodes, 1);
fprintf('Mesa Torsion MATLAB: %d nodos\n', n_nodes);

% Helper indexing 1-based: losa empieza en nodo 5
ix_slab = @(i, j) 4 + 1 + j*nPS + i;   % returns 1-indexed node number

elements = {};   % cell array de elementos
elem_type = {};  % 'shell' o 'frame'
elem_props = {}; % struct con E, nu, t, A, Iy, Iz, J

shell_count = 0;
for j = 0:N-1
    for i = 0:N-1
        elements{end+1} = [ix_slab(i,j), ix_slab(i+1,j), ix_slab(i+1,j+1), ix_slab(i,j+1)];
        elem_type{end+1} = 'shell';
        elem_props{end+1} = struct('E', E, 'nu', nu, 't', t_slab);
        shell_count = shell_count + 1;
    end
end

% 4 columnas (base -> esquina losa)
col_pairs = [1, ix_slab(0,0);
             2, ix_slab(N,0);
             3, ix_slab(N,N);
             4, ix_slab(0,N)];
col_props = struct('E', E, 'nu', nu, 'A', bC*hC, ...
                   'Iy', bC*hC^3/12, 'Iz', hC*bC^3/12, 'J', Jc);
for k = 1:4
    elements{end+1} = col_pairs(k, :);
    elem_type{end+1} = 'frame';
    elem_props{end+1} = col_props;
end

% 4 vigas perimetrales (S, E, N, W) — cada lado N segmentos
viga_props = struct('E', E, 'nu', nu, 'A', bV*hV, ...
                    'Iy', bV*hV^3/12, 'Iz', hV*bV^3/12, 'J', Jv);
for i = 0:N-1
    elements{end+1} = [ix_slab(i,0), ix_slab(i+1,0)];
    elem_type{end+1} = 'frame'; elem_props{end+1} = viga_props;
end
for j = 0:N-1
    elements{end+1} = [ix_slab(N,j), ix_slab(N,j+1)];
    elem_type{end+1} = 'frame'; elem_props{end+1} = viga_props;
end
for i = 0:N-1
    elements{end+1} = [ix_slab(i,N), ix_slab(i+1,N)];
    elem_type{end+1} = 'frame'; elem_props{end+1} = viga_props;
end
for j = 0:N-1
    elements{end+1} = [ix_slab(0,j), ix_slab(0,j+1)];
    elem_type{end+1} = 'frame'; elem_props{end+1} = viga_props;
end

n_elements = length(elements);
fprintf('Elementos: %d shells + %d frames = %d total\n', ...
        shell_count, n_elements - shell_count, n_elements);

%% ─── ENSAMBLE K GLOBAL ───────────────────────────────────────────
ndof = 6 * n_nodes;
% Usar I, J, V (triplets) para sparse efficient
I_list = []; J_list = []; V_list = [];

for e = 1:n_elements
    elm = elements{e};
    props = elem_props{e};
    if strcmp(elem_type{e}, 'shell')
        % Shell 24x24 = membrane(8) + bending(12) + drilling(4) en orden global
        x_e = nodes(elm, 1); y_e = nodes(elm, 2);
        Kb = kbend_mzc(x_e, y_e, props.E, props.nu, props.t);    % 12x12
        Km = kmembrane_q4(x_e, y_e, props.E, props.nu, props.t); % 8x8
        % Drilling weak: 4x4 (1 dof θz por nodo) con stiffness pequeño
        diag_b = max(abs(diag(Kb)));
        k_drill = 1e-3 * diag_b;
        Kd = k_drill * eye(4);
        % Mapping a global dof slots
        gdofs = zeros(1, 24);
        for k = 1:4
            n = elm(k);
            gdofs(6*k-5:6*k) = (6*n-5):(6*n);   % ux,uy,uz,tx,ty,tz
        end
        K24 = zeros(24, 24);
        % Membrane → DOFs (ux, uy) = slots 1, 2 por nodo
        for ni = 1:4
            for nj = 1:4
                K24(6*(ni-1)+1, 6*(nj-1)+1) = Km(2*(ni-1)+1, 2*(nj-1)+1);
                K24(6*(ni-1)+1, 6*(nj-1)+2) = Km(2*(ni-1)+1, 2*(nj-1)+2);
                K24(6*(ni-1)+2, 6*(nj-1)+1) = Km(2*(ni-1)+2, 2*(nj-1)+1);
                K24(6*(ni-1)+2, 6*(nj-1)+2) = Km(2*(ni-1)+2, 2*(nj-1)+2);
            end
        end
        % Bending → DOFs (uz, tx, ty) = slots 3, 4, 5 por nodo
        for ni = 1:4
            for nj = 1:4
                for di = 1:3
                    for dj = 1:3
                        K24(6*(ni-1)+2+di, 6*(nj-1)+2+dj) = ...
                            Kb(3*(ni-1)+di, 3*(nj-1)+dj);
                    end
                end
            end
        end
        % Drilling → DOF tz = slot 6 por nodo
        for ni = 1:4
            K24(6*ni, 6*ni) = K24(6*ni, 6*ni) + Kd(ni, ni);
        end
        [Ii, Jj] = ndgrid(gdofs, gdofs);
        I_list = [I_list; Ii(:)];
        J_list = [J_list; Jj(:)];
        V_list = [V_list; K24(:)];
    else
        % Frame
        n0 = nodes(elm(1), :); n1 = nodes(elm(2), :);
        K_loc = kframe_local(n0, n1, props);
        T = tframe(n0, n1);
        K_glob = T' * K_loc * T;
        gdofs = zeros(1, 12);
        gdofs(1:6)  = (6*elm(1)-5):(6*elm(1));
        gdofs(7:12) = (6*elm(2)-5):(6*elm(2));
        [Ii, Jj] = ndgrid(gdofs, gdofs);
        I_list = [I_list; Ii(:)];
        J_list = [J_list; Jj(:)];
        V_list = [V_list; K_glob(:)];
    end
end

K_global = sparse(I_list, J_list, V_list, ndof, ndof);

%% ─── BCs y CARGAS ────────────────────────────────────────────────
% Apoyos: nodos 1..4 totalmente empotrados (DOFs 1..24)
fixed_dofs = 1:24;
free_dofs = setdiff(1:ndof, fixed_dofs);

% Carga lumped trapezoidal en losa
F = zeros(ndof, 1);
for j = 0:N
    for i = 0:N
        xE = (i == 0 || i == N);
        yE = (j == 0 || j == N);
        f = q_load * dx * dy;
        if xE && yE
            f = f * 0.25;
        elseif xE || yE
            f = f * 0.5;
        end
        n = ix_slab(i, j);
        F(6*n - 3) = F(6*n - 3) - f;   % uz dof, sign hacia -Z
    end
end
fprintf('Carga total aplicada: %.3f kN (esperado: %.3f kN)\n', ...
        sum(F), -q_load * Lx * Ly);

%% ─── SOLVE ───────────────────────────────────────────────────────
u = zeros(ndof, 1);
fprintf('Solving K*u = F  (ndof_free=%d)...\n', length(free_dofs));
tic;
u(free_dofs) = K_global(free_dofs, free_dofs) \ F(free_dofs);
fprintf('Solved in %.2f s\n', toc);

%% ─── POST-PROCESS: shell resultants ──────────────────────────────
shell_res = zeros(shell_count, 11);
for e = 1:shell_count
    elm = elements{e};
    x_e = nodes(elm, 1); y_e = nodes(elm, 2);
    [Mxx, Myy, Mxy, Fxx, Fyy, V13, V23] = shell_centroid(elm, u, x_e, y_e, E, nu, t_slab);
    Mmax = 0.5*(Mxx+Myy) + sqrt(0.25*(Mxx-Myy)^2 + Mxy^2);
    Mmin = 0.5*(Mxx+Myy) - sqrt(0.25*(Mxx-Myy)^2 + Mxy^2);
    Vmax = sqrt(V13^2 + V23^2);
    % vonMises top fiber
    f11 = Fxx / t_slab; f22 = Fyy / t_slab;
    sxx = f11 + Mxx*6/t_slab^2;
    syy = f22 + Myy*6/t_slab^2;
    sxy = Mxy*6/t_slab^2;
    vM = sqrt(sxx^2 - sxx*syy + syy^2 + 3*sxy^2);
    shell_res(e, :) = [Mxx, Myy, Mxy, Mmax, Mmin, Fxx, Fyy, V13, V23, Vmax, vM];
end

%% ─── RECOVERY estilo ETABS: 2x2 Gauss + extrapolacion al nodo ───────
% ETABS muestrea Mxy en los 4 puntos de Gauss (+/-1/sqrt3) y extrapola al nodo
% (literal 0.5773502691896257 hallado hardcodeado en CsiGo2.dll). El centroide
% subestima el pico de twist; aca lo comparamos en el MISMO modelo.
nodal_Mxy = zeros(n_nodes, 1);
nodal_cnt = zeros(n_nodes, 1);
for e = 1:shell_count
    elm = elements{e};
    x_e = nodes(elm, 1); y_e = nodes(elm, 2);
    [~, ~, Mxy4] = shell_gauss(elm, u, x_e, y_e, E, nu, t_slab);
    for k = 1:4
        nodal_Mxy(elm(k)) = nodal_Mxy(elm(k)) + Mxy4(k);
        nodal_cnt(elm(k)) = nodal_cnt(elm(k)) + 1;
    end
end
sel = nodal_cnt > 0;
nodal_Mxy(sel) = nodal_Mxy(sel) ./ nodal_cnt(sel);
Mxy_max_centroid = max(abs(shell_res(:, 3)));
Mxy_max_gauss    = max(abs(nodal_Mxy));
fprintf('\n=== RECOVERY Mxy: centroide vs Gauss 2x2 (ETABS) ===\n');
fprintf('  Mxy max centroide (shell_centroid)   : %.4f kN*m/m\n', Mxy_max_centroid);
fprintf('  Mxy max Gauss+extrapol (shell_gauss) : %.4f kN*m/m\n', Mxy_max_gauss);
fprintf('  ETABS muestrea en +/-1/sqrt3 = %.16f\n', 1/sqrt(3));

%% ─── POST-PROCESS: frame forces ──────────────────────────────────
n_frames = n_elements - shell_count;
frame_F = zeros(n_frames, 12);
for e = (shell_count+1):n_elements
    elm = elements{e};
    props = elem_props{e};
    n0 = nodes(elm(1), :); n1 = nodes(elm(2), :);
    K_loc = kframe_local(n0, n1, props);
    T = tframe(n0, n1);
    uG = [u(6*elm(1)-5:6*elm(1)); u(6*elm(2)-5:6*elm(2))];
    fL = K_loc * (T * uG);
    frame_F(e - shell_count, :) = fL';
end

%% ─── REPORTE ─────────────────────────────────────────────────────
fprintf('\n=== MESA TORSION MATLAB - RESULTADOS ===\n');

% Centro losa: (N/2 par, mesh 5 → centro entre nodos)
center_node = ix_slab(round(N/2), round(N/2));
w_center = abs(u(6*center_node - 3)) * 1000;
fprintf('\nDEFLEXION:\n');
fprintf('  w al nodo ~centro = %.4f mm (nodo #%d)\n', w_center, center_node);

% Shell resultants: max y min separados (Mmin debe ser el valor más negativo)
labels_shell = {'Mxx', 'Myy', 'Mxy', 'Mmax', 'Mmin', 'Fxx', 'Fyy', 'V13', 'V23', 'Vmax', 'vonMises'};
units_shell  = {'kN*m/m','kN*m/m','kN*m/m','kN*m/m','kN*m/m','kN/m','kN/m','kN/m','kN/m','kN/m','kPa'};
fprintf('\nSHELL RESULTANTS (extremos por celda):\n');
for k = 1:length(labels_shell)
    if k == 5   % Mmin → valor más negativo (no max abs)
        [vmin, idx] = min(shell_res(:, k));
        fprintf('  %-10s = %+9.3f %-8s  (cell #%d, min)\n', labels_shell{k}, vmin, units_shell{k}, idx);
    else
        [vmax, idx] = max(abs(shell_res(:, k)));
        val_signed = shell_res(idx, k);
        fprintf('  %-10s = %+9.3f %-8s  (cell #%d, max|·|)\n', labels_shell{k}, val_signed, units_shell{k}, idx);
    end
end

% Frame forces — separar columnas vs vigas en el reporte
labels_frame = {'N (axial)', 'V2 (shear)', 'V3 (shear)', 'T (torsion)', 'M2 (bending)', 'M3 (bending)'};
units_frame  = {'kN', 'kN', 'kN', 'kN*m', 'kN*m', 'kN*m'};
n_cols = 4;
fprintf('\nFRAME FORCES en COLUMNAS (max abs):\n');
for k = 1:6
    [vmax, idx] = max(abs(frame_F(1:n_cols, k)));
    val_signed = frame_F(idx, k);
    fprintf('  %-12s = %+8.3f %-5s  (col #%d)\n', labels_frame{k}, val_signed, units_frame{k}, idx);
end
fprintf('\nFRAME FORCES en VIGAS (max abs):\n');
for k = 1:6
    [vmax, rel_idx] = max(abs(frame_F(n_cols+1:end, k)));
    idx = rel_idx + n_cols;
    val_signed = frame_F(idx, k);
    fprintf('  %-12s = %+8.3f %-5s  (viga #%d)\n', labels_frame{k}, val_signed, units_frame{k}, idx - n_cols);
end

%% ─── PLOT ────────────────────────────────────────────────────────
try
    fig = figure('visible', 'off', 'position', [100, 100, 900, 700]);

    % Subplot 1: deformed shape + Mmax colormap
    subplot(2, 2, 1);
    plot_slab_field(nodes, elements, shell_count, u, shell_res, 4, 'Mmax [kN m/m]');

    subplot(2, 2, 2);
    plot_slab_field(nodes, elements, shell_count, u, shell_res, 3, 'Mxy [kN m/m]');

    subplot(2, 2, 3);
    plot_slab_field(nodes, elements, shell_count, u, shell_res, 10, 'Vmax [kN/m]');

    subplot(2, 2, 4);
    plot_slab_field(nodes, elements, shell_count, u, shell_res, 11, 'vonMises [kPa]');

    try
        saveas(fig, 'mesa_torsion_matlab.png');
        fprintf('\nPlot guardado: mesa_torsion_matlab.png\n');
    catch
        fprintf('\n  [info] PNG save skipped (no headless backend)\n');
    end
catch err
    fprintf('\n  [info] plot skipped: %s\n', err.message);
end

%% ─── EXPORT CSV ─────────────────────────────────────────────────
csv_shell = [(1:shell_count)', shell_res];
csv_header = 'cell_id,Mxx,Myy,Mxy,Mmax,Mmin,Fxx,Fyy,V13,V23,Vmax,vonMises';
fid = fopen('mesa_torsion_shell_results.csv', 'w');
fprintf(fid, '%s\n', csv_header);
fclose(fid);
dlmwrite('mesa_torsion_shell_results.csv', csv_shell, '-append', 'precision', 6);

csv_frame = [(1:n_frames)', frame_F];
csv_header2 = 'frame_id,N_i,V2_i,V3_i,T_i,M2_i,M3_i,N_j,V2_j,V3_j,T_j,M2_j,M3_j';
fid = fopen('mesa_torsion_frame_results.csv', 'w');
fprintf(fid, '%s\n', csv_header2);
fclose(fid);
dlmwrite('mesa_torsion_frame_results.csv', csv_frame, '-append', 'precision', 6);
fprintf('CSV exportados: mesa_torsion_shell_results.csv, mesa_torsion_frame_results.csv\n');


end   % end of main function mesa_torsion_matlab

%% ═════════════════════════════════════════════════════════════════
%%   Helpers en archivos separados (mismo dir):
%%     kbend_mzc.m, kmembrane_q4.m, kframe_local.m, tframe.m, shell_centroid.m
%%   Solo plot_slab_field queda local porque es de visualización auxiliar.
%% ═════════════════════════════════════════════════════════════════

% (Las helpers kbend_mzc, kmembrane_q4, kframe_local, tframe, shell_centroid
% se encuentran en archivos separados del mismo dir.)


function plot_slab_field(nodes, elements, shell_count, u, shell_res, col_idx, title_str)
    % Plot 2D del campo sobre la losa
    n_per_side = sqrt(shell_count) + 1;
    % Centroides de los shells
    cx = zeros(shell_count, 1);
    cy = zeros(shell_count, 1);
    for e = 1:shell_count
        elm = elements{e};
        cx(e) = mean(nodes(elm, 1));
        cy(e) = mean(nodes(elm, 2));
    end
    vals = shell_res(:, col_idx);
    scatter(cx, cy, 200, vals, 'filled', 's');
    colorbar();
    axis equal;
    xlabel('x [m]'); ylabel('y [m]');
    title(title_str);
end
