%% Shell Damage — seccion de cascara RC por capas (M-kappa con dano de concreto)
% -------------------------------------------------------------------------
% Seccion de cascara / placa de hormigon armado, por metro de ancho,
% analizada por CAPAS (layered shell section). Se calcula la respuesta
% momento-curvatura M(kappa) con DANO DE CONCRETO por capa: al crecer el
% dano, las capas comprimidas se ablandan (softening) y el momento cae
% despues del pico = "shell damage".
%
% Concreto (MISMO modelo que concrete_damage):
%   E=30000 MPa, fc=30, ec0=0.0020, ecu=0.0035, ft=3, et0=ft/E=0.0001,
%   etf=0.0005, Z=(1-0.2)/(ecu-ec0)=533.33
%   sc(e): traccion elastica hasta et0, luego ft*exp(-(e-et0)/etf);
%          compresion (x=-e) parabola -fc*(2r-r^2) hasta ec0, luego
%          -fc*max(0.2, 1-Z*(x-ec0)).
%   dano d(e)=clamp(1 - sc/(E*e), 0, 1).
% Acero:  Es=200000, fy=420, elasto-plastico sc(e)=clamp(Es*e, -fy, fy).
%
% Seccion (por metro de ancho):
%   t=200 mm, b=1000 mm, nc=20 capas de concreto (dz=10 mm),
%   z_i = -t/2 + dz*(i-0.5).  Acero As=1000 mm2/m en cada cara,
%   recubrimiento cover=25 -> brazo zs = t/2 - cover = 75 mm.
%
% Cinematica de la seccion:  e(z) = e0 + kappa*z.
% Para cada curvatura kappa:
%   1) e0 por EQUILIBRIO AXIAL N(e0)=0  (biseccion, ~60 iter).
%   2) M(kappa) = suma de momentos de capas + acero            [N*mm -> kN*m]
%   3) d_comp = dano en la fibra mas comprimida  e = e0 - kappa*t/2
%
% Valores VALIDADOS (deben coincidir):
%   kappa(1/mm)   M(kN*m/m)   d_comp
%    5.0e-6         40.80      0.082
%    1.0e-5         51.15      0.127
%    1.5e-5         67.74      0.175
%    1.64e-5        72.34 pico 0.188
%    2.0e-5         71.21      0.206
%    3.0e-5         69.94      0.253
%    4.0e-5         69.75      0.297
% -------------------------------------------------------------------------
clear; clc; close all;

%% Propiedades de los materiales
E   = 30000;    % MPa   modulo elastico del concreto
fc  = 30;       % MPa   resistencia a compresion
ft  = 3;        % MPa   resistencia a traccion
ec0 = 0.0020;   %       deformacion pico en compresion
ecu = 0.0035;   %       deformacion ultima en compresion
et0 = ft/E;     % = 0.0001
etf = 0.0005;   %       softening en traccion
Z   = (1 - 0.2)/(ecu - ec0);   % = 533.33
Es  = 200000;   % MPa   modulo del acero
fy  = 420;      % MPa   fluencia del acero

%% Geometria de la seccion por capas
t     = 200;                 % mm  espesor de la cascara
b     = 1000;                % mm  ancho (por metro)
nc    = 20;                  %     numero de capas de concreto
dz    = t/nc;                % mm  espesor de cada capa (=10)
zc    = -t/2 + dz*((1:nc) - 0.5);   % cota del centro de cada capa
As    = 1000;                % mm2/m  acero por cara
cover = 25;                  % mm  recubrimiento
zs    = t/2 - cover;         % mm  brazo del acero (=75)

fprintf('=== Shell Damage — seccion de cascara RC por capas ===\n\n');
fprintf('t = %.0f mm, b = %.0f mm, nc = %d capas (dz = %.0f mm)\n', t, b, nc, dz);
fprintf('As = %.0f mm2/m por cara, zs = %.0f mm\n', As, zs);
fprintf('E = %.0f, fc = %.0f, ft = %.0f, Z = %.2f | Es = %.0f, fy = %.0f\n\n', ...
        E, fc, ft, Z, Es, fy);

%% Barrido de curvatura kappa con un LOOP (200 puntos, de 0 a 4e-5)
Nk   = 200;
kap  = linspace(0, 4e-5, Nk);   % 1/mm
Mv   = zeros(1, Nk);            % momento  [kN*m/m]
dcv  = zeros(1, Nk);            % dano en la fibra de compresion [-]
e0v  = zeros(1, Nk);            % deformacion del centroide [-]

for j = 1:Nk
    k = kap(j);

    % --- 1) Equilibrio axial N(e0)=0 por BISECCION en e0 ---
    lo = -0.01;  hi = 0.01;
    Nlo = axial_force(lo, k, E, fc, ft, ec0, et0, etf, Z, Es, fy, b, dz, zc, As, zs);
    for it = 1:60
        mid = 0.5*(lo + hi);
        Nmid = axial_force(mid, k, E, fc, ft, ec0, et0, etf, Z, Es, fy, b, dz, zc, As, zs);
        if Nlo*Nmid <= 0
            hi = mid;
        else
            lo = mid;  Nlo = Nmid;
        end
    end
    e0 = 0.5*(lo + hi);
    e0v(j) = e0;

    % --- 2) Momento M(kappa) = momentos de capas + acero ---
    M = 0;
    for i = 1:nc
        ei = e0 + k*zc(i);
        M  = M + sc(ei, E, fc, ft, ec0, et0, etf, Z) * b * dz * zc(i);
    end
    M = M + ss(e0 + k*zs, Es, fy) * As *   zs ...
          + ss(e0 - k*zs, Es, fy) * As * (-zs);
    Mv(j) = M / 1e6;              % N*mm -> kN*m

    % --- 3) Dano en la fibra mas comprimida ---
    dcv(j) = dmg(e0 - k*t/2, E, fc, ft, ec0, et0, etf, Z);
end

%% Momento pico
[Mpk, jpk] = max(Mv);
fprintf('Momento pico  M = %.2f kN*m/m  en kappa = %.3e 1/mm\n', Mpk, kap(jpk));
fprintf('Momento final M = %.2f kN*m/m  en kappa = %.3e 1/mm (softening)\n\n', ...
        Mv(end), kap(end));

%% Verificacion contra la tabla validada
fprintf('=== Verificacion (interpolado a kappa de la tabla) ===\n');
fprintf('  kappa(1/mm)   M(kN*m/m)   d_comp\n');
chk = [5.0e-6, 1.0e-5, 1.5e-5, 1.64e-5, 2.0e-5, 3.0e-5, 4.0e-5];
for q = 1:numel(chk)
    Mq = interp1(kap, Mv,  chk(q));
    dq = interp1(kap, dcv, chk(q));
    fprintf('   %8.3e   %8.2f   %5.3f\n', chk(q), Mq, dq);
end
fprintf('\n');

%% Grafica 1 — Momento-curvatura M(kappa) coloreado por dano (jet_r)
figure;
scatter(kap*1e6, Mv, 22, dcv, 'filled'); hold on;
plot(kap*1e6, Mv, 'k-', 'LineWidth', 1.0);
colormap(jet_r); cb = colorbar; cb.Label.String = 'dano d_{comp} [-]';
caxis([0 max(dcv)]);
grid on;
xlabel('curvatura  \kappa  [10^{-6} 1/mm]');
ylabel('momento  M  [kN\cdotm/m]');
title('Shell Damage — momento-curvatura M(\kappa)');
% marcar el pico
plot(kap(jpk)*1e6, Mpk, 'ko', 'MarkerFaceColor', 'w', 'MarkerSize', 9);
text(kap(jpk)*1e6, Mpk, sprintf('  pico %.1f kN\\cdotm/m', Mpk), ...
     'VerticalAlignment', 'bottom');

%% Grafica 2 — Dano en la fibra de compresion d_comp(kappa)
figure;
plot(kap*1e6, dcv, 'b-', 'LineWidth', 1.8); hold on;
grid on;
xlabel('curvatura  \kappa  [10^{-6} 1/mm]');
ylabel('dano en compresion  d_{comp}  [-]');
title('Shell Damage — evolucion del dano d_{comp}(\kappa)');
% marcar el dano en el pico de momento
plot(kap(jpk)*1e6, dcv(jpk), 'ro', 'MarkerFaceColor', 'r', 'MarkerSize', 7);
text(kap(jpk)*1e6, dcv(jpk), sprintf('  d=%.3f en pico', dcv(jpk)), ...
     'VerticalAlignment', 'top');

fprintf('Listo — 2 graficas: M(kappa) y d_comp(kappa).\n');

%% -------------------- Funciones constitutivas --------------------
function s = sc(e, E, fc, ft, ec0, et0, etf, Z)
    % Ley del concreto sigma(e) (traccion e>0, compresion e<0)
    if e >= 0
        if e <= et0
            s = E*e;                          % elastico
        else
            s = ft*exp(-(e - et0)/etf);       % softening en traccion
        end
    else
        x = -e;                               % magnitud de compresion
        if x <= ec0
            r = x/ec0;
            s = -fc*(2*r - r^2);              % parabola de Hognestad
        else
            s = -fc*max(0.2, 1 - Z*(x - ec0));% rama descendente + residual
        end
    end
end

function s = ss(e, Es, fy)
    % Acero elasto-plastico
    s = min(max(Es*e, -fy), fy);
end

function di = dmg(e, E, fc, ft, ec0, et0, etf, Z)
    % Dano escalar d(e) = clamp(1 - sigma/(E*e), 0, 1)
    if e == 0
        di = 0;
    else
        di = min(max(1 - sc(e, E, fc, ft, ec0, et0, etf, Z)/(E*e), 0), 1);
    end
end

function N = axial_force(e0, k, E, fc, ft, ec0, et0, etf, Z, Es, fy, b, dz, zc, As, zs)
    % Fuerza axial resultante N(e0) para curvatura k
    N = 0;
    for i = 1:numel(zc)
        N = N + sc(e0 + k*zc(i), E, fc, ft, ec0, et0, etf, Z) * b * dz;
    end
    N = N + ss(e0 + k*zs, Es, fy) * As ...
          + ss(e0 - k*zs, Es, fy) * As;
end
