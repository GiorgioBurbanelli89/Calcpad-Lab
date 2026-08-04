% Dimensiones exteriores e interiores
B = 300; % mm
H = 300; % mm
t = 10;  % mm (espesor)
b = B - 2*t; % mm
h = H - 2*t; % mm

% Cálculo de la inercia
I_x = ((1/12) * (B * H^3 - b * h^3))/10^4;

% Mostrar el resultado
fprintf('La inercia de la columna hueca de 300x300x10 mm es %.2f mm^4\n', I_x);
