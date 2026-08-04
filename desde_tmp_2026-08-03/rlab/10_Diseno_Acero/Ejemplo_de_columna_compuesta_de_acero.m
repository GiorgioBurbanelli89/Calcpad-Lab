clc; clear all;

% Definir los parámetros
bc = 25; % cm
hc = 25; % cm
tc = 0.8; % cm
fc=210 % kgf/cm^2
Ec = 14100*sqrt(fc); % kgf/cm^2
Es = 2038901.92; % kgf/cm^2
Seccion = 'Compuesta'; % Puede ser 'Compuesta' o 'Simple'

% Llamar a la función
[I_seleccionada, A_seleccionada] = IA_col_Acero_ETABS(bc, hc, tc, Ec, Es, Seccion);

% Convertir las unidades de los resultados a cm^4 y cm^2
I_seleccionada_m4 = I_seleccionada %/ 100^4;
A_seleccionada_m2 = A_seleccionada %/ 100^2;

% Acceder a los resultados y mostrarlos
fprintf('Inercia seleccionada: %.6f cm^4\n', I_seleccionada_m4);
fprintf('Área seleccionada: %.6f cm^2\n', A_seleccionada_m2);

% Mostrar los resultados en la consola
disp(['Inercia seleccionada (cm^4): ', num2str(I_seleccionada_m4)]);
disp(['Área seleccionada (cm^2): ', num2str(A_seleccionada_m2)]);