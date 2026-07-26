%% Cyclic Damage — concreto ciclico no lineal (histeresis, elasticidad danada)
% -------------------------------------------------------------------------
% Mismo modelo de concrete_damage (elasticidad danada  sigma=(1-d)*E*e) pero
% con el DANO como ESTADO PERSISTENTE (maximo historico). Al aplicar un
% protocolo de reversas de deformacion aparecen los LAZOS DE HISTERESIS:
% cada descarga/recarga viaja sobre la SECANTE (1-d)*E hacia el origen, con
% el dano d CONGELADO del maximo historico. La rigidez se degrada (secante
% mas plana) cada ciclo y el dano SOLO crece. Reproduce ASDConcrete1D de
% ASDEA/STKO (elasticidad danada secante, SIN deformacion plastica residual).
%
% Convencion: e > 0 = traccion, e < 0 = compresion.
%
% Concreto (MISMO modelo que concrete_damage):
%   E=30000 MPa, fc=30, ec0=0.0020, ecu=0.0035, ft=3, et0=ft/E=0.0001,
%   etf=0.0005, Z=(1-0.2)/(ecu-ec0)=533.33
%   Envelope sc(e): traccion elastica hasta et0, luego ft*exp(-(e-et0)/etf);
%                   compresion (x=-e) parabola -fc*(2r-r^2) hasta ec0, luego
%                   -fc*max(0.2, 1-Z*(x-ec0)).
%   Dano secante del envelope: dmg(e)=clamp(1 - sc(e)/(E*e), 0, 1).
%
% MAQUINA DE ESTADO (loop sobre el protocolo, estado persistente):
%   etmax = max ε>0 historico (traccion),  ecmax = max |ε| en compresion.
%   NO se resetean. En cada paso con ε:
%     si ε >= 0:  etmax = max(etmax, ε);   dt = dmg(+etmax);  σ = (1-dt)*E*ε
%     si ε <  0:  ecmax = max(ecmax, -ε);  dc = dmg(-ecmax);  σ = (1-dc)*E*ε
%   CIERRE DE FISURA: cada rama usa SU propio dano, asi que al invertir la
%   rigidez recupera; descarga y recarga quedan sobre la misma secante.
%
% Protocolo de deformacion (reversas de amplitud creciente):
%   0 -> -0.0006 -> 0 -> -0.0012 -> 0 -> -0.0020 -> 0 -> -0.0030 -> 0
%     -> +0.0001 -> 0 -> -0.0040
%
% Valores VALIDADOS en los picos (deben coincidir):
%    e (pico)   sigma(MPa)    d
%   -0.0006      -15.30      0.150
%   -0.0012      -25.20      0.300
%   -0.0020      -30.00      0.500  (pico fc)
%   -0.0030      -14.00      0.844
%   -0.0040       -6.00      0.950  (residual 0.2*fc)
%   +0.0001        3.00      0.000  (dt, traccion)
% -------------------------------------------------------------------------
clear; clc; close all;

%% Propiedades del material (identicas a concrete_damage)
E   = 30000;    % MPa   modulo elastico
fc  = 30;       % MPa   resistencia a compresion
ft  = 3;        % MPa   resistencia a traccion
ec0 = 0.0020;   %       deformacion pico en compresion
ecu = 0.0035;   %       deformacion ultima en compresion
et0 = ft/E;     % = 0.0001
etf = 0.0005;   %       softening en traccion
Z   = (1 - 0.2)/(ecu - ec0);   % = 533.33

fprintf('=== Cyclic Damage — concreto ciclico (elasticidad danada) ===\n\n');
fprintf('E = %.0f MPa, fc = %.0f MPa, ft = %.0f MPa\n', E, fc, ft);
fprintf('ec0 = %.4f, ecu = %.4f, et0 = %.4f, etf = %.4f, Z = %.2f\n\n', ...
        ec0, ecu, et0, etf, Z);

%% Protocolo de reversas: puntos de esquina y subpasos por segmento
pts = [0, -0.0006, 0, -0.0012, 0, -0.0020, 0, -0.0030, 0, 0.0001, 0, -0.0040];
nseg = 40;                     % subpasos lineales por segmento

% Construir el vector de deformacion eps recorriendo cada segmento
eps = pts(1);
for s = 1:numel(pts)-1
    seg = linspace(pts(s), pts(s+1), nseg+1);
    eps = [eps, seg(2:end)];   % sin repetir la esquina
end
Ns = numel(eps);

fprintf('Protocolo: %d esquinas, %d subpasos/segmento -> %d pasos\n\n', ...
        numel(pts), nseg, Ns);

%% Loop de la maquina de estado con dano PERSISTENTE (etmax / ecmax)
sig = zeros(1, Ns);    % esfuerzo  sigma [MPa]
dtv = zeros(1, Ns);    % dano de traccion  dt (persistente)
dcv = zeros(1, Ns);    % dano de compresion dc (persistente)
dav = zeros(1, Ns);    % dano de la rama ACTIVA (para colorear)

etmax = 0;             % max deformacion de traccion historica
ecmax = 0;             % max magnitud de compresion historica

for k = 1:Ns
    ek = eps(k);
    if ek >= 0
        % --- Rama de TRACCION ---
        etmax = max(etmax, ek);
        dt    = dmg(+etmax, E, fc, ft, ec0, et0, etf, Z);
        s     = (1 - dt) * E * ek;          % secante danada al origen
        dav(k) = dt;
    else
        % --- Rama de COMPRESION ---
        ecmax = max(ecmax, -ek);
        dc    = dmg(-ecmax, E, fc, ft, ec0, et0, etf, Z);
        s     = (1 - dc) * E * ek;          % secante danada al origen
        dav(k) = dc;
    end
    sig(k) = s;
    dtv(k) = dmg(+etmax, E, fc, ft, ec0, et0, etf, Z);   % dt actual
    dcv(k) = dmg(-ecmax, E, fc, ft, ec0, et0, etf, Z);   % dc actual
end

%% Verificacion en los PICOS del protocolo (deben coincidir con la tabla)
fprintf('=== Verificacion en los picos del protocolo ===\n');
fprintf('   e (pico)   sigma(MPa)     d\n');
peaks = [-0.0006, -0.0012, -0.0020, -0.0030, 0.0001, -0.0040];
% Recorrido acumulado del maximo historico para evaluar d en cada pico
etm = 0;  ecm = 0;
for q = 1:numel(peaks)
    ep = peaks(q);
    if ep >= 0
        etm = max(etm, ep);
        dp  = dmg(+etm, E, fc, ft, ec0, et0, etf, Z);
    else
        ecm = max(ecm, -ep);
        dp  = dmg(-ecm, E, fc, ft, ec0, et0, etf, Z);
    end
    sp = sc(ep, E, fc, ft, ec0, et0, etf, Z);   % en el pico: sobre el envelope
    fprintf('  %+8.5f   %8.3f   %6.3f\n', ep, sp, dp);
end
fprintf('\n');
fprintf('Dano final:  dt = %.3f (traccion),  dc = %.3f (compresion)\n\n', ...
        dtv(end), dcv(end));

%% Grafica 1 — HISTERESIS sigma vs eps (lazos de descarga/recarga secante)
figure;
plot(eps*1000, sig, 'b-', 'LineWidth', 1.2); hold on;   % los LAZOS
scatter(eps*1000, sig, 20, dav, 'filled');              % coloreado por dano
colormap(jet_r); cb = colorbar; cb.Label.String = 'dano rama activa [-]';
caxis([0 1]);
grid on;
xlabel('deformacion  \epsilon  [milesimas]');
ylabel('esfuerzo  \sigma  [MPa]');
title('Cyclic Damage — histeresis \sigma(\epsilon) con secante degradada');
% marcar los picos de compresion sobre el envelope
pk = [-0.0006 -15.30; -0.0012 -25.20; -0.0020 -30.00; ...
      -0.0030 -14.00; -0.0040 -6.00];
plot(pk(:,1)*1000, pk(:,2), 'ko', 'MarkerFaceColor', 'w', 'MarkerSize', 7);
plot(0.0001*1000, 3.00, 'ks', 'MarkerFaceColor', 'w', 'MarkerSize', 7);
text(-2.0, -30, '  pico fc (-30 MPa)', 'VerticalAlignment', 'top');
text(-4.0,  -6, '  residual (-6 MPa)', 'VerticalAlignment', 'bottom');

%% Grafica 2 — Degradacion del dano dt y dc vs paso (persistente, solo crece)
figure;
plot(1:Ns, dcv, 'r-', 'LineWidth', 1.8); hold on;
plot(1:Ns, dtv, 'b-', 'LineWidth', 1.8);
grid on;
xlabel('paso del protocolo  k');
ylabel('dano  d  [-]');
title('Cyclic Damage — degradacion persistente d_c y d_t');
legend('d_c (compresion)', 'd_t (traccion)', 'Location', 'northwest');
ylim([0 1.05]);
% marcar el dano de compresion en cada pico
plot(Ns, dcv(end), 'ro', 'MarkerFaceColor', 'r', 'MarkerSize', 7);
text(Ns, dcv(end), sprintf('  d_c=%.3f', dcv(end)), ...
     'VerticalAlignment', 'top', 'HorizontalAlignment', 'right');

fprintf('Listo — 2 graficas: histeresis sigma(eps) y degradacion d_c/d_t.\n');

%% -------------------- Funciones constitutivas --------------------
function s = sc(e, E, fc, ft, ec0, et0, etf, Z)
    % Envelope del concreto sigma(e) (traccion e>0, compresion e<0)
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

function di = dmg(e, E, fc, ft, ec0, et0, etf, Z)
    % Dano secante del envelope  d(e) = clamp(1 - sc(e)/(E*e), 0, 1)
    if e == 0
        di = 0;
    else
        di = min(max(1 - sc(e, E, fc, ft, ec0, et0, etf, Z)/(E*e), 0), 1);
    end
end
