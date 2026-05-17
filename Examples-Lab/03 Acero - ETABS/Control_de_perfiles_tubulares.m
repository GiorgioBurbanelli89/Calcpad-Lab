
% Datos proporcionados
Hc = 304.8;  % Altura de la seccion en mm
Bc = 304.8;  % Ancho del perfil en mm
t = 14.76;   % Espesor del perfil en mm
R = 0;       % Radio de esquina interno (suponiendo esquinas afiladas)

% Calcular el area de la seccion transversal
A = (Hc * Bc) - ((Hc - 2 * t) * (Bc - 2 * t));

% Calcular el momento de inercia respecto a los ejes X e Y
Ix = (1/12) * (Bc * Hc^3 - (Bc - 2 * t) * (Hc - 2 * t)^3);
Iy = Ix;  % Debido a la simetria

% Calcular el modulo de seccion
Sx = Ix / (Hc / 2);
Sy = Iy / (Bc / 2);

% Calcular el modulo plastico
Zx = A * Hc / 4;
Zy = Zx;  % Debido a la simetria

% Convertir a cm2, cm^4 y cm3
A_cm2 = A / 100;         % Area en cm2
Ix_cm4 = Ix / 10000;     % Momento de inercia en cm^4
Sx_cm3 = Sx / 1000;      % Modulo de seccion en cm3
Zx_cm3 = Zx / 1000;      % Modulo plastico en cm3

% Mostrar resultados
fprintf('Area (cm2): %.2f\n', A_cm2);
fprintf('Momento de inercia Ix (cm^4): %.2f\n', Ix_cm4);
fprintf('Momento de inercia Iy (cm^4): %.2f\n', Ix_cm4);
fprintf('Modulo de seccion Sx (cm3): %.2f\n', Sx_cm3);
fprintf('Modulo de seccion Sy (cm3): %.2f\n', Sx_cm3);
fprintf('Modulo plastico Zx (cm3): %.2f\n', Zx_cm3);
fprintf('Modulo plastico Zy (cm3): %.2f\n', Zx_cm3);
