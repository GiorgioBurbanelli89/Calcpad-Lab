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
function [I_seleccionada, A_seleccionada] = IA_col_Acero_ETABS(bc, hc, tc, Ec, Es, Seccion)
    % IA_col_Acero_ETABS - propiedades de columna CFT (Concrete-Filled Tube)
    %
    % Argumentos:
    %   bc, hc : base y altura externa de la columna de acero  [m]
    %   tc     : espesor de la pared del tubo de acero         [m]
    %   Ec     : modulo elastico del concreto                  [tonf/m^2]
    %   Es     : modulo elastico del acero                     [tonf/m^2]
    %   Seccion: 'Compuesta' (transformada con n=Ec/Es) o 'Simple' (solo acero)
    %
    % Retorna inercia y area de la seccion seleccionada.

    % Dimensiones del nucleo de concreto
    hconcreto = hc - 2 * tc;
    bconcreto = bc - 2 * tc;

    % Inercia y area del acero (caja hueca)
    I_acero = ((bc * hc^3) - (bconcreto * hconcreto^3)) / 12;
    A_acero = (bc * hc) - bconcreto * hconcreto;

    % Inercia y area del concreto (parte rellena)
    I_concreto = (bconcreto * hconcreto^3) / 12;
    A_concreto = bconcreto * hconcreto;

    % Seccion transformada: equivalente a acero usando n = Ec/Es
    I_eq_acero = I_acero + (Ec / Es) * I_concreto;
    A_eq_acero = A_acero + (Ec / Es) * A_concreto;

    if strcmp(Seccion, 'Compuesta')
        I_seleccionada = I_eq_acero;
        A_seleccionada = A_eq_acero;
    else
        I_seleccionada = I_acero;
        A_seleccionada = A_acero;
    end
end
