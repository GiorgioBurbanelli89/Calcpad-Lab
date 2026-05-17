
% Datos de la seccion para la viga IPE 160
ds = 160;  % Altura de la seccion (mm)
bfs = 82;  % Ancho del ala (mm)
tfs = 7.4; % Espesor del ala (mm)
tsw = 5;   % Espesor del alma (mm)
rs = 9;    % Radio de curvatura (mm)
E = 210000; % Modulo de elasticidad (MPa)
Fy = 355;  % Limite de fluencia (MPa)

% Calculo de la inercia respecto al eje fuerte (perfil I = caja hueca con
% dos alas + un alma - restar UNA sola caja interna, sin factor 2 espurio)
I_y = (1/12) * bfs * ds^3 - (1/12) * (bfs - tsw) * (ds - 2*tfs)^3;

% Modulo elastico plastico (Z)
Z = 2 * I_y / ds;

% Constante torsional de St. Venant (J)
J = (1/3) * (2 * bfs * tfs^3 + (ds - 2 * tfs) * tsw^3);

% Radio de giro en X (r_x)
% Area de la seccion: 2 alas (bfs*tfs) + 1 alma ((ds-2*tfs)*tsw)
A_s = 2 * bfs * tfs + (ds - 2 * tfs) * tsw;
r_xs = sqrt(I_y / A_s);

% Radio de giro en Y (r_y) - inercia eje debil del perfil I:
% suma de las dos alas (cada una bfs*tfs respecto a su eje propio) + alma
I_x = 2 * (1/12) * tfs * bfs^3 + (1/12) * (ds - 2*tfs) * tsw^3;
r_ys = sqrt(I_x / A_s);

% Constante de torsion de alabeo (C_w)
h_os = ds - tfs; % Distancia entre centros de las alas
C_ws = tfs * h_os^2 * bfs^3 / 24;

% Calculo de la esbeltez
lambda_f = bfs / (2 * tfs);
lambda_f_max = 0.32 * sqrt(E / Fy);

lambda_w = h_os / tsw;
lambda_w_max = 2.57 * sqrt(E / Fy);

% Calculo de la longitud no arriostrada
N_s = 3; % Numero de soportes laterales
L = 4.8; % Longitud total de la viga (m)
Lb = L / (N_s + 1); % Longitud no arriostrada de la viga (m)
Lb_max = 0.095 * r_xs * sqrt(E / Fy);

% Calculo de la longitud limite de comportamiento plastico
Lp = 1.76 * r_xs * sqrt(E / Fy);

% Calculo del momento plastico
Mp = Z * Fy / 10^3; % kN*m
phi = 0.90; % Factor de minoracion
Mn = Mp; % Resistencia nominal a flexion (kN*m)
phi_Mn = phi * Mn; % Resistencia minorada a flexion (kN*m)

% Resultados
fprintf('Inercia (I_y): %.2f mm^4\n', I_y);
fprintf('Modulo elastico plastico (Z): %.2f mm^3\n', Z);
fprintf('Constante torsional de St. Venant (J): %.2f mm^4\n', J);
fprintf('Radio de giro en X (r_xs): %.3f cm\n', r_xs / 10); % Convertir a cm
fprintf('Radio de giro en Y (r_ys): %.3f cm\n', r_ys / 10); % Convertir a cm
fprintf('Constante de torsion de alabeo (C_ws): %.2f cm^6\n', C_ws / 1000); % Convertir a cm^6
fprintf('Esbeltez del ala de la viga (?_f): %.2f\n', lambda_f);
fprintf('Esbeltez maxima del ala de la viga (?_f_max): %.2f\n', lambda_f_max);
fprintf('Esbeltez del alma de la viga (?_w): %.2f\n', lambda_w);
fprintf('Esbeltez maxima del alma de la viga (?_w_max): %.2f\n', lambda_w_max);
fprintf('Longitud no arriostrada de la viga (Lb): %.2f m\n', Lb);
fprintf('Longitud maxima no arriostrada de la viga (Lb_max): %.2f m\n', Lb_max);
fprintf('Longitud limite de comportamiento plastico (Lp): %.2f m\n', Lp);
fprintf('Momento plastico (Mp): %.2f kN*m\n', Mp);
fprintf('Resistencia nominal a flexion (Mn): %.2f kN*m\n', Mn);
fprintf('Resistencia minorada a flexion (?Mn): %.2f kN*m\n', phi_Mn);
