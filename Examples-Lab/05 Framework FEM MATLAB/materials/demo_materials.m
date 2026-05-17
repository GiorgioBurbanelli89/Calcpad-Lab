% ============================================================
%  Demo central de las 4 matrices constitutivas (materials/)
%   - buildIsoDb         placa isotropa, flexion
%   - buildIsoDs         placa isotropa, corte transversal
%   - buildOrthotropicDb placa ortotropa, flexion
%   - buildOrthotropicDs placa ortotropa, corte
% ============================================================

%% (1) Isotropa - placa hormigon E=30 GPa, nu=0.2, t=15 cm
E_iso  = 30e9;
nu_iso = 0.2;
t_iso  = 0.15;

Db_iso = buildIsoDb(E_iso, nu_iso, t_iso);
Ds_iso = buildIsoDs(E_iso, nu_iso, t_iso);

D_teorico = E_iso * t_iso^3 / (12 * (1 - nu_iso^2));

fprintf('--- Isotropa: hormigon E=30 GPa, t=15 cm, nu=0.2 ---\n');
fprintf('  D (rigidez flexion teorica) = %.4e N*m\n', D_teorico);
fprintf('  Db(1,1) calculado           = %.4e N*m\n', Db_iso(1,1));
fprintf('  Db(1,2) calculado           = %.4e   (= nu*D)\n', Db_iso(1,2));
fprintf('  Db(3,3) calculado           = %.4e   (= (1-nu)/2 * D)\n', Db_iso(3,3));
fprintf('  Ds(1,1) corte               = %.4e\n', Ds_iso(1,1));
fprintf('  Ds(2,2) corte               = %.4e\n', Ds_iso(2,2));

%% (2) Ortotropa - laminado E1=140 GPa, E2=10 GPa
E1_o   = 140e9;
E2_o   = 10e9;
nu12_o = 0.3;
G12_o  = 5e9;
G13_o  = 5e9;
G23_o  = 4e9;
t_o    = 0.005;   % 5 mm

Db_ort = buildOrthotropicDb(E1_o, E2_o, nu12_o, G12_o, t_o);
Ds_ort = buildOrthotropicDs(G13_o, G23_o, t_o);

fprintf('\n--- Ortotropa: laminado E1=140 GPa, E2=10 GPa, t=5 mm ---\n');
fprintf('  Db(1,1) calculado = %.4e   (rigidez flexion 11)\n', Db_ort(1,1));
fprintf('  Db(2,2) calculado = %.4e   (rigidez flexion 22)\n', Db_ort(2,2));
fprintf('  Db(3,3) calculado = %.4e   (rigidez torsion 12)\n', Db_ort(3,3));
fprintf('  Ds(1,1) corte 13  = %.4e\n', Ds_ort(1,1));
fprintf('  Ds(2,2) corte 23  = %.4e\n', Ds_ort(2,2));

%% Resumen
fprintf('\n--- Resumen ---\n');
fprintf('D teorico isotropa  = %.4e N*m\n', D_teorico);
fprintf('Diagonal Db_iso     = %s\n', mat2str([Db_iso(1,1), Db_iso(2,2), Db_iso(3,3)], 5));
fprintf('Diagonal Db_ort     = %s\n', mat2str([Db_ort(1,1), Db_ort(2,2), Db_ort(3,3)], 5));

% Echo matematico escalar
D_teorico
