%% Plate Thin — Cálculo de Deflexión (Calcpad Lab v1)
% Reproduce el cálculo de placa SS con carga uniforme usando:
%   - Expresiones MATLAB en sintaxis nativa
%   - Solución analítica de Navier (no requiere matrix assembly)
%   - Soluciones FEM por intrínsecas (próxima versión con kernel C++)

%% Parámetros geométricos
W = 1.0;                          % ancho de la placa [m]
H = 1.0;                          % alto [m]
t = 0.05;                         % espesor [m] (t/a = 0.05 → delgada)
q = 1.0;                          % carga uniforme [N/m²]

%% Material
E = 30000;                        % módulo elástico [MPa]
nu = 0.2;                         % Poisson

%% Rigidez a flexión
D = E*t^3 / (12 * (1 - nu^2))

%% Solución analítica de Navier (placa cuadrada SS)
% w_max = α · q · a⁴ / D     con α = 0.00406 (Timoshenko)
alpha = 0.00406
w_max_navier = alpha * q * W^4 / D

%% Solución de Reissner (incluye corte transversal)
% Para t/a = 0.05 el corte aporta ~5% adicional
G = E / (2 * (1 + nu));
kappa = 5/6;
w_shear = 0.0737 * q * W^2 / (kappa * G * t)
w_max_reissner = w_max_navier + w_shear

%% Comparación
ratio = w_max_reissner / w_max_navier

%% Resultados resumen
%   D            = rigidez flexión
%   w_max_navier = deflexión Kirchhoff (sin corte)
%   w_max_reissner = deflexión Mindlin (con corte)
%   ratio        = factor de incremento por shear deformation
