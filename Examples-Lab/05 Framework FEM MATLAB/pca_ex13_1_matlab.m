% =============================================================================
%  PCA EXAMPLE 13-1 STYLE - SPANDREL + LOSA COMPATIBILITY TORSION (ACI 318)
% =============================================================================
%  Setup adaptado en SI (PCA original = 40 ft / 16 in, equivalente en metros):
%     Losa rectangular 10x4 m, t=0.20 m, mesh 10x4.
%     Spandrel V30x60 en borde y=0, columnas C40x40 H=4 m.
%     BCs: 2 columnas empotradas, losa SS en 3 bordes (excepto y=0 = spandrel).
%     Carga q=10 kN/m2 uniforme.
%
%  Pregunta de diseno: T_max en spandrel vs T_u_lim ACI 318 §11.5.2.2.
%     Si T_FEM > phi*Tcr → equilibrium torsion (diseñar refuerzo).
%     Si T_FEM < phi*Tcr → compatibility torsion (redistribuir hacia losa).
%
%  Reusa helpers: kbend_mzc.m, kmembrane_q4.m, kframe_local.m, tframe.m,
%                 shell_centroid.m (mismo directorio).
% =============================================================================

function pca_ex13_1_matlab()

if exist('OCTAVE_VERSION', 'builtin')
    graphics_toolkit('gnuplot');
end
close all;

%% ─── PARAMETROS ──────────────────────────────────────────────────
Lx = 10.0; Ly = 4.0;
t_slab = 0.20;
H_col  = 4.0;
bC = 0.40; hC = 0.40;
bV = 0.30; hV = 0.60;
Nx = 10; Ny = 4;
E  = 24.85e6;        % kPa
nu = 0.20;
q_load = 10.0;       % kN/m2 uniforme

sv_J = @(b,h) (1/3) * (1 - 0.21*min(b,h)/max(b,h)*(1 - (min(b,h)/max(b,h))^4/12)) * max(b,h) * min(b,h)^3;
Jc = 0.141 * bC^4;
Jv = sv_J(bV, hV);

%% ─── MESH ────────────────────────────────────────────────────────
nPSx = Nx + 1; nPSy = Ny + 1;
dx = Lx/Nx; dy = Ly/Ny;

% Nodos losa en z=H_col
nodes = [];
for j = 0:Ny
    for i = 0:Nx
        nodes = [nodes; i*dx, j*dy, H_col];
    end
end
ix_slab = @(i, j) j*nPSx + i + 1;

% 2 bases columnas en z=0
nodes = [nodes; 0,  0, 0;   Lx, 0, 0];
base_idx_1 = size(nodes, 1) - 1;   % col izq
base_idx_2 = size(nodes, 1);       % col der

n_nodes = size(nodes, 1);
fprintf('PCA 13-1 MATLAB: %d nodos (losa %dx%d = %d + 2 bases)\n', ...
        n_nodes, nPSx, nPSy, nPSx*nPSy);

%% ─── ELEMENTOS ───────────────────────────────────────────────────
elements = {};
elem_type = {};
elem_props = {};

% Shells losa
shell_count = 0;
for j = 0:Ny-1
    for i = 0:Nx-1
        elements{end+1} = [ix_slab(i,j), ix_slab(i+1,j), ix_slab(i+1,j+1), ix_slab(i,j+1)];
        elem_type{end+1} = 'shell';
        elem_props{end+1} = struct('E', E, 'nu', nu, 't', t_slab);
        shell_count = shell_count + 1;
    end
end

% 2 columnas
col_props = struct('E', E, 'nu', nu, 'A', bC*hC, ...
                   'Iy', bC*hC^3/12, 'Iz', hC*bC^3/12, 'J', Jc);
elements{end+1} = [base_idx_1, ix_slab(0, 0)];
elem_type{end+1} = 'frame'; elem_props{end+1} = col_props;
elements{end+1} = [base_idx_2, ix_slab(Nx, 0)];
elem_type{end+1} = 'frame'; elem_props{end+1} = col_props;

% Spandrel: segmentos en y=0
viga_props = struct('E', E, 'nu', nu, 'A', bV*hV, ...
                    'Iy', bV*hV^3/12, 'Iz', hV*bV^3/12, 'J', Jv);
for i = 0:Nx-1
    elements{end+1} = [ix_slab(i,0), ix_slab(i+1,0)];
    elem_type{end+1} = 'frame';
    elem_props{end+1} = viga_props;
end

n_elements = length(elements);
fprintf('Elementos: %d shells + %d frames (%d cols + %d spandrel segs)\n', ...
        shell_count, n_elements - shell_count, 2, Nx);

%% ─── ENSAMBLE K GLOBAL ───────────────────────────────────────────
ndof = 6 * n_nodes;
I_list = []; J_list = []; V_list = [];

for e = 1:n_elements
    elm = elements{e};
    props = elem_props{e};
    if strcmp(elem_type{e}, 'shell')
        x_e = nodes(elm, 1); y_e = nodes(elm, 2);
        Kb = kbend_mzc(x_e, y_e, props.E, props.nu, props.t);
        Km = kmembrane_q4(x_e, y_e, props.E, props.nu, props.t);
        diag_b = max(abs(diag(Kb))); k_drill = 1e-3 * diag_b;
        Kd = k_drill * eye(4);
        gdofs = zeros(1, 24);
        for k = 1:4
            n = elm(k);
            gdofs(6*k-5:6*k) = (6*n-5):(6*n);
        end
        K24 = zeros(24, 24);
        for ni = 1:4
            for nj = 1:4
                K24(6*(ni-1)+1, 6*(nj-1)+1) = Km(2*(ni-1)+1, 2*(nj-1)+1);
                K24(6*(ni-1)+1, 6*(nj-1)+2) = Km(2*(ni-1)+1, 2*(nj-1)+2);
                K24(6*(ni-1)+2, 6*(nj-1)+1) = Km(2*(ni-1)+2, 2*(nj-1)+1);
                K24(6*(ni-1)+2, 6*(nj-1)+2) = Km(2*(ni-1)+2, 2*(nj-1)+2);
            end
        end
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
        for ni = 1:4
            K24(6*ni, 6*ni) = K24(6*ni, 6*ni) + Kd(ni, ni);
        end
        [Ii, Jj] = ndgrid(gdofs, gdofs);
        I_list = [I_list; Ii(:)];
        J_list = [J_list; Jj(:)];
        V_list = [V_list; K24(:)];
    else
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
% Bases columnas empotradas
fixed_dofs = [(6*base_idx_1-5):(6*base_idx_1), (6*base_idx_2-5):(6*base_idx_2)];

% Losa SS en 3 bordes (x=0, x=Lx, y=Ly) — excluyendo esquinas con columnas
col_corner_nodes = [ix_slab(0, 0), ix_slab(Nx, 0)];
for j = 0:Ny
    for i = 0:Nx
        on_x0 = (i == 0);
        on_xL = (i == Nx);
        on_yB = (j == Ny);
        if ~(on_x0 || on_xL || on_yB)
            continue
        end
        n_id = ix_slab(i, j);
        if any(n_id == col_corner_nodes)
            continue
        end
        % UZ=0 perimetral
        fixed_dofs = [fixed_dofs, 6*n_id - 3];
        % En esquinas SS (cruce 2 bordes): UX=UY también
        if (on_x0 && on_yB) || (on_xL && on_yB)
            fixed_dofs = [fixed_dofs, 6*n_id - 5, 6*n_id - 4];
        end
    end
end
fixed_dofs = unique(fixed_dofs);
free_dofs = setdiff(1:ndof, fixed_dofs);

% Carga lumped trapezoidal
F = zeros(ndof, 1);
for j = 0:Ny
    for i = 0:Nx
        xE = (i == 0 || i == Nx);
        yE = (j == 0 || j == Ny);
        f = q_load * dx * dy;
        if xE && yE
            f = f * 0.25;
        elseif xE || yE
            f = f * 0.5;
        end
        n = ix_slab(i, j);
        F(6*n - 3) = F(6*n - 3) - f;
    end
end
fprintf('Carga total losa: %.3f kN (esperado: %.3f kN)\n', ...
        sum(F), -q_load * Lx * Ly);

%% ─── SOLVE ───────────────────────────────────────────────────────
u = zeros(ndof, 1);
fprintf('Solving K*u = F  (ndof_free=%d)...\n', length(free_dofs));
tic;
u(free_dofs) = K_global(free_dofs, free_dofs) \ F(free_dofs);
fprintf('Solved in %.2f s\n', toc);

%% ─── POST-PROCESS shell ──────────────────────────────────────────
shell_res = zeros(shell_count, 11);
for e = 1:shell_count
    elm = elements{e};
    x_e = nodes(elm, 1); y_e = nodes(elm, 2);
    [Mxx, Myy, Mxy, Fxx, Fyy, V13, V23] = shell_centroid(elm, u, x_e, y_e, E, nu, t_slab);
    Mmax = 0.5*(Mxx+Myy) + sqrt(0.25*(Mxx-Myy)^2 + Mxy^2);
    Mmin = 0.5*(Mxx+Myy) - sqrt(0.25*(Mxx-Myy)^2 + Mxy^2);
    Vmax = sqrt(V13^2 + V23^2);
    f11 = Fxx/t_slab; f22 = Fyy/t_slab;
    sxx = f11 + Mxx*6/t_slab^2;
    syy = f22 + Myy*6/t_slab^2;
    sxy = Mxy*6/t_slab^2;
    vM = sqrt(sxx^2 - sxx*syy + syy^2 + 3*sxy^2);
    shell_res(e, :) = [Mxx, Myy, Mxy, Mmax, Mmin, Fxx, Fyy, V13, V23, Vmax, vM];
end

%% ─── POST-PROCESS frames ─────────────────────────────────────────
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
fprintf('\n=== PCA EX13-1 MATLAB - RESULTADOS ===\n');

center_node = ix_slab(round(Nx/2), round(Ny/2));
w_center = abs(u(6*center_node - 3)) * 1000;
fprintf('\nDEFLEXION (centro losa):\n');
fprintf('  w = %.4f mm (nodo #%d)\n', w_center, center_node);

% Shell resultants
labels = {'Mxx', 'Myy', 'Mxy', 'Mmax', 'Mmin', 'Fxx', 'Fyy', 'V13', 'V23', 'Vmax', 'vonMises'};
units  = {'kN*m/m','kN*m/m','kN*m/m','kN*m/m','kN*m/m','kN/m','kN/m','kN/m','kN/m','kN/m','kPa'};
fprintf('\nSHELL RESULTANTS losa:\n');
for k = 1:length(labels)
    if k == 5
        [vmin, idx] = min(shell_res(:, k));
        fprintf('  %-10s = %+9.3f %-8s (cell #%d, min)\n', labels{k}, vmin, units{k}, idx);
    else
        [~, idx] = max(abs(shell_res(:, k)));
        val = shell_res(idx, k);
        fprintf('  %-10s = %+9.3f %-8s (cell #%d, max|·|)\n', labels{k}, val, units{k}, idx);
    end
end

% Spandrel frame forces (frames 3..n_frames, después de las 2 cols)
fprintf('\nSPANDREL forces (max abs en nodos i/j):\n');
labels_fr = {'N', 'V2', 'V3', 'T', 'M2', 'M3'};
units_fr  = {'kN', 'kN', 'kN', 'kN*m', 'kN*m', 'kN*m'};
spandrel_rows = 3:n_frames;
for k = 1:6
    [~, rel_idx] = max(abs(frame_F(spandrel_rows, k)));
    idx = spandrel_rows(rel_idx);
    val_i = frame_F(idx, k);
    val_j = frame_F(idx, k+6);
    fprintf('  %-3s = i=%+8.3f  j=%+8.3f %-5s  (spandrel seg #%d)\n', ...
            labels_fr{k}, val_i, val_j, units_fr{k}, idx - 2);
end

fprintf('\nCOLUMNAS (max abs en nodo i):\n');
for k = 1:6
    [~, idx] = max(abs(frame_F(1:2, k)));
    val = frame_F(idx, k);
    fprintf('  %-3s = %+8.3f %-5s  (col #%d)\n', labels_fr{k}, val, units_fr{k}, idx);
end

% ACI compatibility torsion limit (referencia diseño)
fc_MPa = 21.0;
phi = 0.75;
Acp = bV * hV * 1e6;
pcp = 2 * (bV + hV) * 1e3;
T_u_lim_kNm = phi * (1/3) * sqrt(fc_MPa) * (Acp^2 / pcp) * 1e-6;
T_max_spandrel = max(abs(frame_F(spandrel_rows, 4)));
fprintf('\n=== REFERENCIA ACI 318 §11.5.2.2 ===\n');
fprintf('  φ·Tcr (compat. torsion limit) = %.2f kN·m  (f''c=%.1f MPa, V%dx%d)\n', ...
        T_u_lim_kNm, fc_MPa, bV*100, hV*100);
fprintf('  T_FEM spandrel max            = %.3f kN·m\n', T_max_spandrel);
if T_max_spandrel < T_u_lim_kNm
    fprintf('  → COMPATIBILITY TORSION regime: la losa puede redistribuir.\n');
    fprintf('     ACI 318 permite reducir T a 0.33·φ·Tcr ≈ %.2f kN·m y aumentar M en losa.\n', ...
            0.33 * T_u_lim_kNm);
else
    fprintf('  → EQUILIBRIUM TORSION regime: el spandrel DEBE diseñarse para T_FEM.\n');
end

%% ─── EXPORT CSV ──────────────────────────────────────────────────
csv_shell = [(1:shell_count)', shell_res];
hdr1 = 'cell_id,Mxx,Myy,Mxy,Mmax,Mmin,Fxx,Fyy,V13,V23,Vmax,vonMises';
fid = fopen('pca_ex13_1_shell_results.csv', 'w');
fprintf(fid, '%s\n', hdr1);
fclose(fid);
dlmwrite('pca_ex13_1_shell_results.csv', csv_shell, '-append', 'precision', 6);

csv_frame = [(1:n_frames)', frame_F];
hdr2 = 'frame_id,N_i,V2_i,V3_i,T_i,M2_i,M3_i,N_j,V2_j,V3_j,T_j,M2_j,M3_j';
fid = fopen('pca_ex13_1_frame_results.csv', 'w');
fprintf(fid, '%s\n', hdr2);
fclose(fid);
dlmwrite('pca_ex13_1_frame_results.csv', csv_frame, '-append', 'precision', 6);
fprintf('\nCSV exportados.\n');

end
