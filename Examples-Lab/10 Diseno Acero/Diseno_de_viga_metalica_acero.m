clc;
clear all;

% ============================================================
%  DISENO DE VIGA DE ACERO - Pandeo Lateral-Torsional (AISC 360 F2)
%  Unidades de entrada: geometria en cm, Fy en ksi, E en MPa.
%
%  Directiva de unidades consistentes (validacion doble):
%  cada resultado clave (Lp, Lr, Mp) se calcula de DOS formas:
%    (A) "con unidades"  -> conversiones explicitas en la formula
%    (B) "sin unidades"  -> todo pre-convertido a un sistema
%        consistente (N, mm, MPa) y formulas SIN factores.
%  Ambas deben coincidir (diferencia ~ 0).
% ============================================================

% Datos generales
H = 3.20;                    % Altura de piso (m)
L = 6.40;                    % Longitud de cada tramo (m)
N_pisos = 5;
N_tramos = 3;
n_soportes = 3;
L_b = L / (n_soportes + 1);  % Longitud no arriostrada (m)
L_b_cm = L_b * 100;          % misma longitud en cm (para comparar con Lp, Lr)

% Propiedades del material (acero ASTM A36)
F_yp = 36.259;               % Fy de la viga principal (ksi)
E = 200000;                  % Modulo de elasticidad (MPa)
ksi_a_MPa = 6.89476;         % factor 1 ksi = 6.89476 MPa
Fy = F_yp * ksi_a_MPa;       % Fy en MPa (~250 MPa) -> se usa de aqui en adelante
R_y50 = 1.1;
R_y36 = 1.5;

% Viga a utilizar: Seccion I soldada (cm)
d_b = 32;                    % Altura de la seccion (cm)
b_f = 12;                    % Ancho del ala (cm)
t_f = 0.6;                   % Espesor del ala (cm)
t_w = 0.3;                   % Espesor del alma (cm)
r_b = 0;                     % Radio de curvatura (cm)

% Area de la seccion transversal (cm^2)
h = d_b;
A = 2 * b_f * t_f + (h - 2 * t_f) * t_w;

% Momentos de inercia (cm^4)
I_x = (b_f * d_b^3 / 12) - ((b_f - t_w) * (d_b - 2 * t_f)^3 / 12);
I_y = 2 * (t_f * b_f^3 / 12) + ((h - 2 * t_f) * t_w^3 / 12);

% Modulos elasticos (cm^3)
S_x = I_x / (h / 2);
S_y = I_y / (b_f / 2);

% Modulos plasticos (cm^3)
Z_x = b_f * t_f * (d_b - t_f) + (t_w * (d_b - 2 * t_f)^2 / 4);
Z_y = t_f * b_f^2 / 2 + (t_w^2 * (d_b - 2 * t_f) / 4);

% Constante torsional de St. Venant (cm^4)
J = (2 * b_f * t_f^3 / 3) + ((d_b + t_f) * t_w^3 / 3);

disp(['A = ', num2str(A), ' cm^2']);
disp(['I_x = ', num2str(I_x), ' cm^4']);
disp(['I_y = ', num2str(I_y), ' cm^4']);
disp(['S_x = ', num2str(S_x), ' cm^3']);
disp(['S_y = ', num2str(S_y), ' cm^3']);
disp(['Z_x = ', num2str(Z_x), ' cm^3']);
disp(['Z_y = ', num2str(Z_y), ' cm^3']);
disp(['J = ', num2str(J), ' cm^4']);

% Propiedades adicionales
r_xb = sqrt(I_x / A);        % Radio de giro en X (cm)
r_yb = sqrt(I_y / A);        % Radio de giro en Y (cm)

h_0 = d_b - t_f;             % Distancia entre centros de las alas (cm)
k_b = t_f + r_b;             % Espesor del ala + curvatura (cm)

h_w = d_b - 2 * k_b;         % Altura libre del alma (cm)
disp(['h_w = ', num2str(h_w), ' cm']);

C_w = (I_y * h_0^2) / 4;     % Constante de alabeo (cm^6)
disp(['C_w = ', num2str(C_w), ' cm^6']);

% Radio de giro efectivo para LTB (cm): r_ts^2 = sqrt(I_y*C_w)/S_x
r_ts = sqrt(sqrt(I_y * C_w) / S_x);
disp(['r_ts = ', num2str(r_ts), ' cm']);

% 3. Diseno Sismorresistente de Vigas
% 3.1. Pandeo local (Fy ya esta en MPa, mismas unidades que E)
lambda_ala = b_f / (2 * t_f);
lambda_ala_max_360_22 = 0.38 * sqrt(E / Fy);

if lambda_ala < 0.32 * sqrt(E / (R_y36 * Fy))
    disp('Ala Altamente ductil AISC 341-22');
elseif lambda_ala < lambda_ala_max_360_22
    disp('Ala Compacta AISC360 22');
else
    disp('Ala no compacta AISC 360 22');
end

lambda_alma = h_w / t_w;
lambda_alma_max_360_22 = 3.76 * sqrt(E / Fy);

if lambda_alma < 2.57 * sqrt(E / (R_y36 * Fy))
    disp('Alma Altamente ductil AISC 341-22');
elseif lambda_alma < lambda_alma_max_360_22
    disp('Alma Compacta AISC360 22');
else
    disp('Alma no compacta AISC 360 22');
end

% 3.2. Limite de arriostramiento (AISC 341, alta ductilidad): 0.095*ry*E/Fy
%      (E/Fy adimensional, r_yb en cm -> L_b_max en cm)
L_b_max = 0.095 * r_yb * (E / Fy);
disp(['L_b_max = ', num2str(L_b_max), ' cm']);
if L_b_cm <= L_b_max
    disp('OK');
else
    disp('No Cumple');
end

% ============================================================
% 3.7 - 3.8  Lp, Lr y Mp  --  VALIDACION con vs sin unidades
% ============================================================
c_v = 1;

% ---- Way A: "con unidades" (conversiones explicitas), longitudes en cm ----
Lp_A = 1.76 * r_yb * sqrt(E / Fy);                        % cm
t1_A = (J * c_v) / (S_x * h_0);                           % adimensional
t2_A = 6.76 * (0.7 * Fy / E)^2;                           % adimensional
% AISC F2-6: UN solo sqrt EXTERIOR que envuelve a t1 + sqrt(t1^2 + t2)
Lr_A = 1.95 * r_ts * (E / (0.7 * Fy)) * sqrt(t1_A + sqrt(t1_A^2 + t2_A));   % cm
% Z_x[cm^3] * Fy[MPa] = N*m ; *1e-3 -> kN*m
Mp_A = Z_x * Fy * 1e-3;                                   % kN*m

% ---- Way B: "sin unidades" (sistema consistente N-mm-MPa, sin factores) ----
r_yb_mm = r_yb * 10;   r_ts_mm = r_ts * 10;   h0_mm = h_0 * 10;      % cm -> mm
S_x_mm3 = S_x * 1e3;   Z_x_mm3 = Z_x * 1e3;   J_mm4 = J * 1e4;       % cm^n -> mm^n
% E y Fy ya estan en MPa = N/mm^2 (consistentes con mm y N)
Lp_B_mm = 1.76 * r_yb_mm * sqrt(E / Fy);
t1_B = J_mm4 / (S_x_mm3 * h0_mm);
t2_B = 6.76 * (0.7 * Fy / E)^2;
Lr_B_mm = 1.95 * r_ts_mm * (E / (0.7 * Fy)) * sqrt(t1_B + sqrt(t1_B^2 + t2_B));
Mp_B_Nmm = Z_x_mm3 * Fy;                                  % N*mm
Lp_B = Lp_B_mm / 10;                                      % mm -> cm
Lr_B = Lr_B_mm / 10;                                      % mm -> cm
Mp_B = Mp_B_Nmm / 1e6;                                    % N*mm -> kN*m (1 kN*m = 1e6 N*mm)

% ---- Resultados adoptados ----
L_p = Lp_A;
L_r = Lr_A;
M_p = Mp_A;
phi = 0.90;
phi_M_n = phi * M_p;

disp('--- Validacion: con unidades  vs  sin unidades (mismo sistema) ---');
disp(['Lp: con = ', num2str(Lp_A), ' cm | sin = ', num2str(Lp_B), ' cm | dif = ', num2str(abs(Lp_A - Lp_B))]);
disp(['Lr: con = ', num2str(Lr_A), ' cm | sin = ', num2str(Lr_B), ' cm | dif = ', num2str(abs(Lr_A - Lr_B))]);
disp(['Mp: con = ', num2str(Mp_A), ' kN*m | sin = ', num2str(Mp_B), ' kN*m | dif = ', num2str(abs(Mp_A - Mp_B))]);
disp(['Chequeo fisico Lp < Lr : ', num2str(L_p < L_r)]);
disp(['L_p = ', num2str(L_p), ' cm']);
disp(['L_r = ', num2str(L_r), ' cm']);
disp(['M_p = ', num2str(M_p), ' kN*m']);
disp(['phi_M_n = ', num2str(phi_M_n), ' kN*m']);

if L_b_cm <= L_p
    disp('L_b <= L_p : diseno plastico (Mn = Mp)');
else
    disp('L_b > L_p : rige pandeo lateral-torsional');
end

% ============================================================
% 4. Curva M_n(L_b): las tres regiones en orden Lp < Lr
%    Momentos en kN*m, longitudes en cm.
% ============================================================
C_b = 1.0;                           % Coeficiente de modificacion (conservador)
Mr = 0.7 * Fy * S_x * 1e-3;          % Momento en L_r (kN*m)

Lb_plot = linspace(1, 1.3 * L_r, 400);   % cm (desde 1: Fcr es singular en Lb=0)

% Region 1 - plastica: Mn = Mp
MnP_full = M_p * ones(size(Lb_plot));
% Region 2 - LTB inelastico: recta de Mp a Mr entre Lp y Lr
MnI_full = C_b * (M_p - (M_p - Mr) .* (Lb_plot - L_p) / (L_r - L_p));
% Region 3 - LTB elastico: Fcr[MPa] * S_x[cm^3] * 1e-3 -> kN*m
%   (el sqrt MULTIPLICA, no divide)
Fcr_full = C_b * pi^2 * E ./ (Lb_plot / r_ts).^2 .* sqrt(1 + 0.078 * (J / (S_x * h_0)) * (Lb_plot / r_ts).^2);
MnE_full = Fcr_full * S_x * 1e-3;

mP = Lb_plot <= L_p;
mI = Lb_plot > L_p & Lb_plot <= L_r;
mE = Lb_plot > L_r;

figure;
hold on;
plot(Lb_plot(mP), MnP_full(mP), 'r', 'LineWidth', 1.5);
plot(Lb_plot(mI), MnI_full(mI), 'g', 'LineWidth', 1.5);
plot(Lb_plot(mE), MnE_full(mE), 'b', 'LineWidth', 1.5);
plot([L_p L_p], [0 M_p], 'k--');
plot([L_r L_r], [0 M_p], 'k--');
text(L_p * 0.15, M_p, ' Plastic Design', 'Color', 'r');
text((L_p + L_r) / 2, M_p * 0.92, ' Inelastic LTB', 'Color', [0 0.6 0]);
text(L_r * 1.02, Mr * 0.85, ' Elastic LTB', 'Color', 'b');
text(L_p, 0, ' L_p', 'Color', 'k');
text(L_r, 0, ' L_r', 'Color', 'k');
xlabel('Longitud no arriostrada L_b (cm)');
ylabel('Resistencia nominal a la flexion M_n (kN*m)');
title('M_n en funcion de la longitud no arriostrada (AISC F2)');
grid on;
hold off;
