%% Plate Thin - Calculo de Deflexion (Calcpad Lab v1)
% Reproduce el calculo de placa SS con carga uniforme usando:
%   - Expresiones MATLAB en sintaxis nativa
%   - Solucion analitica de Navier (no requiere matrix assembly)
%   - Soluciones FEM por intrinsecas (proxima version con kernel C++)

%% Parametros geometricos
W = 1.0;                          % ancho de la placa [m]
H = 1.0;                          % alto [m]
t = 0.05;                         % espesor [m] (t/a = 0.05 -> delgada)
q = 1.0;                          % carga uniforme [N/m2]

%% Material
E = 30000;                        % modulo elastico [MPa]
nu = 0.2;                         % Poisson

%% Rigidez a flexion
D = E*t^3 / (12 * (1 - nu^2))

%% Solucion analitica de Navier (placa cuadrada SS)
% w_max = alpha * q * a4 / D     con alpha = 0.00406 (Timoshenko)
alpha = 0.00406
w_max_navier = alpha * q * W^4 / D

%% Solucion de Reissner (incluye corte transversal)
% Para t/a = 0.05 el corte aporta ~5% adicional
G = E / (2 * (1 + nu));
kappa = 5/6;
w_shear = 0.0737 * q * W^2 / (kappa * G * t)
w_max_reissner = w_max_navier + w_shear

%% Comparacion
ratio = w_max_reissner / w_max_navier

%% Resultados resumen
%   D            = rigidez flexion
%   w_max_navier = deflexion Kirchhoff (sin corte)
%   w_max_reissner = deflexion Mindlin (con corte)
%   ratio        = factor de incremento por shear deformation
