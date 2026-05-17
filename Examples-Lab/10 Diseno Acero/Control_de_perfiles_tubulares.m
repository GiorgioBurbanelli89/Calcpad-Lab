clc;
clear all;

% Datos proporcionados
Hc = 304.8;  % Altura de la sección en mm
Bc = 304.8;  % Ancho del perfil en mm
t = 14.76;   % Espesor del perfil en mm
R = 0;       % Radio de esquina interno (suponiendo esquinas afiladas)

% Calcular el área de la sección transversal
A = (Hc * Bc) - ((Hc - 2 * t) * (Bc - 2 * t));

% Calcular el momento de inercia respecto a los ejes X e Y
Ix = (1/12) * (Bc * Hc^3 - (Bc - 2 * t) * (Hc - 2 * t)^3);
Iy = Ix;  % Debido a la simetría

% Calcular el módulo de sección
Sx = Ix / (Hc / 2);
Sy = Iy / (Bc / 2);

% Calcular el módulo plástico
Zx = A * Hc / 4;
Zy = Zx;  % Debido a la simetría

% Convertir a cm², cm^4 y cm³
A_cm2 = A / 100;         % Área en cm²
Ix_cm4 = Ix / 10000;     % Momento de inercia en cm^4
Sx_cm3 = Sx / 1000;      % Módulo de sección en cm³
Zx_cm3 = Zx / 1000;      % Módulo plástico en cm³

% Mostrar resultados
fprintf('Área (cm²): %.2f\n', A_cm2);
fprintf('Momento de inercia Ix (cm^4): %.2f\n', Ix_cm4);
fprintf('Momento de inercia Iy (cm^4): %.2f\n', Ix_cm4);
fprintf('Módulo de sección Sx (cm³): %.2f\n', Sx_cm3);
fprintf('Módulo de sección Sy (cm³): %.2f\n', Sx_cm3);
fprintf('Módulo plástico Zx (cm³): %.2f\n', Zx_cm3);
fprintf('Módulo plástico Zy (cm³): %.2f\n', Zx_cm3);
