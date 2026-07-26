%% Concrete Damage — modelo uniaxial de elasticidad danada (CDP escalar)
% -------------------------------------------------------------------------
% Ley constitutiva del concreto con dano escalar d(e) en [0,1]:
%
%       sigma(e) = (1 - d(e)) * E * e
%
% El dano d crece cuando el material se ablanda (softening), tanto en
% traccion (fisuracion) como en compresion (aplastamiento).
%
% Convencion: e > 0 = traccion, e < 0 = compresion.
%
% Propiedades del concreto:
%   E    = 30000 MPa    modulo elastico
%   fc   = 30 MPa       resistencia a compresion    (ec0 = 0.0020, ecu = 0.0035)
%   ft   = 3  MPa       resistencia a traccion      (et0 = ft/E = 0.0001)
%   etf  = 0.0005       parametro de softening en traccion
%   Z    = (1-0.2)/(ecu-ec0) = 533.33  pendiente descendente en compresion
%
% Ley sigma(e):
%   Traccion (e >= 0):
%       e <= et0 : sigma = E*e                        (elastico)
%       e >  et0 : sigma = ft*exp(-(e-et0)/etf)       (softening exponencial)
%   Compresion (e < 0), con x = -e (magnitud):
%       x <= ec0 : sigma = -fc*(2*(x/ec0) - (x/ec0)^2)      (parabola Hognestad)
%       x >  ec0 : sigma = -fc*max(0.2, 1 - Z*(x-ec0))      (residual 0.2*fc)
%
% Dano escalar:  d(e) = clamp(1 - sigma/(E*e), 0, 1),  d(0) = 0
%
% Puntos pico (validados):
%   pico traccion   : e=+0.0001  sigma=+3   MPa  d=0.000
%   pico compresion : e=-0.0020  sigma=-30  MPa  d=0.500
%   ecu (residual)  : e=-0.0035  sigma=-6   MPa  d=0.943
% -------------------------------------------------------------------------
clear; clc; close all;

%% Propiedades del material
E   = 30000;    % MPa
fc  = 30;       % MPa
ft  = 3;        % MPa
ec0 = 0.0020;   % deformacion pico en compresion
ecu = 0.0035;   % deformacion ultima en compresion
et0 = ft/E;     % = 0.0001, deformacion pico en traccion
etf = 0.0005;   % softening en traccion
Z   = (1 - 0.2)/(ecu - ec0);   % = 533.33

fprintf('=== Concrete Damage — elasticidad danada (CDP escalar) ===\n\n');
fprintf('E = %.0f MPa, fc = %.0f MPa, ft = %.0f MPa\n', E, fc, ft);
fprintf('ec0 = %.4f, ecu = %.4f, et0 = %.4f, etf = %.4f, Z = %.2f\n\n', ...
        ec0, ecu, et0, etf, Z);

%% Barrido de deformacion e con un LOOP (200 puntos, de -0.0035 a +0.0006)
N   = 200;
e   = linspace(-0.0035, 0.0006, N);
sig = zeros(1, N);      % sigma(e)  [MPa]
d   = zeros(1, N);      % dano d(e) [-]

for i = 1:N
    ei = e(i);
    if ei >= 0
        % --- Traccion ---
        if ei <= et0
            s = E*ei;                          % elastico
        else
            s = ft*exp(-(ei - et0)/etf);       % softening exponencial
        end
    else
        % --- Compresion --- (x = magnitud)
        x = -ei;
        if x <= ec0
            r = x/ec0;
            s = -fc*(2*r - r^2);               % parabola ascendente
        else
            s = -fc*max(0.2, 1 - Z*(x - ec0)); % rama descendente + residual
        end
    end
    sig(i) = s;

    % Dano escalar d = 1 - sigma/(E*e), con d(0)=0
    if ei == 0
        di = 0;
    else
        di = 1 - s/(E*ei);
    end
    d(i) = min(max(di, 0), 1);   % clamp a [0,1]
end

%% Verificacion de los valores pico (deben coincidir con la tabla)
fprintf('=== Valores de referencia ===\n');
fprintf('   e         sigma(MPa)    d\n');
chk = [0.0001; 0.0003; 0.0010; -0.0010; -0.0020; -0.0030; -0.0035];
for k = 1:numel(chk)
    ek = chk(k);
    if ek >= 0
        if ek <= et0, sk = E*ek; else, sk = ft*exp(-(ek-et0)/etf); end
    else
        xk = -ek;
        if xk <= ec0, rk = xk/ec0; sk = -fc*(2*rk - rk^2);
        else, sk = -fc*max(0.2, 1 - Z*(xk-ec0)); end
    end
    if ek == 0, dk = 0; else, dk = min(max(1 - sk/(E*ek),0),1); end
    fprintf('  %+8.5f   %8.3f   %5.3f\n', ek, sk, dk);
end
fprintf('\n');

%% Grafica 1 — Curva constitutiva sigma vs e (coloreada por dano, jet_r)
figure;
scatter(e*1000, sig, 24, d, 'filled'); hold on;
plot(e*1000, sig, 'k-', 'LineWidth', 1.0);
colormap(jet_r); cb = colorbar; cb.Label.String = 'dano d [-]';
caxis([0 1]);
grid on;
xlabel('deformacion  \epsilon  [milesimas]');
ylabel('esfuerzo  \sigma  [MPa]');
title('Concrete Damage — ley constitutiva \sigma(\epsilon)');
% marcar picos
plot(-0.0020*1000, -30, 'ko', 'MarkerFaceColor', 'w', 'MarkerSize', 8);
plot( 0.0001*1000,   3, 'ko', 'MarkerFaceColor', 'w', 'MarkerSize', 8);
text(-2.0, -30, '  pico compresion (-30 MPa)', 'VerticalAlignment', 'top');
text( 0.1,   3, '  pico traccion (3 MPa)');

%% Grafica 2 — Evolucion del dano d vs e
figure;
plot(e*1000, d, 'b-', 'LineWidth', 1.8); hold on;
grid on;
xlabel('deformacion  \epsilon  [milesimas]');
ylabel('dano  d  [-]');
title('Concrete Damage — evolucion del dano d(\epsilon)');
ylim([0 1.05]);
% marcar dano en ecu
plot(-0.0035*1000, 0.943, 'ro', 'MarkerFaceColor', 'r', 'MarkerSize', 7);
text(-3.5, 0.943, '  d=0.943 en \epsilon_{cu}', 'VerticalAlignment', 'bottom');

fprintf('Listo — 2 graficas: sigma(e) y d(e).\n');
