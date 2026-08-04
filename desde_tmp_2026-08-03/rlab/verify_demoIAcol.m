% Demo de IA_col_Acero_ETABS - columna CFT 200x200 mm, espesor 6 mm
% Ec = 2.195e6 tonf/m^2  (concreto f'c ~210 kgf/cm^2)
% Es = 2.039e7 tonf/m^2  (acero ASTM A36 nominal)

bc_demo = 0.20;       % m
hc_demo = 0.20;       % m
tc_demo = 0.006;      % m  (6 mm)
Ec_demo = 2.195e6;    % tonf/m^2
Es_demo = 2.039e7;    % tonf/m^2

% Seccion Compuesta (acero + concreto transformado)
[I_c, A_c] = IA_col_Acero_ETABS(bc_demo, hc_demo, tc_demo, Ec_demo, Es_demo, 'Compuesta');
fprintf('--- Seccion COMPUESTA ---\n');
fprintf('  I_eq = %.6e m^4   (%.2f cm^4)\n', I_c, I_c*1e8);
fprintf('  A_eq = %.6e m^2   (%.2f cm^2)\n', A_c, A_c*1e4);

% Seccion Simple (solo acero)
[I_s, A_s] = IA_col_Acero_ETABS(bc_demo, hc_demo, tc_demo, Ec_demo, Es_demo, 'Simple');
fprintf('--- Seccion SIMPLE (solo acero) ---\n');
fprintf('  I_acero = %.6e m^4   (%.2f cm^4)\n', I_s, I_s*1e8);
fprintf('  A_acero = %.6e m^2   (%.2f cm^2)\n', A_s, A_s*1e4);

% Echo matematico
I_c
A_c
I_s
A_s


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
