% Parámetros dados (extraídos de la imagen de la hoja de cálculo)
E = 29000; % Módulo de elasticidad del acero en ksi (200 000 MPa)
Fy = 36; % Esfuerzo de fluencia en ksi (248 MPa)
Sx = 47; % Módulo elástico de sección en in^3 (mm^3)
Mp = 16.42; % Momento plástico en kg-cm
Jc = 4280.34; % Constante torsional en cm^4
ho = 30.5; % Distancia entre centros de ala en cm
Cw = 298830; % Constante de pandeo por torsión en cm^6
ry = 4.34; % Radio de giro alrededor del eje y en cm
Cb = 1.0; % Coeficiente de modificación Cb

% Conversiones necesarias
E = E * 1000; % Convertir a MPa
Sx = Sx * 16.387; % Convertir a cm^3
Mp = Mp * 980.665; % Convertir a N-cm

% Cálculo de rts
rts = sqrt(Cw / Sx); % Calculado como r_ts^2 = (I_y * C_w) / S_x

% Cálculo de Lp y Lr usando las fórmulas dadas
Lp = 1.76 * ry * sqrt(E / Fy); % Longitud crítica Lp en cm
Lr = 1.95 * rts * sqrt(E / (0.7 * Fy)) * ...
    sqrt(Jc / (Sx * ho) + sqrt((Jc / (Sx * ho))^2 + 6.76 * (0.7 * Fy / E)^2));

% Generar los datos para el gráfico
Lb = linspace(0, 15, 1000); % Longitud no arriostrada en metros
Mn1 = Mp * ones(size(Lb));
Mn2 = Cb * (Mp - (Mp - 0.7 * Fy * Sx) .* (Lb - Lp) / (Lr - Lp));
Fcr = (Cb * pi^2 * E) ./ ((Lb / rts).^2 .* sqrt(1 + 0.078 * (Jc / (Sx * ho)) * (Lb / rts).^2));
Mn3 = Sx * Fcr;

% Crear el vector Mn utilizando las condiciones dadas
Mn = arrayfun(@(L) ifelse(L <= Lp, Mp, ifelse(L <= Lr, Cb * (Mp - (Mp - 0.7 * Fy * Sx) * (L - Lp) / (Lr - Lp)), Sx * (Cb * pi^2 * E) / ((L / rts)^2 * sqrt(1 + 0.078 * (Jc / (Sx * ho)) * (L / rts)^2)))), Lb);

% Generar el gráfico
figure;
hold on;
plot(Lb(Lb <= Lp), Mn1(Lb <= Lp), 'r', 'LineWidth', 1.5);
plot(Lb(Lb > Lp & Lb <= Lr), Mn2(Lb > Lp & Lb <= Lr), 'g', 'LineWidth', 1.5);
plot(Lb(Lb > Lr), Mn3(Lb > Lr), 'b', 'LineWidth', 1.5);
plot([Lp Lp], [0 Mp], 'k--');
plot([Lr Lr], [0 max(Mn3)], 'k--');
xlabel('Longitud no arriostrada, L_b');
ylabel('Resistencia nominal a la flexión, M_n');
title('Resistencia Nominal a la Flexión vs Longitud no Arriostrada');
legend('Plastic Design', 'Inelastic LTB', 'Elastic LTB', 'L_p', 'L_r', 'Location', 'northeast');
grid on;
hold off;

% Explicación del gráfico en español
disp('Este gráfico muestra la resistencia nominal a la flexión, M_n, en función de la longitud no arriostrada, L_b.');
disp('1. Plastic Design: Para L_b <= L_p, M_n = M_p. Esta es la región de diseño plástico donde no se aplica el pandeo torsional lateral.');
disp('2. Inelastic LTB: Para L_p < L_b <= L_r, se considera el pandeo torsional lateral inelástico, donde M_n disminuye linealmente.');
disp('3. Elastic LTB: Para L_b > L_r, se considera el pandeo torsional lateral elástico, donde M_n se calcula usando la fórmula de pandeo elástico.');
disp('Las líneas verticales punteadas indican las longitudes críticas L_p y L_r.');
