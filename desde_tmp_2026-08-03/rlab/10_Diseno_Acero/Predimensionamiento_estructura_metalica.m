% Definir las dimensiones del perfil tubular rectangular en milímetros
L1_mm = 300.0;   % Lado exterior del rectángulo grande en dirección X (300 mm)
L2_mm = 310.0;   % Lado exterior del rectángulo grande en dirección Y (310 mm)
t_mm = 3.0;      % Espesor de la pared del tubo (3 mm)

% Propiedades del material
E = 210000.0;    % Módulo de elasticidad en MPa
F_y = 250.0;     % Esfuerzo de fluencia en MPa

% Calcular Momento de inercia respecto al eje X (I_x) en mm?
I_x = (1/12) * (L1_mm * L2_mm^3 - (L1_mm - 2 * t_mm) * (L2_mm - 2 * t_mm)^3);

% Calcular Momento de inercia respecto al eje Y (I_y) en mm?
I_y = (1/12) * (L2_mm * L1_mm^3 - (L2_mm - 2 * t_mm) * (L1_mm - 2 * t_mm)^3);

% Calcular Módulo de sección respecto al eje X (S_x) en mm³
S_x = (1/6) * t_mm * (L1_mm * L2_mm^2 - (L1_mm - 2 * t_mm) * (L2_mm - 2 * t_mm)^2);

% Calcular Módulo de sección respecto al eje Y (S_y) en mm³
S_y = (1/6) * t_mm * (L2_mm * L1_mm^2 - (L2_mm - 2 * t_mm) * (L1_mm - 2 * t_mm)^2);

% Calcular Módulo plástico respecto al eje X (Z_x) en m³
Z_x = I_x / (t_mm / 1000);  % Convertido a m³

% Calcular Módulo plástico respecto al eje Y (Z_y) en m³
Z_y = I_y / (t_mm / 1000);  % Convertido a m³

% Ajustar los resultados según el comentario
I_x_ajustado = I_x / 10000;   % cm?
I_y_ajustado = I_y / 10000;   % cm?
S_x_ajustado = S_x / 1000;    % cm³
S_y_ajustado = S_y / 1000;    % cm³
Z_x_ajustado = Z_x / 1000000; % m³
Z_y_ajustado = Z_y / 1000000; % m³

% Clasificación del ala y alma de la sección en dirección X
lambda_ala_x = L1_mm / t_mm;
lambda_p_ala_x = 1.12 * (E / F_y)^0.5;
lambda_r_ala_x = 1.40 * (E / F_y)^0.5;

if lambda_ala_x <= lambda_p_ala_x
    clasificacion_ala_x = 'Compacta';
elseif lambda_ala_x <= lambda_r_ala_x
    clasificacion_ala_x = 'No Compacta';
else
    clasificacion_ala_x = 'Esbelta';
end

lambda_alma_x = (L2_mm - 2 * t_mm) / t_mm;
lambda_p_alma_x = 2.42 * (E / F_y)^0.5;
lambda_r_alma_x = 5.70 * (E / F_y)^0.5;

if lambda_alma_x <= lambda_p_alma_x
    clasificacion_alma_x = 'Compacta';
elseif lambda_alma_x <= lambda_r_alma_x
    clasificacion_alma_x = 'No Compacta';
else
    clasificacion_alma_x = 'Esbelta';
end

% Clasificación del ala y alma de la sección en dirección Y
lambda_ala_y = L2_mm / t_mm;
lambda_p_ala_y = 1.12 * (E / F_y)^0.5;
lambda_r_ala_y = 1.40 * (E / F_y)^0.5;

if lambda_ala_y <= lambda_p_ala_y
    clasificacion_ala_y = 'Compacta';
elseif lambda_ala_y <= lambda_r_ala_y
    clasificacion_ala_y = 'No Compacta';
else
    clasificacion_ala_y = 'Esbelta';
end

lambda_alma_y = (L1_mm - 2 * t_mm) / t_mm;
lambda_p_alma_y = 2.42 * (E / F_y)^0.5;
lambda_r_alma_y = 5.70 * (E / F_y)^0.5;

if lambda_alma_y <= lambda_p_alma_y
    clasificacion_alma_y = 'Compacta';
elseif lambda_alma_y <= lambda_r_alma_y
    clasificacion_alma_y = 'No Compacta';
else
    clasificacion_alma_y = 'Esbelta';
end

% Mostrar los resultados
disp('Resultados en dirección X:');
disp(['Inercia ajustada en X: ' num2str(I_x_ajustado) ' cm?']);
disp(['Módulo de sección ajustado en X: ' num2str(S_x_ajustado) ' cm³']);
disp(['Módulo plástico ajustado en X: ' num2str(Z_x_ajustado) ' m³']);
disp(['Clasificación del alma en X: ' clasificacion_alma_x]);
disp(['Clasificación del ala en X: ' clasificacion_ala_x]);
disp(' ');

disp('Resultados en dirección Y:');
disp(['Inercia ajustada en Y: ' num2str(I_y_ajustado) ' cm?']);
disp(['Módulo de sección ajustado en Y: ' num2str(S_y_ajustado) ' cm³']);
disp(['Módulo plástico ajustado en Y: ' num2str(Z_y_ajustado) ' m³']);
disp(['Clasificación del alma en Y: ' clasificacion_alma_y]);
disp(['Clasificación del ala en Y: ' clasificacion_ala_y]);