% =============================================================================
%  TIMOSHENKO §51 - PLACA SS + VIGAS ELASTICAS EN BORDES (compatibility torsion)
% =============================================================================
%  Geometria: placa 6 x 4 m, t=0.10 m, mesh 12x8 Q4.
%  SS perimetral en los 4 bordes (UZ=0).
%  Vigas V30x60 en los bordes longitudinales y=0 y y=Ly (a lo largo de X).
%  Material: E=24.85 GPa, nu=0.20.
%  Carga: q=10 kN/m2 uniforme.
%
%  Compatibility torsion: las vigas restringen parcialmente la rotacion local
%  del borde de la losa → aparece T en la viga sin que ella reciba flexion neta.
%
%  Reusa helpers: kbend_mzc.m, kmembrane_q4.m, kframe_local.m, tframe.m,
%                 shell_centroid.m (mismo directorio).
% =============================================================================

function timoshenko_edge_beams()

if exist('OCTAVE_VERSION', 'builtin')
    graphics_toolkit('gnuplot');
end
close all;

%% ─── PARAMETROS ──────────────────────────────────────────────────
Lx = 6.0; Ly = 4.0;
t_slab = 0.10;
bV = 0.30; hV = 0.60;
Nx = 12; Ny = 8;
E  = 24.85e6;
nu = 0.20;
q_load = 10.0;

sv_J = @(b,h) (1/3) * (1 - 0.21*min(b,h)/max(b,h)*(1 - (min(b,h)/max(b,h))^4/12)) * max(b,h) * min(b,h)^3;
Jv = sv_J(bV, hV);

%% ─── MESH ────────────────────────────────────────────────────────
nPSx = Nx + 1; nPSy = Ny + 1;
dx = Lx/Nx; dy = Ly/Ny;

nodes = [];
for j = 0:Ny
    for i = 0:Nx
        nodes = [nodes; i*dx, j*dy, 0.0];
    end
end
ix_slab = @(i, j) j*nPSx + i + 1;
n_nodes = size(nodes, 1);
fprintf('Timoshenko §51 MATLAB: %d nodos\n', n_nodes);

%% ─── ELEMENTOS ───────────────────────────────────────────────────
elements = {}; elem_type = {}; elem_props = {};

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

% Vigas longitudinales en y=0 e y=Ly
viga_props = struct('E', E, 'nu', nu, 'A', bV*hV, ...
                    'Iy', bV*hV^3/12, 'Iz', hV*bV^3/12, 'J', Jv);
for i = 0:Nx-1
    elements{end+1} = [ix_slab(i,0), ix_slab(i+1,0)];
    elem_type{end+1} = 'frame'; elem_props{end+1} = viga_props;
end
for i = 0:Nx-1
    elements{end+1} = [ix_slab(i,Ny), ix_slab(i+1,Ny)];
    elem_type{end+1} = 'frame'; elem_props{end+1} = viga_props;
end

n_elements = length(elements);
fprintf('Elementos: %d shells + %d frames (vigas longitudinales)\n', ...
        shell_count, n_elements - shell_count);

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

%% ─── BCs: SS perimetral 4 lados ──────────────────────────────────
fixed_dofs = [];
for j = 0:Ny
    for i = 0:Nx
        on_x0 = (i == 0); on_xL = (i == Nx);
        on_y0 = (j == 0); on_yL = (j == Ny);
        if on_x0 || on_xL || on_y0 || on_yL
            n_id = ix_slab(i, j);
            fixed_dofs = [fixed_dofs, 6*n_id - 3];   % UZ
            if (on_x0 || on_xL) && (on_y0 || on_yL)
                fixed_dofs = [fixed_dofs, 6*n_id - 5, 6*n_id - 4];
            end
        end
    end
end
fixed_dofs = unique(fixed_dofs);
free_dofs = setdiff(1:ndof, fixed_dofs);

%% ─── CARGAS ──────────────────────────────────────────────────────
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
fprintf('Carga total: %.3f kN (esperado: %.3f kN)\n', sum(F), -q_load*Lx*Ly);

%% ─── SOLVE ───────────────────────────────────────────────────────
u = zeros(ndof, 1);
tic;
u(free_dofs) = K_global(free_dofs, free_dofs) \ F(free_dofs);
fprintf('Solved in %.2f s (ndof_free=%d)\n', toc, length(free_dofs));

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
    sxx = f11 + Mxx*6/t_slab^2; syy = f22 + Myy*6/t_slab^2;
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
fprintf('\n=== TIMOSHENKO §51 MATLAB ===\n');
center_node = ix_slab(round(Nx/2), round(Ny/2));
w_center = abs(u(6*center_node - 3)) * 1000;
fprintf('\nw_max (centro) = %.4f mm\n', w_center);

% Navier SS pura para reducción
D = E * t_slab^3 / (12*(1-nu^2));
w_ss = 0;
for m = 1:2:41
    for n = 1:2:41
        denom = (m^2/Lx^2 + n^2/Ly^2)^2;
        w_ss = w_ss + 16*q_load / (pi^6 * m * n * denom) * sin(m*pi/2)*sin(n*pi/2) / D;
    end
end
w_ss_mm = abs(w_ss) * 1000;
reduccion = (w_ss_mm - w_center) / w_ss_mm * 100;
fprintf('w_SS_Navier (sin vigas) = %.4f mm\n', w_ss_mm);
fprintf('Reduccion por vigas     = %+.2f %%\n', reduccion);

% Shell results
labels = {'Mxx','Myy','Mxy','Mmax','Mmin','Fxx','Fyy','V13','V23','Vmax','vonMises'};
units  = {'kN*m/m','kN*m/m','kN*m/m','kN*m/m','kN*m/m','kN/m','kN/m','kN/m','kN/m','kN/m','kPa'};
fprintf('\nSHELL RESULTANTS:\n');
for k = 1:length(labels)
    if k == 5
        [vmin, idx] = min(shell_res(:, k));
        fprintf('  %-10s = %+9.3f %-8s (cell #%d, min)\n', labels{k}, vmin, units{k}, idx);
    else
        [~, idx] = max(abs(shell_res(:, k)));
        fprintf('  %-10s = %+9.3f %-8s (cell #%d, max|·|)\n', labels{k}, shell_res(idx,k), units{k}, idx);
    end
end

% Compatibility torsion en vigas
fprintf('\nVIGAS - compatibility torsion:\n');
labels_fr = {'N','V2','V3','T','M2','M3'};
units_fr  = {'kN','kN','kN','kN*m','kN*m','kN*m'};
for k = 1:6
    [~, idx] = max(abs(frame_F(:, k)));
    val = frame_F(idx, k);
    fprintf('  %-3s = %+8.3f %-5s  (viga #%d)\n', labels_fr{k}, val, units_fr{k}, idx);
end

end
