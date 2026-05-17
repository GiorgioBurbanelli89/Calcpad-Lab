clc;
clear all;

% Datos generales
H = 3.20; % Altura de piso en metros
L = 6.40; % Longitud de cada tramo en metros
N_pisos = 5; % Número de pisos
N_tramos = 3; % Número de tramos
n_soportes = 3;
L_b = L / (n_soportes + 1); % Longitud de la viga en metros

% Propiedades de los materiales (acero ASTM A36)
F_yp = 36.259; % Tensión cedente de la viga principal en ksi
E = 200000; % Módulo de elasticidad en MPa
R_y50 = 1.1;
R_y36 = 1.5;

% Viga a Utilizar: Sección I Soldada
d_b = 32; % Altura de la sección en cm
b_f = 12; % Ancho del ala en cm
t_f = 0.6; % Espesor del ala en cm
t_w = 0.3; % Espesor del alma en cm
r_b = 0; % Radio de curvatura en cm

% Cálculo del área de la sección transversal
h = d_b; % Altura total de la sección en cm
A = 2 * b_f * t_f + (h - 2 * t_f) * t_w; % Área de la sección transversal en cm^2

% Cálculo del momento de inercia
I_x = (b_f * d_b^3 / 12) - ((b_f - t_w) * (d_b - 2 * t_f)^3 / 12); % Momento de inercia respecto al eje x en cm^4
I_y = 2 * (t_f * b_f^3 / 12) + ((h - 2 * t_f) * t_w^3 / 12); % Momento de inercia respecto al eje y en cm^4

% Cálculo del módulo elástico
S_x = I_x / (h / 2); % Módulo elástico respecto al eje x en cm^3
S_y = I_y / (b_f / 2); % Módulo elástico respecto al eje y en cm^3

% Cálculo del módulo plástico
Z_x = b_f * t_f * (d_b - t_f) + (t_w * (d_b - 2 * t_f)^2 / 4); % Módulo plástico respecto al eje x en cm^3
Z_y = t_f * b_f^2 / 2 + (t_w^2 * (d_b - 2 * t_f) / 4); % Módulo plástico respecto al eje y en cm^3

% Cálculo de la constante de St. Venant
J = (2 * b_f * t_f^3 / 3) + ((d_b + t_f) * t_w^3 / 3); % Constante de St. Venant en cm^4

% Mostrar los resultados en unidades deseadas
disp(['A = ', num2str(A), ' cm^2']);
disp(['I_x = ', num2str(I_x), ' cm^4']);
disp(['I_y = ', num2str(I_y), ' cm^4']);
disp(['S_x = ', num2str(S_x), ' cm^3']);
disp(['S_y = ', num2str(S_y), ' cm^3']);
disp(['Z_x = ', num2str(Z_x), ' cm^3']);
disp(['Z_y = ', num2str(Z_y), ' cm^3']);
disp(['J = ', num2str(J), ' cm^4']);

% Cálculo de propiedades adicionales
r_xb = sqrt(I_x / A); % Radio de giro en X
r_yb = sqrt(I_y / A); % Radio de giro en Y

h_0 = d_b - t_f; % Distancia entre centros de las alas en cm
k_b = t_f + r_b; % Espesor del ala + curvatura en cm

% Cálculo de la altura libre del alma
h_w = d_b - 2 * k_b; % Altura libre del alma en cm
disp(['h_w = ', num2str(h_w), ' cm']);

% Cálculo de la constante de alabeo
C_w = (I_y * h_0^2) / 4; % Constante de alabeo en cm^6
disp(['C_w = ', num2str(C_w), ' cm^6']);

% Cálculo del radio de giro respecto al eje de torsión
r_ts = sqrt(sqrt(I_y * C_w) / S_x); % Radio de giro respecto al eje de torsión en cm
disp(['r_ts = ', num2str(r_ts), ' cm']);

% 3. Diseño Sismorresistente de Vigas

% 3.1. Revisión del pandeo local de la viga
% Pandeo local ala
lambda_ala = b_f / (2 * t_f); % Esbeltez del ala de la viga
lambda_ala_max_360_22 = 0.38 * sqrt(E / (F_yp * 6.89476)); % Convertir F_yp de ksi a MPa

if lambda_ala < 0.32 * sqrt(E / (R_y36 * F_yp * 6.89476))
    disp('Ala Altamente ductil AISC 341-22');
elseif lambda_ala < lambda_ala_max_360_22
    disp('Ala Compacta AISC360 22');
else
    disp('Ala no compacta AISC 360 22');
end

% Pandeo local alma
lambda_alma = h_w / t_w; % Esbeltez del alma de la viga
lambda_alma_max_360_22 = 3.76 * sqrt(E / (F_yp * 6.89476)); % Convertir F_yp de ksi a MPa

if lambda_alma < 2.57 * sqrt(E / (R_y36 * F_yp * 6.89476))
    disp('Alma Altamente ductil AISC 341-22');
elseif lambda_alma < lambda_alma_max_360_22
    disp('Alma Compacta AISC360 22');
else
    disp('Alma no compacta AISC 360 22');
end

% 3.2. Revisión de la longitud no arriostrada de la viga
L_b_max = 0.095 * r_yb * sqrt(E / (F_yp * 6.89476)); % Convertir F_yp de ksi a MPa
disp(['L_b_max = ', num2str(L_b_max), ' cm']);

if L_b <= L_b_max
    disp('OK');
else
    disp('No Cumple');
end

% 3.7. Revisión de las vigas por flexión
L_p = 1.76 * r_yb * sqrt(E / (F_yp * 6.89476)); % Convertir F_yp de ksi a MPa
disp(['L_p = ', num2str(L_p), ' cm']);

if L_b <= L_p
    disp('OK');
else
    disp('No Cumple');
end

M_p = Z_x * F_yp * 6.89476; % Convertir F_yp de ksi a MPa
phi = 0.90; % Factor de minoración
phi_M_n = phi * M_p; % Resistencia a flexión en kN*m
disp(['phi_M_n = ', num2str(phi_M_n), ' kN*m']);

% 3.8. Cálculo de L_r
c_v = 1;
L_r = 1.95 * r_ts * (E / (0.7 * F_yp * 6.89476)) * (sqrt((J * c_v) / (S_x * h_0)) + sqrt((J / (S_x * h_0))^2 + 6.76 * (0.7 * F_yp * 6.89476 / E)^2));
disp(['L_r = ', num2str(L_r), ' cm']);

% 4. Cálculo de F_cr
C_b = 1.0; % Asumiendo un valor típico para C_b
F_cr = (C_b * pi^2 * E) / ((L_b / r_ts)^2) * sqrt(1 + 0.078 * (J / (S_x * h_0)) * (L_b / r_ts)^2);
disp(['F_cr = ', num2str(F_cr), ' kgf/cm^2']);

% Definición de la función de resistencia nominal a la flexión M_n en función de la longitud no arriostrada
M_n1 = Z_x * F_yp * 6.89476;
M_n2 = @(L_b) C_b * (M_p - (M_p - 0.7 * F_yp * Z_x * 6.89476) * ((L_b - L_p) / (L_r - L_p)));
M_n3 = @(L_b) ((C_b * pi^2 * E) / ((L_b / r_ts)^2) * sqrt(1 + 0.078 * (J / (S_x * h_0)) * (L_b / r_ts)^2)) * S_x;

disp(['M_n1 = ', num2str(M_n1), ' tonf*m']);

L_n = linspace(0, 20, 100); % Longitudes no arriostradas en cm
M_n = arrayfun(@(L_n) ...
    (L_n <= L_p) * M_n1 + ...
    (L_n > L_p & L_n < L_r) * M_n2(L_n) + ...
    (L_n >= L_r) * M_n3(L_n), ...
    L_n);

% Generación de la gráfica
figure;
hold on;
plot([0 L_p L_r max(L_n)], [M_n1 M_n1 NaN NaN], 'r', 'DisplayName', 'M_n1');
plot([L_p L_r], [M_n1 M_n2(L_r)], 'g', 'DisplayName', 'M_n2');
plot([L_r max(L_n)], [M_n2(L_r) M_n3(max(L_n))], 'b', 'DisplayName', 'M_n3');
plot(L_n, M_n, 'k', 'DisplayName', 'M_n(L_n)');
legend();
xlabel('Longitud no arriostrada (cm)');
ylabel('Resistencia nominal a la flexión (tonf*m)');
title('Gráfica de M_n en función de la longitud no arriostrada');
grid on;
