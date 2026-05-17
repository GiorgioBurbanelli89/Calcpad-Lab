%% DEMO TFM: Análisis dinámico de viga simplemente apoyada
%% Ejercita el motor MATLAB puro v1→v27 end-to-end:
%%   - Sym matrix, expand(), diff(), int(), subs(), dsolve()
%%   - OOP classdef con operator overloading
%%   - Plot Plotly numérico
%%   - ODE/PDE numérico

%% =====================================================================
%% PARTE 1: Ecuación diferencial de viga (Euler-Bernoulli) — simbólico
%% =====================================================================
% EI · y''''(x) = w(x)
% Caso simple: carga distribuida uniforme w₀ sobre viga simplemente apoyada
% Condiciones de contorno: y(0) = y(L) = 0 y M(0) = M(L) = 0
% Solución analítica: y(x) = w₀/(24·EI) · x · (L - x) · (L² + L·x - x²)
%   ó equivalente: y(x) = w₀·x·(L³ - 2·L·x² + x³)/(24·EI)

syms x L EI w0

%% Solución teórica
y_exact = w0 * x * (L^3 - 2*L*x^2 + x^3) / (24*EI)
% Verificación: y(0) = 0
y_at_0 = subs(y_exact, x, 0)                    % → 0
% y(L) = 0
y_at_L = subs(y_exact, x, L)                    % → 0

%% Momentos y cortantes simbólicos
phi = diff(y_exact, x)                          % rotación φ(x) = y'(x)
M = -EI * diff(y_exact, x, 2)                   % momento M(x) = -EI·y''(x)
V = diff(M, x)                                  % cortante V(x) = M'(x)

% Momento máximo en x = L/2
M_max_expr = subs(M, x, L/2)
M_max = expand(M_max_expr)                      % → w0*L²/8 (conocido)

% Flecha máxima en x = L/2
y_max_expr = subs(y_exact, x, L/2)
y_max = expand(y_max_expr)                      % → 5*w0*L⁴/(384*EI)

%% =====================================================================
%% PARTE 2: Caso numérico — Viga de hormigón armado
%% =====================================================================
% Geometría y propiedades
L_val = 6.0                                     % L = 6 m
b = 0.30                                        % b = 30 cm
h = 0.50                                        % h = 50 cm
E_val = 30e9                                    % E = 30 GPa (hormigón)
I_val = b * h^3 / 12                            % I = momento inercia
EI_val = E_val * I_val
w0_val = 15000                                  % w = 15 kN/m

% Sustituir y evaluar (numérico)
y_max_num = subs(y_max, [w0 L EI], [w0_val L_val EI_val])
M_max_num = subs(M_max, [w0 L EI], [w0_val L_val EI_val])

% Resultados ingenieriles
fprintf('====== Viga simplemente apoyada con carga uniforme ======\n')
fprintf('Luz L = %.2f m\n', L_val)
fprintf('Sección b·h = %.2f · %.2f m²\n', b, h)
fprintf('Inercia I = %.4e m^4\n', I_val)
fprintf('Carga w = %.0f N/m\n', w0_val)
fprintf('Flecha máxima δ_max = %.4f mm\n', y_max_num * 1000)
fprintf('Momento máximo M_max = %.2f kN·m\n', M_max_num / 1000)

%% =====================================================================
%% PARTE 3: Plot del perfil deformado
%% =====================================================================
xs = linspace(0, L_val, 100)
% Evaluar y(x) numéricamente para cada x
ys = w0_val * xs .* (L_val^3 - 2*L_val*xs.^2 + xs.^3) / (24 * EI_val) * 1000   % mm

%% Momento M(x)
Ms = w0_val * xs .* (L_val - xs) / 2 / 1000   % kN·m

%% Plot deformada
figure
plot(xs, -ys, 'b-', 'LineWidth', 2)
hold on
plot([0, L_val], [0, 0], 'k--')
xlabel('x (m)')
ylabel('Deformada δ (mm)')
title('Viga simplemente apoyada — Deformada bajo carga uniforme')
grid on

%% =====================================================================
%% PARTE 4: Dinámica — Modo fundamental (ω₁) — usando dsolve simbólico
%% =====================================================================
% Ecuación de movimiento del oscilador equivalente con amortiguamiento:
% m·y'' + c·y' + k·y = 0
% Sub-amortiguado (decaimiento exponencial)
% Para 1er orden equivalente: v' = -ω²·y (mass=1, no damping puro)
% Demo simple: dsolve(-y, y, t) → exp decay con λ=1

syms y t omega_sym
sol_decay = dsolve(-omega_sym*y, y, t, 1)       % y(0) = 1 → exp(-ω·t)

% Period and frequency (analytical from k, m)
m_eq = 1000                                     % masa equivalente kg
k_eq = 48 * EI_val / L_val^3                    % rigidez central simplemente apoyada (carga puntual)
omega_n = sqrt(k_eq / m_eq)                     % rad/s
T_n = 2 * pi / omega_n                          % período (s)
f_n = 1 / T_n                                   % frecuencia (Hz)

fprintf('====== Modo fundamental (viga + masa concentrada en centro) ======\n')
fprintf('Rigidez k = %.0f N/m\n', k_eq)
fprintf('Frecuencia natural ω_n = %.4f rad/s\n', omega_n)
fprintf('Período T_n = %.4f s\n', T_n)
fprintf('Frecuencia f_n = %.4f Hz\n', f_n)

%% Plot del modo: y(t) = exp(-ξ·ω·t) · cos(ω_d·t)
ts = linspace(0, 4*T_n, 200)
zeta = 0.05                                     % ratio de amortiguamiento (5%)
omega_d = omega_n * sqrt(1 - zeta^2)            % freq amortiguada
y_t = exp(-zeta * omega_n * ts) .* cos(omega_d * ts)

figure
plot(ts, y_t, 'r-', 'LineWidth', 1.5)
hold on
plot(ts, exp(-zeta * omega_n * ts), 'k--')
plot(ts, -exp(-zeta * omega_n * ts), 'k--')
xlabel('Tiempo (s)')
ylabel('Desplazamiento normalizado')
title('Respuesta libre amortiguada — viga primer modo (ξ=5%)')
grid on

%% =====================================================================
%% PARTE 5: OOP — Encapsulando viga como clase
%% =====================================================================
classdef Viga
    properties
        L = 1
        EI = 1
        w = 0
    end
    methods
        function obj = Viga(L_, EI_, w_)
            obj.L = L_;
            obj.EI = EI_;
            obj.w = w_;
        end
        function d = flecha_max(obj)
            d = 5 * obj.w * obj.L^4 / (384 * obj.EI);
        end
        function m = momento_max(obj)
            m = obj.w * obj.L^2 / 8;
        end
        function v = cortante_max(obj)
            v = obj.w * obj.L / 2;
        end
    end
    methods (Static)
        function obj = standard()
            obj = Viga(6.0, 30e9 * 0.30 * 0.50^3 / 12, 15000);
        end
    end
end

viga = Viga.standard()
delta = viga.flecha_max() * 1000                % mm
Mm = viga.momento_max() / 1000                  % kN·m
Vm = viga.cortante_max() / 1000                 % kN

fprintf('====== OOP: clase Viga ======\n')
fprintf('Flecha δ = %.4f mm\n', delta)
fprintf('M_max = %.2f kN·m\n', Mm)
fprintf('V_max = %.2f kN\n', Vm)

%% =====================================================================
%% FIN — todas las partes deben coincidir con la teoría clásica
%% =====================================================================
